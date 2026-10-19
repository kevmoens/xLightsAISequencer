using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement.Tests;

/// <summary>
/// PHASE-12 Step 084 -- "Add candidate gallery, score explanations, preview and explicit selection."
/// Required acceptance (verbatim): "User can compare alternatives and commit chosen candidate." Required
/// failure/edge case (verbatim): "Out-of-scope, locked or stale revision must be rejected without partial
/// mutation." These tests exercise the real <see cref="GestureCandidateCommitService"/> against a real
/// <see cref="JsonRefinementRunStore"/> (real temp-directory files, no mocking) and a run actually driven
/// to <see cref="RefinementStatus.AwaitingApproval"/> by the real <see cref="GestureRefinementOrchestrator"/>
/// (Step 083) -- the same convention Steps 079-083 already established.
/// </summary>
public sealed class Step084AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step084-" + Guid.NewGuid().ToString("N"));
    private readonly GestureCandidateCommitService _commitService = new(new EffectLockPolicy(), new EffectRecipeRegistry());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static EditScope Scope(params string[] models) =>
        new(new TimeWindow(0, 8_000_000), models.Select(m => new ModelId(m)).ToHashSet());

    private static LoopRun NewQueuedRun(Guid documentId, EditScope scope, TransitionKind family = TransitionKind.Transition, int maxAttempts = 3) => new(
        Id: Guid.NewGuid(),
        DocumentId: documentId,
        BaseRevision: 1,
        Scope: scope,
        InputHash: "hash-of-scope-and-seed",
        Seed: 42,
        MaxAttempts: maxAttempts,
        AttemptsMade: 0,
        Status: RefinementStatus.Queued,
        BestCandidateId: null,
        StopReason: null,
        ClaimToken: null,
        ClaimedAtUtc: null,
        HeartbeatAtUtc: null,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        GestureFamily: family);

    private static GestureRefinementOrchestrator NewOrchestrator(IRefinementRunStore store) => new(
        store,
        new DeterministicGestureCandidateGenerator(),
        new GestureTargetedRepairStrategy(),
        new GestureQualityEvaluator(),
        ChoreographyQualityContext.Empty);

    private static SequenceDocument EmptyDocument(Guid id, long revision = 1) => new(id, revision, []);

    /// <summary>Drives a fresh run through the real orchestrator to <see cref="RefinementStatus.AwaitingApproval"/> with a real, non-violating retained best candidate.</summary>
    private static async Task<LoopRun> SeedAwaitingApprovalRunAsync(IRefinementRunStore store, Guid documentId, EditScope scope, int maxAttempts = 3)
    {
        var run = NewQueuedRun(documentId, scope, maxAttempts: maxAttempts);
        await store.CreateAsync(run, CancellationToken.None);
        var finished = await NewOrchestrator(store).ExecuteAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
        Assert.NotNull(finished.BestCandidateId);
        return finished;
    }

    // ---- Required acceptance, verbatim: "User can compare alternatives and commit chosen candidate." ----

    [Fact]
    public async Task Step084_Acceptance_AcceptBestCandidate_CommitsNewEffectsAndTransitionsRunToAccepted()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = await SeedAwaitingApprovalRunAsync(store, document.Id, Scope("Arches", "MegaTree"));

        var result = await _commitService.AcceptAsync(store, run.Id, run.BestCandidateId!.Value, document, CancellationToken.None);

        Assert.Equal(document.Revision + 1, result.Document.Revision);
        Assert.True(result.Document.Effects.Count > document.Effects.Count);
        Assert.All(result.Document.Effects, e => Assert.Equal(EffectRecipeRegistry.SimpleOnRecipeId, e.RecipeId));
        Assert.Equal(document.Revision, result.Transaction.BeforeRevision);
        Assert.Equal(result.Document.Revision, result.Transaction.AfterRevision);

        var finalRun = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Accepted, finalRun!.Status);
        Assert.Equal(run.BestCandidateId, finalRun.BestCandidateId);
        Assert.Null(finalRun.ClaimToken); // terminal status auto-releases the claim
        Assert.Contains(run.BestCandidateId.ToString()!, finalRun.StopReason);
    }

    [Fact]
    public async Task Step084_Acceptance_UserCanChooseANonBestAlternative_NotOnlyTheRetainedBest()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = await SeedAwaitingApprovalRunAsync(store, document.Id, Scope("Arches", "MegaTree"), maxAttempts: 3);

        var attempts = await store.LoadAttemptsAsync(run.Id, CancellationToken.None);
        var nonBest = attempts.First(a => a.Id != run.BestCandidateId && a.Violations.Count == 0);

        var result = await _commitService.AcceptAsync(store, run.Id, nonBest.Id, document, CancellationToken.None);

        var finalRun = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Accepted, finalRun!.Status);
        Assert.Equal(nonBest.Id, finalRun.BestCandidateId); // the explicitly chosen alternative, not whatever was previously retained
        Assert.NotEmpty(result.Document.Effects);
    }

    [Fact]
    public async Task Step084_Acceptance_Reject_LeavesDocumentByteEquivalent_RecordsOptionalFeedbackLabel()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = await SeedAwaitingApprovalRunAsync(store, document.Id, Scope("Arches"));

        var rejected = await _commitService.RejectAsync(store, run.Id, "too busy for this phrase", CancellationToken.None);

        Assert.Equal(RefinementStatus.Rejected, rejected.Status);
        Assert.Contains("too busy for this phrase", rejected.StopReason);
        Assert.Null(rejected.ClaimToken);

        // "Reject leaves state byte-equivalent" -- nothing about the caller's own document changes; this
        // method's own signature never even accepts one.
        var reloaded = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Rejected, reloaded!.Status);
    }

    [Fact]
    public async Task Step084_Acceptance_Reject_WithNoFeedbackLabel_RecordsGenericReason()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = await SeedAwaitingApprovalRunAsync(store, document.Id, Scope("Arches"));

        var rejected = await _commitService.RejectAsync(store, run.Id, feedbackLabel: null, CancellationToken.None);

        Assert.Equal(RefinementStatus.Rejected, rejected.Status);
        Assert.StartsWith("Rejected:", rejected.StopReason);
    }

    // ---- Required failure/edge case, verbatim: "Out-of-scope, locked or stale revision must be rejected without partial mutation." ----

    [Fact]
    public async Task Step084_NegativePath_StaleRevision_RejectedWithoutPartialMutation()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = await SeedAwaitingApprovalRunAsync(store, document.Id, Scope("Arches"));

        // The document moved on (e.g. a manual edit landed) since this run's own BaseRevision was
        // snapshotted -- revision 2 no longer matches BaseRevision 1.
        var staleDocument = document with { Revision = document.Revision + 1 };

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => _commitService.AcceptAsync(store, run.Id, run.BestCandidateId!.Value, staleDocument, CancellationToken.None));

        Assert.Equal(EditRejectionReason.StaleRevision, ex.Reason);

        // No partial mutation: the run is still exactly where it was (AwaitingApproval, re-claimable).
        var reloaded = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, reloaded!.Status);
        Assert.Null(reloaded.ClaimToken);
    }

    [Fact]
    public async Task Step084_NegativePath_LockedEffectInScope_RejectedWithoutPartialMutation()
    {
        var store = new JsonRefinementRunStore(_root);
        var documentId = Guid.NewGuid();
        var scope = Scope("Arches");
        var run = await SeedAwaitingApprovalRunAsync(store, documentId, scope);

        var lockedEffect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Arches"), Layer: 0, StartTicks: scope.Time.StartTicks, EndTicks: scope.Time.EndTicks,
            RecipeId: EffectRecipeRegistry.SimpleOnRecipeId, RecipeVersion: "1", Parameters: new Dictionary<string, string>(), Locked: true);
        var document = new SequenceDocument(documentId, 1, [lockedEffect]);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => _commitService.AcceptAsync(store, run.Id, run.BestCandidateId!.Value, document, CancellationToken.None));

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);

        var reloaded = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, reloaded!.Status);
        Assert.Null(reloaded.ClaimToken);
        Assert.Single(document.Effects); // document itself was never touched
    }

    [Fact]
    public async Task Step084_NegativePath_CandidateGestureOutOfScope_RejectedWithoutPartialMutation()
    {
        var store = new JsonRefinementRunStore(_root);
        var documentId = Guid.NewGuid();
        var scope = Scope("Arches");
        var run = await SeedAwaitingApprovalRunAsync(store, documentId, scope);

        // Directly save an out-of-scope-targeting attempt that bypassed the evaluator's own already-
        // accepted "legal effects" gate -- this step's own defense-in-depth re-check at commit time, not
        // merely trusting an earlier evaluation.
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(claim);
        var outOfScopeGesture = new TransitionGesture(
            Guid.NewGuid(), TransitionKind.Transition, scope.Time, [new ModelId("MegaTree")], 0.5, 0.5);
        var outOfScopeAttempt = new CandidateAttempt(
            Guid.NewGuid(), run.Id, Attempt: run.AttemptsMade, Seed: run.Seed, SnapshotHash: "hash-out-of-scope",
            Status: RefinementStatus.PreviewReady, Violations: [], RecordedAtUtc: DateTimeOffset.UtcNow, Gestures: [outOfScopeGesture]);
        await store.SaveAttemptAsync(run.Id, claim!.Token, outOfScopeAttempt, CancellationToken.None);
        await store.ReleaseClaimAsync(run.Id, claim.Token, CancellationToken.None);

        var document = EmptyDocument(documentId);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => _commitService.AcceptAsync(store, run.Id, outOfScopeAttempt.Id, document, CancellationToken.None));

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);

        var reloaded = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, reloaded!.Status);
        Assert.Null(reloaded.ClaimToken);
    }

    [Fact]
    public async Task Step084_NegativePath_ViolatingCandidate_NeverCommits()
    {
        var store = new JsonRefinementRunStore(_root);
        var documentId = Guid.NewGuid();
        var scope = Scope("Arches");
        var run = await SeedAwaitingApprovalRunAsync(store, documentId, scope);

        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(claim);
        var violatingAttempt = new CandidateAttempt(
            Guid.NewGuid(), run.Id, Attempt: run.AttemptsMade, Seed: run.Seed, SnapshotHash: "hash-violating",
            Status: RefinementStatus.Rejected, Violations: ["Legal effects hard gate failed: candidate carries no gestures to evaluate (silence/empty generation)."],
            RecordedAtUtc: DateTimeOffset.UtcNow, Gestures: []);
        await store.SaveAttemptAsync(run.Id, claim!.Token, violatingAttempt, CancellationToken.None);
        await store.ReleaseClaimAsync(run.Id, claim.Token, CancellationToken.None);

        var document = EmptyDocument(documentId);

        var ex = await Assert.ThrowsAsync<RefinementRunInvalidException>(
            () => _commitService.AcceptAsync(store, run.Id, violatingAttempt.Id, document, CancellationToken.None));

        Assert.Contains("violation", ex.Message, StringComparison.OrdinalIgnoreCase);

        var reloaded = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, reloaded!.Status);
        Assert.Null(reloaded.ClaimToken);
    }

    [Fact]
    public async Task Step084_NegativePath_RunNotAwaitingApproval_RejectedWithoutMutation()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = NewQueuedRun(document.Id, Scope("Arches"));
        await store.CreateAsync(run, CancellationToken.None);

        await Assert.ThrowsAsync<RefinementRunInvalidException>(
            () => _commitService.AcceptAsync(store, run.Id, Guid.NewGuid(), document, CancellationToken.None));

        await Assert.ThrowsAsync<RefinementRunInvalidException>(
            () => _commitService.RejectAsync(store, run.Id, null, CancellationToken.None));

        var reloaded = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Queued, reloaded!.Status); // untouched
    }

    [Fact]
    public async Task Step084_NegativePath_UnknownCandidateId_Throws()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = await SeedAwaitingApprovalRunAsync(store, document.Id, Scope("Arches"));

        await Assert.ThrowsAsync<RefinementRunInvalidException>(
            () => _commitService.AcceptAsync(store, run.Id, Guid.NewGuid(), document, CancellationToken.None));
    }

    [Fact]
    public async Task Step084_NegativePath_WrongDocument_Throws()
    {
        var store = new JsonRefinementRunStore(_root);
        var document = EmptyDocument(Guid.NewGuid());
        var run = await SeedAwaitingApprovalRunAsync(store, document.Id, Scope("Arches"));
        var wrongDocument = EmptyDocument(Guid.NewGuid());

        await Assert.ThrowsAsync<RefinementRunInvalidException>(
            () => _commitService.AcceptAsync(store, run.Id, run.BestCandidateId!.Value, wrongDocument, CancellationToken.None));
    }

    // ---- Reproducibility / layer-safety: committed effects never collide with existing document effects ----

    [Fact]
    public async Task Step084_Acceptance_CommittedEffects_NeverCollideWithExistingDocumentEffects()
    {
        var store = new JsonRefinementRunStore(_root);
        var documentId = Guid.NewGuid();
        var scope = Scope("Arches", "MegaTree");
        var run = await SeedAwaitingApprovalRunAsync(store, documentId, scope);

        var existingEffect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Arches"), Layer: 0, StartTicks: scope.Time.StartTicks, EndTicks: scope.Time.EndTicks,
            RecipeId: EffectRecipeRegistry.SimpleOnRecipeId, RecipeVersion: "1", Parameters: new Dictionary<string, string>(), Locked: false);
        var document = new SequenceDocument(documentId, 1, [existingEffect]);

        var result = await _commitService.AcceptAsync(store, run.Id, run.BestCandidateId!.Value, document, CancellationToken.None);

        var archesEffects = result.Document.Effects.Where(e => e.Target == new ModelId("Arches")).ToArray();
        foreach (var a in archesEffects)
        {
            foreach (var b in archesEffects)
            {
                if (a.Id == b.Id)
                {
                    continue;
                }

                var overlaps = a.StartTicks < b.EndTicks && b.StartTicks < a.EndTicks;
                if (overlaps)
                {
                    Assert.NotEqual(a.Layer, b.Layer);
                }
            }
        }
    }
}
