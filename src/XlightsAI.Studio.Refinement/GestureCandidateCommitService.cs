using System.Text.Json;
using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement;

/// <summary>Default <see cref="IRefinementCandidateCommitService"/>. See interface doc for ownership/scope.</summary>
public sealed class GestureCandidateCommitService : IRefinementCandidateCommitService
{
    /// <summary>
    /// Every gesture this build commits becomes one real, verified effect of this one recipe --
    /// <see cref="EffectRecipeRegistry.SimpleOnRecipeId"/> is the only recipe id this build's own
    /// <see cref="IEffectRecipeRegistry"/> currently recognizes (PHASE-02 Step 012). PHASE-10's own
    /// <see cref="TransitionGesture"/> carries no <c>RecipeId</c> of its own (disclosed in
    /// <c>implementation-evidence/PHASE-10/STEP-070.md</c>: concrete recipe/layer compilation was
    /// deferred); this step is the first point a gesture is ever compiled into a real document effect,
    /// so it reuses the one already-verified recipe rather than inventing an unverified new one
    /// (Section 2: "do not invent xLights effect parameters").
    /// </summary>
    public const string CommittedRecipeId = EffectRecipeRegistry.SimpleOnRecipeId;

    private static readonly TimeSpan DefaultStaleClaimAfter = TimeSpan.FromMinutes(5);

    private readonly IEffectLockPolicy _lockPolicy;
    private readonly IEffectRecipeRegistry _recipeRegistry;
    private readonly TimeSpan _staleClaimAfter;

    public GestureCandidateCommitService(IEffectLockPolicy lockPolicy, IEffectRecipeRegistry recipeRegistry, TimeSpan? staleClaimAfter = null)
    {
        _lockPolicy = lockPolicy ?? throw new ArgumentNullException(nameof(lockPolicy));
        _recipeRegistry = recipeRegistry ?? throw new ArgumentNullException(nameof(recipeRegistry));
        _staleClaimAfter = staleClaimAfter ?? DefaultStaleClaimAfter;
    }

    public async Task<EditApplyResult> AcceptAsync(
        IRefinementRunStore store,
        Guid runId,
        Guid candidateId,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(document);
        ct.ThrowIfCancellationRequested();

        var run = await store.LoadRunAsync(runId, ct).ConfigureAwait(false)
            ?? throw new RefinementRunInvalidException($"No run with Id '{runId}' exists.");

        if (run.DocumentId != document.Id)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{runId}' refines document '{run.DocumentId}', not '{document.Id}'; no candidate was committed.");
        }

