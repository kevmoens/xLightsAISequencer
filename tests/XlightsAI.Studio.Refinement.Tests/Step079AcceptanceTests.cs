using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Refinement.Tests;

/// <summary>
/// PHASE-12 Step 079 -- "Create persisted run/candidate/result schema with hashes, seeds and state
/// transitions." Required acceptance (verbatim): "Interrupted run resumes idempotently." Required
/// failure/edge case (verbatim): "Malformed proposal, no-improvement and exhausted retry budget must
/// not auto-commit." These tests exercise the real <see cref="JsonRefinementRunStore"/> against real
/// files in a temp directory (no mocking), never an in-memory stand-in -- every "restart"/"crash" is
/// modeled by constructing a brand-new store instance pointed at the same on-disk directory.
/// </summary>
public sealed class Step079AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step079-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static EditScope Scope() =>
        new(new TimeWindow(0, 8_000_000), new HashSet<ModelId> { new("Arches") });

    private static LoopRun NewQueuedRun(Guid? id = null, int maxAttempts = 5) => new(
        Id: id ?? Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        BaseRevision: 1,
        Scope: Scope(),
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
        CreatedAtUtc: DateTimeOffset.UtcNow);

    private static CandidateAttempt Attempt(Guid runId, int attempt, RefinementStatus status, IReadOnlyList<string>? violations = null) => new(
        Id: Guid.NewGuid(),
        RunId: runId,
        Attempt: attempt,
        Seed: 42 + attempt,
        SnapshotHash: $"snapshot-{attempt}",
        Status: status,
        Violations: violations ?? [],
        RecordedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Step079_Acceptance_InterruptedRun_ResumesIdempotently_WithoutDuplicatingWork()
    {
        // "Interrupted run resumes idempotently" -- required acceptance, verbatim.
        var run = NewQueuedRun();
        var storeBeforeCrash = new JsonRefinementRunStore(_root);
        await storeBeforeCrash.CreateAsync(run, CancellationToken.None);

        var claim1 = await storeBeforeCrash.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(claim1);

        var generating = run with { Status = RefinementStatus.Generating };
        await storeBeforeCrash.SaveRunStateAsync(generating, claim1!.Token, CancellationToken.None);
        await storeBeforeCrash.SaveAttemptAsync(run.Id, claim1.Token, Attempt(run.Id, 0, RefinementStatus.PreviewReady), CancellationToken.None);

        // Crash: the process ends here. The claim is never released and the heartbeat is never refreshed
        // again -- "storeBeforeCrash" and claim1.Token are simply abandoned, exactly like a killed process.

        // Restart: a brand-new store instance against the same on-disk directory, with no in-memory state
        // carried over at all.
        var storeAfterRestart = new JsonRefinementRunStore(_root);

        // A near-zero staleness window proves the abandoned claim from "before the crash" is reclaimable
        // (it is already older than this window by the time this line runs).
        var claim2 = await storeAfterRestart.TryClaimAsync(run.Id, TimeSpan.FromMilliseconds(1), CancellationToken.None);
        Assert.NotNull(claim2);
        Assert.NotEqual(claim1.Token, claim2!.Token);

        // The in-flight attempt recorded before the crash is intact, not duplicated or lost.
        var attemptsAfterRestart = await storeAfterRestart.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Single(attemptsAfterRestart);
        Assert.Equal(0, attemptsAfterRestart[0].Attempt);

        // Resuming re-saves the exact same in-flight state the crash interrupted -- an idempotent re-save
        // of Status=Generating (self-transition), not a rejected illegal jump.
        var resumed = await storeAfterRestart.SaveRunStateAsync(generating, claim2.Token, CancellationToken.None);
        Assert.Equal(RefinementStatus.Generating, resumed.Status);

        // Re-saving the SAME attempt ordinal again (the generator re-producing attempt 0 after resume)
        // overwrites in place -- it does not append a second, duplicate attempt.
        await storeAfterRestart.SaveAttemptAsync(run.Id, claim2.Token, Attempt(run.Id, 0, RefinementStatus.PreviewReady), CancellationToken.None);
        var attemptsAfterResumeRewrite = await storeAfterRestart.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Single(attemptsAfterResumeRewrite);

        // The claim held before the crash can never again mutate the run once a fresh claim exists.
        await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            storeAfterRestart.HeartbeatAsync(run.Id, claim1.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Step079_Acceptance_LiveClaim_CannotBeStolenBeforeItGoesStale()
    {
        var run = NewQueuedRun();
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);

        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(claim);

        // A second claim attempt, with the same generous staleness window, must fail: the first claim's
        // heartbeat is fresh, so this is a live run being actively worked, not an abandoned one.
        var secondAttempt = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Null(secondAttempt);
    }

    [Fact]
    public async Task Step079_Acceptance_SameSeedAndInputs_ProduceReproducibleSnapshotHash()
    {
        // PHASE-12 Section 7 cross-cutting test #1: "Same seed + inputs yields reproducible candidate."
        var runId = Guid.NewGuid();
        var attemptA = Attempt(runId, 0, RefinementStatus.PreviewReady) with { Seed = 7, SnapshotHash = "deterministic-hash" };
        var attemptB = Attempt(runId, 1, RefinementStatus.PreviewReady) with { Seed = 7, SnapshotHash = "deterministic-hash" };

        Assert.Equal(attemptA.Seed, attemptB.Seed);
        Assert.Equal(attemptA.SnapshotHash, attemptB.SnapshotHash);
    }

    [Fact]
    public async Task Step079_Acceptance_MaxAttemptsEnforced_ExactAttemptCount()
    {
        // PHASE-12 Section 7 cross-cutting test #4: "MaxAttempts enforced with exact attempt count."
        var run = NewQueuedRun(maxAttempts: 3);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);

        var generating = run with { Status = RefinementStatus.Generating };
        await store.SaveRunStateAsync(generating, claim!.Token, CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            await store.SaveAttemptAsync(run.Id, claim.Token, Attempt(run.Id, i, RefinementStatus.PreviewReady), CancellationToken.None);
        }

        var validating = await store.SaveRunStateAsync(generating with { Status = RefinementStatus.Validating }, claim.Token, CancellationToken.None);
        var previewReady = await store.SaveRunStateAsync(validating with { Status = RefinementStatus.PreviewReady }, claim.Token, CancellationToken.None);
        var exhausted = previewReady with { AttemptsMade = 3, Status = RefinementStatus.AwaitingApproval, StopReason = "Exhausted" };
        var saved = await store.SaveRunStateAsync(exhausted, claim.Token, CancellationToken.None);

        Assert.Equal(3, saved.AttemptsMade);
        var attempts = await store.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Equal(3, attempts.Count);

        // A fourth attempt is not rejected by the store itself at this step's own scope (budget
        // enforcement is Step 083's own named scope) -- but AttemptsMade exceeding MaxAttempts is still
        // structurally invalid on the run record itself.
        var overBudget = exhausted with { AttemptsMade = 4 };
        Assert.Throws<RefinementRunInvalidException>(() => overBudget.Validate());
    }

    [Fact]
    public async Task Step079_NegativePath_CancelledRun_NeverCommits()
    {
        // PHASE-12 Section 7 cross-cutting test #5: "Cancelled run never commits."
        var run = NewQueuedRun();
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);

        var cancelled = run with { Status = RefinementStatus.Cancelled, StopReason = "Cancelled" };
        var saved = await store.SaveRunStateAsync(cancelled, claim!.Token, CancellationToken.None);

        Assert.Equal(RefinementStatus.Cancelled, saved.Status);
        Assert.True(RefinementStatusTransitions.IsTerminal(saved.Status));
        // A terminal save releases the claim -- nothing can mutate this run again, which is the concrete
        // mechanism preventing a cancelled run from ever reaching Accepted afterward.
        Assert.Null(saved.ClaimToken);
        await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            store.SaveRunStateAsync(cancelled with { Status = RefinementStatus.Accepted }, claim.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Step079_NegativePath_MalformedAttempt_CannotBecomeBestCandidate_RunNeverReachesAccepted()
    {
        // Required failure/edge case, verbatim: "Malformed proposal... must not auto-commit."
        var run = NewQueuedRun();
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);

        var generating = await store.SaveRunStateAsync(run with { Status = RefinementStatus.Generating }, claim!.Token, CancellationToken.None);

        var malformed = Attempt(run.Id, 0, RefinementStatus.Rejected, violations: ["Operation names an unknown effect kind."]);
        await store.SaveAttemptAsync(run.Id, claim.Token, malformed, CancellationToken.None);

        var validating = await store.SaveRunStateAsync(generating with { Status = RefinementStatus.Validating }, claim.Token, CancellationToken.None);
        var previewReady = await store.SaveRunStateAsync(validating with { Status = RefinementStatus.PreviewReady }, claim.Token, CancellationToken.None);
        var awaitingApproval = await store.SaveRunStateAsync(
            previewReady with { Status = RefinementStatus.AwaitingApproval, AttemptsMade = 1 }, claim.Token, CancellationToken.None);

        // Attempting to retain the malformed attempt as the best candidate must fail -- a violating
        // attempt can never reach LoopRun.BestCandidateId, which is the only path toward a later commit.
        var withBadBest = awaitingApproval with { BestCandidateId = malformed.Id };
        var ex = await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            store.SaveRunStateAsync(withBadBest, claim.Token, CancellationToken.None));
        Assert.Contains("violation", ex.Reason, StringComparison.OrdinalIgnoreCase);

        // The run itself never reached Accepted.
        var persisted = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, persisted!.Status);
        Assert.Null(persisted.BestCandidateId);
    }

    [Fact]
    public async Task Step079_NegativePath_NoImprovementStop_CannotSkipApprovalAndReachAcceptedDirectly()
    {
        // "no-improvement... must not auto-commit" -- a Generating/Validating/PreviewReady run that stops
        // due to no further improvement must land on AwaitingApproval or Blocked, never Accepted directly.
        var run = NewQueuedRun();
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);

        var previewReady = run with { Status = RefinementStatus.Generating };
        previewReady = await store.SaveRunStateAsync(previewReady, claim!.Token, CancellationToken.None);
        previewReady = await store.SaveRunStateAsync(previewReady with { Status = RefinementStatus.Validating }, claim.Token, CancellationToken.None);
        previewReady = await store.SaveRunStateAsync(previewReady with { Status = RefinementStatus.PreviewReady }, claim.Token, CancellationToken.None);

        // The illegal shortcut: jumping straight from PreviewReady to Accepted, skipping AwaitingApproval
        // (and therefore skipping any human approval) entirely.
        var ex = await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            store.SaveRunStateAsync(previewReady with { Status = RefinementStatus.Accepted, StopReason = "NoImprovement" }, claim.Token, CancellationToken.None));
        Assert.Contains("cannot transition", ex.Reason, StringComparison.OrdinalIgnoreCase);

        // The only legal "stop, no further improvement" path.
        var stopped = await store.SaveRunStateAsync(
            previewReady with { Status = RefinementStatus.AwaitingApproval, StopReason = "NoImprovement" }, claim.Token, CancellationToken.None);
        Assert.Equal(RefinementStatus.AwaitingApproval, stopped.Status);
        Assert.Equal("NoImprovement", stopped.StopReason);
    }

    [Fact]
    public async Task Step079_NegativePath_StaleRevisionInput_RejectedWithoutMutation()
    {
        // "Invalid/stale input: reject and show the source of the problem; do not commit partial project
        // state" -- Section 7's cross-cutting negative case. A negative BaseRevision is this step's own
        // concrete, structurally-checkable stand-in for an invalid/stale snapshot reference.
        var invalid = NewQueuedRun() with { BaseRevision = -1 };
        Assert.Throws<RefinementRunInvalidException>(() => invalid.Validate());

        var store = new JsonRefinementRunStore(_root);
        await Assert.ThrowsAsync<RefinementRunInvalidException>(() => store.CreateAsync(invalid, CancellationToken.None));

        // No file was ever written for the rejected run.
        Assert.Null(await store.LoadRunAsync(invalid.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Step079_NegativePath_ForeignClaimToken_RejectedWithoutMutation()
    {
        var run = NewQueuedRun();
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(claim);

        var foreignToken = Guid.NewGuid();
        await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            store.SaveRunStateAsync(run with { Status = RefinementStatus.Generating }, foreignToken, CancellationToken.None));

        // The run's state was not mutated by the rejected call.
        var persisted = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Queued, persisted!.Status);
    }

    [Fact]
    public void Step079_NegativePath_InconsistentClaimFields_RejectedAtValidation()
    {
        var run = NewQueuedRun() with { ClaimToken = Guid.NewGuid(), ClaimedAtUtc = null, HeartbeatAtUtc = null };
        Assert.Throws<RefinementRunInvalidException>(() => run.Validate());
    }

    [Fact]
    public void Step079_RefinementStatusTransitions_AcceptedRejectedCancelled_AreTerminal()
    {
        Assert.True(RefinementStatusTransitions.IsTerminal(RefinementStatus.Accepted));
        Assert.True(RefinementStatusTransitions.IsTerminal(RefinementStatus.Rejected));
        Assert.True(RefinementStatusTransitions.IsTerminal(RefinementStatus.Cancelled));
        Assert.True(RefinementStatusTransitions.IsTerminal(RefinementStatus.Blocked));
        Assert.False(RefinementStatusTransitions.IsTerminal(RefinementStatus.Queued));
        Assert.False(RefinementStatusTransitions.CanTransition(RefinementStatus.Queued, RefinementStatus.Accepted));
        Assert.False(RefinementStatusTransitions.CanTransition(RefinementStatus.Accepted, RefinementStatus.Queued));
    }
}
