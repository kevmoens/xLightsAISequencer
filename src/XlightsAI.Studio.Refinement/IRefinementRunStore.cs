namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 079's own persistence boundary: "Create persisted run/candidate/result schema with
/// hashes, seeds and state transitions... use transactionally claimed work." Every mutating method
/// requires the caller to present the exact <see cref="RefinementClaim.Token"/> currently held for that
/// run (obtained from <see cref="TryClaimAsync"/>) -- a run can only be mutated by whoever currently
/// holds its claim, which is how this interface realizes "transactionally claimed work" over a local,
/// single-project JSON store (no SQLite table exists for refinement runs, and none of this phase's
/// other persisted contracts -- <see cref="XlightsAI.Studio.Editing.IEditTransactionHistoryStore"/>,
/// <see cref="XlightsAI.Studio.Xlights.ISequenceDocumentStore"/> -- use SQLite either; inventing one
/// here for this step alone would duplicate, not share, state already modeled as a JSON file per
/// aggregate throughout this repository).
/// </summary>
public interface IRefinementRunStore
{
    /// <summary>
    /// Persists a brand-new run. <paramref name="run"/> must pass <see cref="LoopRun.Validate"/>, must
    /// have <see cref="LoopRun.Status"/> equal to <see cref="RefinementStatus.Queued"/>, and must carry
    /// no claim fields (a run cannot be created already claimed).
    /// </summary>
    /// <exception cref="RefinementRunInvalidException">
    /// <paramref name="run"/> fails <see cref="LoopRun.Validate"/>; its <see cref="LoopRun.Status"/> is
    /// not <see cref="RefinementStatus.Queued"/>; it carries a non-null claim field; or a run with the
    /// same <see cref="LoopRun.Id"/> already exists.
    /// </exception>
    Task CreateAsync(LoopRun run, CancellationToken ct);

    /// <summary>Returns the persisted run, or <see langword="null"/> when no run with <paramref name="runId"/> has ever been created.</summary>
    Task<LoopRun?> LoadRunAsync(Guid runId, CancellationToken ct);

    /// <summary>Returns every attempt recorded for <paramref name="runId"/> so far, ordered by <see cref="CandidateAttempt.Attempt"/>. Empty (never <see langword="null"/>) when none have been recorded, including when <paramref name="runId"/> is unknown.</summary>
    Task<IReadOnlyList<CandidateAttempt>> LoadAttemptsAsync(Guid runId, CancellationToken ct);

    /// <summary>
    /// Attempts to acquire exclusive, resumable ownership of <paramref name="runId"/>: succeeds either
    /// when the run has never been claimed, or when its existing claim's
    /// <see cref="LoopRun.HeartbeatAtUtc"/> is older than <paramref name="staleAfter"/> (the prior
    /// claimant is presumed crashed) -- this step's own required acceptance, verbatim: "Interrupted run
    /// resumes idempotently." Returns <see langword="null"/>, without mutating anything, when the run
    /// does not exist, is already in a terminal <see cref="RefinementStatus"/>
    /// (<see cref="RefinementStatusTransitions.IsTerminal"/>) -- nothing left to resume -- or is
    /// actively held by a live (non-stale) claim.
    /// </summary>
    Task<RefinementClaim?> TryClaimAsync(Guid runId, TimeSpan staleAfter, CancellationToken ct);

    /// <summary>Refreshes the liveness heartbeat for a claim this caller currently holds.</summary>
    /// <exception cref="RefinementRunInvalidException"><paramref name="runId"/> is unknown, or <paramref name="claimToken"/> does not match the run's current claim.</exception>
    Task HeartbeatAsync(Guid runId, Guid claimToken, CancellationToken ct);