        if (run.Status != RefinementStatus.AwaitingApproval)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{runId}' is not {RefinementStatus.AwaitingApproval} (was {run.Status}); only a run awaiting human approval can accept a candidate.");
        }

        // This step's own required failure/edge case, verbatim: "stale revision must be rejected
        // without partial mutation." The document may have changed (a manual edit landed, another run's
        // candidate was already accepted) since this run's own BaseRevision was snapshotted -- checked
        // before anything else is computed or touched.
        if (document.Revision != run.BaseRevision)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.StaleRevision,
                $"Document '{document.Id}' revision {document.Revision} no longer matches LoopRun '{runId}''s own BaseRevision {run.BaseRevision}; the document changed since this run was snapshotted. No candidate was committed.");
        }

        var attempts = await store.LoadAttemptsAsync(runId, ct).ConfigureAwait(false);
        var candidate = attempts.FirstOrDefault(a => a.Id == candidateId)
            ?? throw new RefinementRunInvalidException($"LoopRun '{runId}' has no recorded candidate attempt with Id '{candidateId}'.");

        if (candidate.Violations.Count > 0)
        {
            throw new RefinementRunInvalidException(
                $"Candidate attempt '{candidateId}' (attempt {candidate.Attempt}) carries {candidate.Violations.Count} hard-gate violation(s) and can never be committed: {string.Join(" ", candidate.Violations)}");
        }

        // Scope/lock hard gates, re-checked live against the caller's own current document -- never
        // trusted from whatever GestureQualityEvaluator already concluded against an earlier snapshot.
        _lockPolicy.AssertEditable(document, run.Scope);

        var gestures = candidate.Gestures ?? [];
        if (gestures.Count == 0)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.OutOfScope,
                $"Candidate attempt '{candidateId}' carries no gestures to commit; no candidate was committed.");
        }

        AssertGesturesInScope(run, candidateId, gestures);

        // Every effect this commit would add is computed in full, and only then, before this run's own
        // persisted status is ever touched -- "no partial mutation" means a layer collision on the Nth
        // gesture must leave both the document and the run exactly as they were, not just the first N-1
        // gestures un-applied.
        var newEffects = CompileEffects(document, candidateId, gestures);

        var newRevision = document.Revision + 1;
        var updatedDocument = document with { Revision = newRevision, Effects = document.Effects.Concat(newEffects).ToList() };

        var transaction = new EditTransaction(
            Guid.NewGuid(),
            document.Revision,
            newRevision,
            SerializedBeforePatch: "[]",
            JsonSerializer.Serialize(newEffects),
            DateTimeOffset.UtcNow);

        var claim = await store.TryClaimAsync(runId, _staleClaimAfter, ct).ConfigureAwait(false)
            ?? throw new RefinementRunInvalidException(
                $"LoopRun '{runId}' could not be claimed to record acceptance (already terminal, or held by another live claim).");

        var accepted = run with
        {
            Status = RefinementStatus.Accepted,
            BestCandidateId = candidateId,
            StopReason = $"Accepted: candidate attempt {candidate.Attempt} ('{candidateId}') committed as EditTransaction '{transaction.Id}'.",
        };
        await store.SaveRunStateAsync(accepted, claim.Token, CancellationToken.None).ConfigureAwait(false);

        return new EditApplyResult(updatedDocument, transaction);
    }

    public async Task<LoopRun> RejectAsync(IRefinementRunStore store, Guid runId, string? feedbackLabel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ct.ThrowIfCancellationRequested();

        var run = await store.LoadRunAsync(runId, ct).ConfigureAwait(false)
            ?? throw new RefinementRunInvalidException($"No run with Id '{runId}' exists.");

        if (run.Status != RefinementStatus.AwaitingApproval)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{runId}' is not {RefinementStatus.AwaitingApproval} (was {run.Status}); only a run awaiting human approval can be rejected.");
        }

        var claim = await store.TryClaimAsync(runId, _staleClaimAfter, ct).ConfigureAwait(false)
            ?? throw new RefinementRunInvalidException(
                $"LoopRun '{runId}' could not be claimed to record rejection (already terminal, or held by another live claim).");

        var reason = string.IsNullOrWhiteSpace(feedbackLabel)
            ? "Rejected: human reviewer declined every candidate."
            : $"Rejected: human reviewer declined every candidate (feedback: {feedbackLabel}).";

        var rejected = run with { Status = RefinementStatus.Rejected, StopReason = reason };
        return await store.SaveRunStateAsync(rejected, claim.Token, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Defense-in-depth re-check of exactly the two scope rules <see cref="GestureQualityEvaluator"/>'s
    /// own "legal effects" hard gate already enforced against an earlier snapshot (Step 081) -- re-run
    /// here against <paramref name="run"/>'s own live <see cref="LoopRun.Scope"/> rather than trusted from
    /// that earlier evaluation, the same "never trust a stale validation" rule
    /// <see cref="XlightsAI.Studio.AiEdits.EditProposalApprovalService"/>'s own doc comment already
    /// states for its sibling Accept path.
    /// </summary>
    private static void AssertGesturesInScope(LoopRun run, Guid candidateId, IReadOnlyList<TransitionGesture> gestures)
    {
        foreach (var gesture in gestures)
        {
            var outOfScopeProps = gesture.TargetProps.Where(p => !run.Scope.Models.Contains(p)).ToArray();
            if (outOfScopeProps.Length > 0)
            {
                throw new EditCommandRejectedException(
                    EditRejectionReason.OutOfScope,
                    $"Candidate attempt '{candidateId}' gesture '{gesture.Id}' targets prop(s) outside the run's permitted scope: {string.Join(", ", outOfScopeProps.Select(p => p.Value))}. No candidate was committed.");
            }

            if (gesture.Window.StartTicks < run.Scope.Time.StartTicks || gesture.Window.EndTicks > run.Scope.Time.EndTicks)
            {
                throw new EditCommandRejectedException(
                    EditRejectionReason.OutOfScope,
                    $"Candidate attempt '{candidateId}' gesture '{gesture.Id}' window [{gesture.Window.StartTicks},{gesture.Window.EndTicks}) exceeds the run's permitted time scope [{run.Scope.Time.StartTicks},{run.Scope.Time.EndTicks}). No candidate was committed.");
            }
        }
    }

    /// <summary>
    /// Compiles every gesture's own target prop into one real <see cref="EffectInstance"/> via
    /// <see cref="CommittedRecipeId"/>, choosing the lowest layer that does not collide (same half-open
    /// overlap rule as <see cref="ChoreographyCompiler"/>) with <paramref name="document"/>'s own already
    /// existing effects on that prop -- "no unintended model conflicts" applied here to a committed
    /// candidate, the same property <see cref="ChoreographyCompiler"/> already guarantees for role-based
    /// candidates. Pure computation; never mutates <paramref name="document"/>.
    /// </summary>
    private IReadOnlyList<EffectInstance> CompileEffects(SequenceDocument document, Guid candidateId, IReadOnlyList<TransitionGesture> gestures)
    {
        var occupiedByTarget = new Dictionary<ModelId, List<(int Layer, long Start, long End)>>();
        foreach (var effect in document.Effects)
        {
            if (!occupiedByTarget.TryGetValue(effect.Target, out var existing))
            {
                existing = [];
                occupiedByTarget[effect.Target] = existing;
            }

            existing.Add((effect.Layer, effect.StartTicks, effect.EndTicks));
        }

        var compiled = new List<EffectInstance>();
        foreach (var gesture in gestures.OrderBy(g => g.Window.StartTicks).ThenBy(g => g.Id))
        {
            foreach (var target in gesture.TargetProps)
            {
                if (!occupiedByTarget.TryGetValue(target, out var occupied))
                {
                    occupied = [];
                    occupiedByTarget[target] = occupied;
                }

                var chosenLayer = -1;
                for (var layer = 0; layer < ChoreographyCompiler.MaxLayers; layer++)
                {
                    var collides = occupied.Any(o => o.Layer == layer && Overlaps(o.Start, o.End, gesture.Window.StartTicks, gesture.Window.EndTicks));
                    if (!collides)
                    {
                        chosenLayer = layer;
                        break;
                    }
                }

                if (chosenLayer < 0)
                {
                    throw new EditCommandRejectedException(
                        EditRejectionReason.OutOfScope,
                        $"Candidate attempt '{candidateId}' gesture '{gesture.Id}' on '{target.Value}' at [{gesture.Window.StartTicks}, {gesture.Window.EndTicks}) collides with an existing effect on every layer up to {ChoreographyCompiler.MaxLayers}; no candidate was committed.");
                }

                var effect = _recipeRegistry.CreateVerified(CommittedRecipeId, target, gesture.Window.StartTicks, gesture.Window.EndTicks) with { Layer = chosenLayer };
                occupied.Add((chosenLayer, gesture.Window.StartTicks, gesture.Window.EndTicks));
                compiled.Add(effect);
            }
        }

        return compiled;
    }

    private static bool Overlaps(long aStart, long aEnd, long bStart, long bEnd) => aStart < bEnd && bStart < aEnd;
}
