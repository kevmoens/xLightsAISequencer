using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Refinement.Tests;

/// <summary>
/// PHASE-12 Step 083 -- "Add retry, time, compute and quality thresholds; cancellation." Required
/// acceptance (verbatim): "Unproductive loop stops deterministically with reason." Required failure/edge
/// case (verbatim): "Silence, invalid timing boundaries and missing media must be handled explicitly."
/// These tests exercise the real <see cref="GestureRefinementOrchestrator"/>, <see cref="JsonRefinementRunStore"/>,
/// <see cref="DeterministicGestureCandidateGenerator"/>, <see cref="GestureTargetedRepairStrategy"/> and
/// <see cref="GestureQualityEvaluator"/> together against deterministic, directly-constructed fixtures (no
/// mocking) -- the same convention Steps 079-082 already established.
/// </summary>
public sealed class Step083AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step083-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static EditScope Scope(params string[] models) =>
        new(new TimeWindow(0, 8_000_000), models.Select(m => new ModelId(m)).ToHashSet());

    private static LoopRun NewQueuedRun(
        EditScope scope,
        TransitionKind family = TransitionKind.Transition,
        int maxAttempts = 50,
        RefinementBudget? budget = null) => new(
        Id: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
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
        GestureFamily: family,
        Budget: budget);

    private static GestureRefinementOrchestrator NewOrchestrator(IRefinementRunStore store) => new(
        store,
        new DeterministicGestureCandidateGenerator(),
        new GestureTargetedRepairStrategy(),
        new GestureQualityEvaluator(),
        ChoreographyQualityContext.Empty);

    // ---- Required acceptance, verbatim: "Unproductive loop stops deterministically with reason." ----

    [Fact]
    public async Task Step083_Acceptance_RetryThreshold_StopsAtExactMaxAttempts_WithExhaustedReason()
    {
        var run = NewQueuedRun(Scope("Arches", "MegaTree"), maxAttempts: 4);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(4, finished.AttemptsMade);
        Assert.StartsWith("Exhausted", finished.StopReason);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
        Assert.NotNull(finished.BestCandidateId);

        var attempts = await store.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Equal(4, attempts.Count);

        // Running the exact same deterministic inputs again (fresh run, same seed/scope/family) reaches the
        // exact same attempt count and the exact same stop reason -- "deterministically."
        var secondRun = NewQueuedRun(Scope("Arches", "MegaTree"), maxAttempts: 4) with { Id = Guid.NewGuid(), Seed = run.Seed };
        await store.CreateAsync(secondRun, CancellationToken.None);
        var secondFinished = await orchestrator.ExecuteAsync(secondRun.Id, CancellationToken.None);
        Assert.Equal(finished.AttemptsMade, secondFinished.AttemptsMade);
        Assert.Equal(finished.StopReason, secondFinished.StopReason);
    }

    [Fact]
    public async Task Step083_Acceptance_ComputeThreshold_StopsBeforeMaxAttempts_WithComputeBudgetExceededReason()
    {
        var budget = new RefinementBudget(MaxComputeUnits: 2.0);
        var run = NewQueuedRun(Scope("Arches", "MegaTree", "Mini1"), maxAttempts: 50, budget: budget);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(2, finished.AttemptsMade);
        Assert.Equal(2.0, finished.ComputeUnitsSpent);
        Assert.StartsWith("ComputeBudgetExceeded", finished.StopReason);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
        Assert.True(finished.AttemptsMade < finished.MaxAttempts);
    }

    [Fact]
    public async Task Step083_Acceptance_TimeThreshold_StopsImmediately_WithTimeBudgetExceededReason()
    {
        // A budget that is already exceeded the instant the run starts (MaxElapsedDuration smaller than any
        // real wall-clock gap between StartedAtUtc being set and the very next check) proves the time
        // threshold is actually enforced, deterministically, without relying on a flaky real-time sleep.
        var budget = new RefinementBudget(MaxElapsedDuration: TimeSpan.FromTicks(1));
        var run = NewQueuedRun(Scope("Arches"), maxAttempts: 50, budget: budget);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(1, finished.AttemptsMade); // StartedAtUtc is only set on the first Generating transition, so attempt 0 always runs once before the clock can be checked against a near-zero budget.
        Assert.StartsWith("TimeBudgetExceeded", finished.StopReason);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
    }

    [Fact]
    public async Task Step083_Acceptance_QualityThreshold_StopsEarly_WithQualityTargetReachedReason()
    {
        // Density reaches 1.0 (every permitted prop targeted) on the very first generated candidate when
        // the run's permitted scope has exactly the props DeterministicGestureCandidateGenerator always
        // targets in full -- every other soft metric reports Weight=0 (no BeatGrid/Melody/PriorAttempts
        // supplied), so the aggregate score is driven entirely by LegalEffects (1.0) and Density (1.0).
        var budget = new RefinementBudget(QualityTarget: 0.99);
        var run = NewQueuedRun(Scope("Arches"), maxAttempts: 50, budget: budget);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(1, finished.AttemptsMade);
        Assert.StartsWith("QualityTargetReached", finished.StopReason);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
    }

    [Fact]
    public async Task Step083_Acceptance_NoImprovementPatience_StopsBeforeMaxAttempts_WithNoImprovementPatienceExceededReason()
    {
        // The run's own permitted scope is exactly the props the generator always fully targets, so the
        // very first attempt already reaches the maximum Density/LegalEffects score; the repair strategy then
        // reports nothing left to target (every weighted metric is already perfect), so every subsequent
        // attempt is a fresh seeded variant that can never score any higher -- never improving the retained
        // best again after attempt 0.
        var budget = new RefinementBudget(NoImprovementPatience: 3);
        var run = NewQueuedRun(Scope("Arches", "MegaTree"), maxAttempts: 50, budget: budget);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(3, finished.ConsecutiveNoImprovementCount);
        Assert.StartsWith("NoImprovementPatienceExceeded", finished.StopReason);
        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
        Assert.True(finished.AttemptsMade < finished.MaxAttempts);
    }

    [Fact]
    public async Task Step083_Acceptance_Cancellation_RequestedBeforeExecute_StopsImmediately_RunsZeroAttempts()
    {
        // PHASE-12 Section 7 cross-cutting test #5: "Cancelled run never commits" -- applied to the
        // orchestrator: a cancellation requested before the run ever starts executing must stop it
        // immediately, with zero attempts recorded.
        var run = NewQueuedRun(Scope("Arches"));
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        await orchestrator.CancelAsync(run.Id, CancellationToken.None);
        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(0, finished.AttemptsMade);
        Assert.Equal(RefinementStatus.Cancelled, finished.Status);
        Assert.Equal("Cancelled: cancellation was requested.", finished.StopReason);
        Assert.True(RefinementStatusTransitions.IsTerminal(finished.Status));
        Assert.Null(finished.ClaimToken);

        var attempts = await store.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Empty(attempts);
    }

    /// <summary>
    /// A deterministic test double (not a mocking framework) that requests cancellation -- via
    /// <see cref="IRefinementRunStore.RequestCancellationAsync"/>, the exact same store-level path
    /// <see cref="IRefinementOrchestrator.CancelAsync"/> itself uses -- immediately after the very first
    /// real attempt it generates, before returning it. This models a caller distinct from whoever holds the
    /// run's claim (e.g. a UI "Cancel" action) requesting cancellation partway through the loop, without
    /// depending on any real-time race between a background task and a polling test thread.
    /// </summary>
    private sealed class CancelAfterFirstGenerationGenerator(ISeededCandidateGenerator inner, IRefinementRunStore store, Guid runId) : ISeededCandidateGenerator
    {
        public async Task<CandidateAttempt> GenerateAsync(LoopRun run, CandidateAttempt? prior, CancellationToken ct)
        {
            var attempt = await inner.GenerateAsync(run, prior, ct);
            await store.RequestCancellationAsync(runId, ct);
            return attempt;
        }
    }

    [Fact]
    public async Task Step083_Acceptance_Cancellation_RequestedMidLoop_StopsBeforeMaxAttempts_PreservesPriorAttempts()
    {
        var run = NewQueuedRun(Scope("Arches", "MegaTree"), maxAttempts: 50);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);

        var generator = new CancelAfterFirstGenerationGenerator(new DeterministicGestureCandidateGenerator(), store, run.Id);
        var orchestrator = new GestureRefinementOrchestrator(
            store, generator, new GestureTargetedRepairStrategy(), new GestureQualityEvaluator(), ChoreographyQualityContext.Empty);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(RefinementStatus.Cancelled, finished.Status);
        Assert.Equal("Cancelled: cancellation was requested.", finished.StopReason);
        Assert.Equal(1, finished.AttemptsMade);
        Assert.True(finished.AttemptsMade < finished.MaxAttempts);
        Assert.True(RefinementStatusTransitions.IsTerminal(finished.Status));
        Assert.Null(finished.ClaimToken);

        // The one attempt recorded before cancellation is preserved, not rolled back.
        var finalAttempts = await store.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Single(finalAttempts);
    }

    // ---- Required failure/edge case, verbatim (restated from Steps 081/082): missing-prerequisite handling ----

    [Fact]
    public async Task Step083_NegativePath_MissingGestureFamily_BlocksImmediately_NeverCommits()
    {
        // LoopRun.GestureFamily is a generation-time precondition the generator itself enforces
        // (DeterministicGestureCandidateGenerator throws RefinementRunInvalidException when unset) --
        // PHASE-12 Section 3's own named stop criterion, verbatim: "missing prerequisite."
        var run = NewQueuedRun(Scope("Arches")) with { GestureFamily = null };
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(RefinementStatus.Blocked, finished.Status);
        Assert.StartsWith("MissingPrerequisite", finished.StopReason);
        Assert.Equal(0, finished.AttemptsMade);
        Assert.Null(finished.BestCandidateId);
        Assert.True(RefinementStatusTransitions.IsTerminal(finished.Status));

        var attempts = await store.LoadAttemptsAsync(run.Id, CancellationToken.None);
        Assert.Empty(attempts);
    }

    // ---- Required failure/edge case, verbatim: "exhausted retry budget... must not auto-commit" (explicitly deferred by Step 082) ----

    [Fact]
    public async Task Step083_NegativePath_ExhaustedBudget_NeverReachesAccepted_OnlyAwaitingApproval()
    {
        var run = NewQueuedRun(Scope("Arches", "MegaTree"), maxAttempts: 2);
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var orchestrator = NewOrchestrator(store);

        var finished = await orchestrator.ExecuteAsync(run.Id, CancellationToken.None);

        Assert.Equal(RefinementStatus.AwaitingApproval, finished.Status);
        Assert.NotEqual(RefinementStatus.Accepted, finished.Status);

        // The only way to reach Accepted from here is a distinct, later, human-approval save -- the
        // orchestrator itself never performs it, and an attempted illegal shortcut from a live attempt
        // status would be rejected by the store (already proven by Step079AcceptanceTests); here we confirm
        // the orchestrator's own returned run is sitting at the one legal pre-Accepted state.
        Assert.True(RefinementStatusTransitions.CanTransition(finished.Status, RefinementStatus.Accepted));
        Assert.False(RefinementStatusTransitions.IsTerminal(finished.Status));

        // AwaitingApproval is not terminal, so the orchestrator must have explicitly released the claim for
        // a later human-approval action to be able to claim it again.
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.NotNull(claim);
    }

    // ---- RefinementStopEvaluator / RefinementBudget: direct pure-function tests ----

    [Fact]
    public void Step083_RefinementStopEvaluator_NoThresholdHitYet_ReturnsNull()
    {
        var run = NewQueuedRun(Scope("Arches"), maxAttempts: 10);
        Assert.Null(RefinementStopEvaluator.Evaluate(run, TimeSpan.Zero, bestAggregateScore: null));
    }

    [Fact]
    public void Step083_RefinementStopEvaluator_Cancellation_TakesPriorityOverEveryOtherReason()
    {
        var budget = new RefinementBudget(MaxComputeUnits: 100.0, QualityTarget: 1.0, NoImprovementPatience: 100);
        var run = NewQueuedRun(Scope("Arches"), maxAttempts: 1, budget: budget) with
        {
            CancellationRequested = true,
            AttemptsMade = 1, // also independently exhausted
        };

        Assert.Equal(RefinementStopReason.Cancelled, RefinementStopEvaluator.Evaluate(run, TimeSpan.Zero, bestAggregateScore: 1.0));
    }

    [Fact]
    public void Step083_RefinementBudget_NegativePath_InvalidFieldsRejectedAtValidation()
    {
        Assert.Throws<RefinementRunInvalidException>(() => new RefinementBudget(MaxElapsedDuration: TimeSpan.Zero).Validate());
        Assert.Throws<RefinementRunInvalidException>(() => new RefinementBudget(MaxComputeUnits: 0.0).Validate());
        Assert.Throws<RefinementRunInvalidException>(() => new RefinementBudget(QualityTarget: 1.5).Validate());
        Assert.Throws<RefinementRunInvalidException>(() => new RefinementBudget(QualityTarget: -0.1).Validate());
        Assert.Throws<RefinementRunInvalidException>(() => new RefinementBudget(NoImprovementPatience: 0).Validate());

        // A LoopRun carrying an invalid Budget is itself rejected -- the budget is validated transitively.
        var invalidRun = NewQueuedRun(Scope("Arches"), budget: new RefinementBudget(MaxComputeUnits: -1.0));
        Assert.Throws<RefinementRunInvalidException>(() => invalidRun.Validate());
    }

    [Fact]
    public async Task Step083_NegativePath_RequestCancellation_UnknownRun_Throws()
    {
        var store = new JsonRefinementRunStore(_root);
        await Assert.ThrowsAsync<RefinementRunInvalidException>(() =>
            store.RequestCancellationAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Step083_NegativePath_RequestCancellation_AlreadyTerminalRun_IsANoOp()
    {
        var run = NewQueuedRun(Scope("Arches"));
        var store = new JsonRefinementRunStore(_root);
        await store.CreateAsync(run, CancellationToken.None);
        var claim = await store.TryClaimAsync(run.Id, TimeSpan.FromMinutes(5), CancellationToken.None);
        await store.SaveRunStateAsync(run with { Status = RefinementStatus.Cancelled, StopReason = "Cancelled" }, claim!.Token, CancellationToken.None);

        // Requesting cancellation on an already-terminal run must not throw and must not resurrect it.
        await store.RequestCancellationAsync(run.Id, CancellationToken.None);

        var persisted = await store.LoadRunAsync(run.Id, CancellationToken.None);
        Assert.Equal(RefinementStatus.Cancelled, persisted!.Status);
    }
}