    /// <summary>
    /// Persists <paramref name="run"/>'s updated state. The caller must currently hold
    /// <paramref name="claimToken"/> for this run. The transition from the currently-persisted
    /// <see cref="LoopRun.Status"/> to <paramref name="run"/>'s own <see cref="LoopRun.Status"/> must be
    /// legal per <see cref="RefinementStatusTransitions.CanTransition"/>. When <paramref name="run"/>'s
    /// own <see cref="LoopRun.Status"/> is terminal (<see cref="RefinementStatusTransitions.IsTerminal"/>),
    /// the claim is released as part of the same save (there is nothing left to resume). When
    /// <see cref="LoopRun.BestCandidateId"/> is set, it must identify an already-saved
    /// <see cref="CandidateAttempt"/> for this run whose own <see cref="CandidateAttempt.Violations"/> is
    /// empty -- this step's own required failure/edge case, verbatim: "Malformed proposal... must not
    /// auto-commit."
    /// </summary>
    /// <exception cref="RefinementRunInvalidException">
    /// <paramref name="run"/> fails <see cref="LoopRun.Validate"/>; no run with
    /// <see cref="LoopRun.Id"/> exists; <paramref name="claimToken"/> does not match the run's current
    /// claim; the status transition is illegal; or <see cref="LoopRun.BestCandidateId"/> names a
    /// missing attempt or one that carries violations.
    /// </exception>
    Task<LoopRun> SaveRunStateAsync(LoopRun run, Guid claimToken, CancellationToken ct);

    /// <summary>
    /// Persists <paramref name="attempt"/> against <paramref name="runId"/>. The caller must currently
    /// hold <paramref name="claimToken"/> for this run. Re-saving the same
    /// <see cref="CandidateAttempt.Attempt"/> ordinal overwrites the prior entry for that ordinal rather
    /// than appending a duplicate (idempotent resume).
    /// </summary>
    /// <exception cref="RefinementRunInvalidException">
    /// <paramref name="attempt"/> fails <see cref="CandidateAttempt.Validate"/>;
    /// <paramref name="attempt"/>'s own <see cref="CandidateAttempt.RunId"/> does not equal
    /// <paramref name="runId"/>; no run with <paramref name="runId"/> exists; or
    /// <paramref name="claimToken"/> does not match the run's current claim.
    /// </exception>
    Task<CandidateAttempt> SaveAttemptAsync(Guid runId, Guid claimToken, CandidateAttempt attempt, CancellationToken ct);

    /// <summary>Releases a held claim without changing <see cref="LoopRun.Status"/> (a clean pause, distinct from cancellation).</summary>
    /// <exception cref="RefinementRunInvalidException"><paramref name="runId"/> is unknown, or <paramref name="claimToken"/> does not match the run's current claim.</exception>
    Task ReleaseClaimAsync(Guid runId, Guid claimToken, CancellationToken ct);

    /// <summary>
    /// PHASE-12 Step 083 ("Add retry, time, compute and quality thresholds; cancellation"): sets
    /// <see cref="LoopRun.CancellationRequested"/> to <see langword="true"/> for <paramref name="runId"/>,
    /// without requiring (or touching) that run's current claim -- deliberately, since the caller requesting
    /// cancellation (e.g. a human clicking "Cancel" in the UI) is, by construction, a different actor than
    /// whichever <see cref="IRefinementOrchestrator.ExecuteAsync"/> call currently holds the claim and is
    /// actively running attempts against it. A long-running execution loop is expected to observe this flag
    /// (re-loading the run) between attempts and honor it, rather than this method itself interrupting
    /// anything synchronously. A no-op (returns normally, mutates nothing) when <paramref name="runId"/>'s
    /// current <see cref="LoopRun.Status"/> is already terminal (<see cref="RefinementStatusTransitions.IsTerminal"/>)
    /// -- there is nothing left to cancel.
    /// </summary>
    /// <exception cref="RefinementRunInvalidException"><paramref name="runId"/> is unknown.</exception>
    Task RequestCancellationAsync(Guid runId, CancellationToken ct);
}
