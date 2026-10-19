using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Section 3's own proposed <c>LoopRun</c> record, realized for this step's own narrower scope
/// ("Create persisted run/candidate/result schema with hashes, seeds and state transitions"): a single
/// bounded refinement attempt against one <see cref="DocumentId"/>, with the resumable-job fields this
/// step's own implementation guidance names, verbatim -- "resumable job states with input hashes,
/// selected scope, attempts, last heartbeat, seeds and cancellation records... transactionally claimed
/// work."
/// </summary>
/// <param name="Id">This run's own stable identity.</param>
/// <param name="DocumentId">
/// The <see cref="XlightsAI.Studio.Xlights.SequenceDocument.Id"/> this run refines. Not part of the
/// spec's own proposed <c>LoopRun</c> signature -- added additively (the same kind of disclosed,
/// additive deviation PHASE-10/11's own ADRs already used) because <see cref="Scope"/> alone (an
/// <see cref="EditScope"/>) names a time/model boundary, never a document identity; without it, a
/// persisted run could not say which document's revision <see cref="BaseRevision"/> actually refers to.
/// </param>
/// <param name="BaseRevision">The document revision this run was snapshotted against (PHASE-12 Section 3: "Each attempt takes a versioned snapshot").</param>
/// <param name="Scope">The exact time window/model set this run is permitted to touch -- carried unchanged from PHASE-03's already-accepted <see cref="EditScope"/>, never re-derived.</param>
/// <param name="InputHash">A stable hash of this run's own deterministic inputs (<see cref="DocumentId"/>, <see cref="BaseRevision"/>, <see cref="Scope"/>, <see cref="Seed"/>) -- this step's own implementation guidance, verbatim: "resumable job states with input hashes."</param>
/// <param name="Seed">The run-level seed Step 080's own seeded candidate generator will consume; persisted here so a resumed run reproduces the same attempt sequence rather than silently re-randomizing.</param>
/// <param name="MaxAttempts">The hard attempt budget (PHASE-12 Section 7 cross-cutting test #4: "MaxAttempts enforced with exact attempt count"). Must be strictly positive.</param>
/// <param name="AttemptsMade">How many attempts this run has actually recorded so far; never greater than <see cref="MaxAttempts"/>.</param>
/// <param name="Status">This run's current lifecycle state -- see <see cref="RefinementStatusTransitions"/> for which transitions are legal.</param>
/// <param name="BestCandidateId">The best technical candidate retained so far, or <see langword="null"/> before any candidate has passed its hard gates. Never set to a candidate attempt that itself carries violations -- enforced by <see cref="IRefinementRunStore.SaveRunStateAsync"/>, not by this record's own constructor (validating that requires the attempt list, which this record does not carry).</param>
/// <param name="StopReason">A human-readable reason once this run leaves a non-terminal state for a terminal one (<c>"Exhausted"</c>, <c>"NoImprovement"</c>, <c>"Cancelled"</c>, <c>"MalformedProposal"</c>, an approval outcome, etc.) -- PHASE-12 Section 3: "preserve best candidate, metric evidence and failed attempts for diagnostic reuse," surfaced here as the run's own terminal explanation. <see langword="null"/> while still in progress.</param>
/// <param name="ClaimToken">The opaque token currently holding this run's exclusive claim for resumption (PHASE-12 Step 079 implementation guidance: "transactionally claimed work"), or <see langword="null"/> when unclaimed.</param>
/// <param name="ClaimedAtUtc">When <see cref="ClaimToken"/> was most recently (re)acquired, or <see langword="null"/> when unclaimed.</param>
/// <param name="HeartbeatAtUtc">The claim holder's last liveness heartbeat; a claim older than the caller's own staleness window is treated as abandoned by a crashed process and may be reclaimed (see <see cref="IRefinementRunStore.TryClaimAsync"/>).</param>
/// <param name="CreatedAtUtc">When this run was first persisted.</param>
/// <param name="GestureFamily">
/// PHASE-12 Step 080 ("Build seeded candidate generator constrained to selected gesture family"): the
/// single <see cref="TransitionKind"/> this run's <see cref="ISeededCandidateGenerator"/> is constrained
/// to -- every generated gesture's own <see cref="TransitionGesture.Kind"/> must equal this value
/// (Step 080's own heading, verbatim: "constrained to selected gesture family"). Added additively,
/// defaulted to <see langword="null"/>, after Step 079's own already-accepted <c>LoopRun</c> shape: a
/// Step-079-era persisted run (no family ever selected) still deserializes unchanged, and
/// <see cref="DeterministicGestureCandidateGenerator"/> itself -- not this record's own
/// <see cref="Validate"/> -- rejects attempting to generate against a run with no family selected
/// (a generation-time precondition, not a persisted-shape defect). <see cref="TransitionKind"/> is
/// reused, unmodified, from PHASE-10's own already-accepted contract rather than inventing a parallel
/// "gesture family" enum.
/// </param>
/// <param name="Budget">
/// PHASE-12 Step 083 ("Add retry, time, compute and quality thresholds; cancellation"): the optional time/
/// compute/quality/no-improvement-patience thresholds bounding this run, in addition to
/// <see cref="MaxAttempts"/> (the "retry" threshold, already named by Step 079). Added additively, defaulted
/// to <see langword="null"/>, after Step 082's own already-accepted <c>LoopRun</c> shape -- a Step
/// 079/080/081/082-era persisted run (no budget ever configured) still deserializes unchanged and remains
/// bounded by <see cref="MaxAttempts"/> alone, exactly as it already was.
/// </param>
/// <param name="ComputeUnitsSpent">
/// PHASE-12 Step 083's own "compute" threshold tracking field: the running total compute cost this run has
/// consumed generating/evaluating attempts so far, compared against <see cref="RefinementBudget.MaxComputeUnits"/>
/// when <see cref="Budget"/> is set. Never negative.
/// </param>
/// <param name="ConsecutiveNoImprovementCount">
/// PHASE-12 Step 083's own "no-improvement patience" tracking field: how many attempts in a row have failed
/// to replace <see cref="BestCandidateId"/> with a strictly better candidate, compared against
/// <see cref="RefinementBudget.NoImprovementPatience"/> when <see cref="Budget"/> is set. Reset to <c>0</c>
/// whenever an attempt does improve the retained best candidate. Never negative.
/// </param>
/// <param name="StartedAtUtc">
/// PHASE-12 Step 083's own "time" threshold baseline: when this run actually left <see cref="RefinementStatus.Queued"/>
/// and began generating its first attempt, distinct from <see cref="CreatedAtUtc"/> (when the run was first
/// persisted, which may be earlier if the run sat queued before execution began). <see langword="null"/>
/// before the run has ever started executing.
/// </param>
/// <param name="CancellationRequested">
/// PHASE-12 Step 083's own "cancellation" stop criterion: <see langword="true"/> once
/// <see cref="IRefinementOrchestrator.CancelAsync"/> has been requested for this run (via
/// <see cref="IRefinementRunStore.RequestCancellationAsync"/>) -- a caller distinct from whoever currently
/// holds this run's claim may set this flag without itself holding the claim, so a long-running
/// <see cref="IRefinementOrchestrator.ExecuteAsync"/> loop can observe and honor it before its next attempt.
/// </param>
public sealed record LoopRun(
    Guid Id,
    Guid DocumentId,
    long BaseRevision,
    EditScope Scope,
    string InputHash,
    int Seed,
    int MaxAttempts,
    int AttemptsMade,
    RefinementStatus Status,
    Guid? BestCandidateId,
    string? StopReason,
    Guid? ClaimToken,
    DateTimeOffset? ClaimedAtUtc,
    DateTimeOffset? HeartbeatAtUtc,
    DateTimeOffset CreatedAtUtc,
    TransitionKind? GestureFamily = null,
    RefinementBudget? Budget = null,
    double ComputeUnitsSpent = 0.0,
    int ConsecutiveNoImprovementCount = 0,
    DateTimeOffset? StartedAtUtc = null,
    bool CancellationRequested = false)
{
    /// <summary>
    /// Validates this run's own shape in isolation -- never reaches into a store or attempt list.
    /// </summary>
    /// <exception cref="RefinementRunInvalidException">
    /// <see cref="Id"/> or <see cref="DocumentId"/> is empty; <see cref="BaseRevision"/> is negative;
    /// <see cref="MaxAttempts"/> is not strictly positive; <see cref="AttemptsMade"/> is negative or
    /// exceeds <see cref="MaxAttempts"/>; <see cref="InputHash"/> is blank; or a claim field is
    /// inconsistent (<see cref="ClaimToken"/> set without both <see cref="ClaimedAtUtc"/> and
    /// <see cref="HeartbeatAtUtc"/>, or vice versa).
    /// </exception>
    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new RefinementRunInvalidException("A LoopRun's Id must not be empty.");
        }

        if (DocumentId == Guid.Empty)
        {
            throw new RefinementRunInvalidException($"LoopRun '{Id}' DocumentId must not be empty.");
        }

        ArgumentNullException.ThrowIfNull(Scope);

        if (BaseRevision < 0)
        {
            throw new RefinementRunInvalidException($"LoopRun '{Id}' BaseRevision must not be negative (was {BaseRevision}).");
        }

        if (MaxAttempts <= 0)
        {
            throw new RefinementRunInvalidException($"LoopRun '{Id}' MaxAttempts must be strictly positive (was {MaxAttempts}).");
        }

        if (AttemptsMade < 0 || AttemptsMade > MaxAttempts)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{Id}' AttemptsMade must be between 0 and MaxAttempts={MaxAttempts} (was {AttemptsMade}).");
        }

        if (string.IsNullOrWhiteSpace(InputHash))
        {
            throw new RefinementRunInvalidException($"LoopRun '{Id}' InputHash must not be blank.");
        }

        var claimFieldsPresent = new[] { ClaimToken is not null, ClaimedAtUtc is not null, HeartbeatAtUtc is not null };
        if (claimFieldsPresent.Distinct().Count() != 1)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{Id}' claim fields must be all-set or all-null together (ClaimToken={ClaimToken}, ClaimedAtUtc={ClaimedAtUtc}, HeartbeatAtUtc={HeartbeatAtUtc}).");
        }

        Budget?.Validate();

        if (!double.IsFinite(ComputeUnitsSpent) || ComputeUnitsSpent < 0.0)
        {
            throw new RefinementRunInvalidException($"LoopRun '{Id}' ComputeUnitsSpent must be a finite, non-negative number (was {ComputeUnitsSpent}).");
        }

        if (ConsecutiveNoImprovementCount < 0)
        {
            throw new RefinementRunInvalidException($"LoopRun '{Id}' ConsecutiveNoImprovementCount must not be negative (was {ConsecutiveNoImprovementCount}).");
        }
    }
}
