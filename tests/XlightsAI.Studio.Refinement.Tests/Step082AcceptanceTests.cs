using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement.Tests;

/// <summary>
/// PHASE-12 Step 082 -- "Implement targeted repair strategy and preserve best-so-far candidate." Required
/// acceptance (verbatim): "Repair only addresses reported dimension and respects locks." Required
/// failure/edge case (verbatim): "Malformed proposal, no-improvement and exhausted retry budget must not
/// auto-commit." These tests exercise the real <see cref="GestureTargetedRepairStrategy"/> and
/// <see cref="BestCandidateSelector"/> against deterministic, directly-constructed fixtures (no mocking) --
/// the same convention Steps 079/080/081 already established.
/// </summary>
public sealed class Step082AcceptanceTests
{
    private readonly GestureQualityEvaluator _evaluator = new();
    private readonly GestureTargetedRepairStrategy _repair = new();

    private static EditScope Scope(bool respectLocks = true, params string[] models) =>
        new(new TimeWindow(0, 8_000_000), models.Select(m => new ModelId(m)).ToHashSet(), RespectLocks: respectLocks);

    private static LoopRun NewQueuedRun(EditScope scope, TransitionKind family = TransitionKind.Build) => new(
        Id: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        BaseRevision: 1,
        Scope: scope,
        InputHash: "hash-of-scope-and-seed",
        Seed: 42,
        MaxAttempts: 5,
        AttemptsMade: 0,
        Status: RefinementStatus.Generating,
        BestCandidateId: null,
        StopReason: null,
        ClaimToken: null,
        ClaimedAtUtc: null,
        HeartbeatAtUtc: null,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        GestureFamily: family);

    private static CandidateAttempt Attempt(
        LoopRun run,
        int attempt,
        IReadOnlyList<TransitionGesture>? gestures,
        IReadOnlyList<string>? violations = null) => new(
        Id: Guid.NewGuid(),
        RunId: run.Id,
        Attempt: attempt,
        Seed: run.Seed,
        SnapshotHash: $"hash-{attempt}",
        Status: (violations?.Count ?? 0) > 0 ? RefinementStatus.Rejected : RefinementStatus.PreviewReady,
        Violations: violations ?? [],
        RecordedAtUtc: DateTimeOffset.UtcNow,
        Gestures: gestures);

    private static TransitionGesture Gesture(
        TransitionKind kind,
        TimeWindow window,
        double start,
        double end,
        params string[] targetProps) => new(
        Guid.NewGuid(),
        kind,
        window,
        targetProps.Select(p => new ModelId(p)).ToArray(),
        start,
        end);

    private static BeatGrid BeatGridWith(params long[] beatTicks) => new(
        Guid.NewGuid(),
        new SongId(Guid.NewGuid()),
        "deadbeef",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "test-analyzer.v1",
        new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: 10_000),
        SelectedTempo: null,
        Beats: beatTicks.Select((t, i) => new BeatMarker(Guid.NewGuid(), t, i % 4, i / 4, Confidence: 1.0, UserCorrected: false)).ToArray(),
        Candidates: [],
        CorrectionVersion: 0);

    // ---- Required acceptance, verbatim: "Repair only addresses reported dimension..." ----

    [Fact]
    public async Task Step082_Acceptance_TimingRepair_OnlyChangesWindow_NotPropsOrIntensities()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var window = new TimeWindow(1_200_000, 2_200_000); // offset from beats below
        var gesture = Gesture(TransitionKind.Transition, window, 0.4, 0.4, "Arches");
        var candidate = Attempt(run, 0, [gesture]);
        var beatGrid = BeatGridWith(1_000_000, 1_500_000, 2_000_000, 2_500_000);
        var context = new ChoreographyQualityContext(BeatGrid: beatGrid);

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal("Timing", outcome.TargetedMetricName);
        var repairedGesture = Assert.Single(outcome.Repaired.Gestures!);
        Assert.NotEqual(gesture.Window, repairedGesture.Window);
        Assert.Equal(gesture.TargetProps, repairedGesture.TargetProps);
        Assert.Equal(gesture.StartIntensity, repairedGesture.StartIntensity);
        Assert.Equal(gesture.EndIntensity, repairedGesture.EndIntensity);

        var repairedReport = await _evaluator.EvaluateAsync(run, outcome.Repaired, context, CancellationToken.None);
        var originalTiming = Assert.Single(report.Metrics, m => m.Name == "Timing");
        var repairedTiming = Assert.Single(repairedReport.Metrics, m => m.Name == "Timing");
        Assert.True(repairedTiming.Value > originalTiming.Value);
    }

    [Fact]
    public async Task Step082_Acceptance_DensityRepair_OnlyChangesTargetProps_NotWindowOrIntensities()
    {
        var run = NewQueuedRun(Scope(models: ["Arches", "MegaTree", "Mini1", "Mini2"]));
        var gesture = Gesture(TransitionKind.Transition, run.Scope.Time, 0.5, 0.5, "Arches", "MegaTree", "Mini1", "Mini2");
        var candidate = Attempt(run, 0, [gesture]);
        var context = ChoreographyQualityContext.Empty;

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var density = Assert.Single(report.Metrics, m => m.Name == "Density");
        Assert.Equal(1.0, density.Value); // over-busy: all 4 permitted props targeted

        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal("Density", outcome.TargetedMetricName);
        var repairedGesture = Assert.Single(outcome.Repaired.Gestures!);
        Assert.Equal(gesture.TargetProps.Count - 1, repairedGesture.TargetProps.Count);
        Assert.True(repairedGesture.TargetProps.All(gesture.TargetProps.Contains));
        Assert.Equal(gesture.Window, repairedGesture.Window);
        Assert.Equal(gesture.StartIntensity, repairedGesture.StartIntensity);
        Assert.Equal(gesture.EndIntensity, repairedGesture.EndIntensity);

        var repairedReport = await _evaluator.EvaluateAsync(run, outcome.Repaired, context, CancellationToken.None);
        var repairedDensity = Assert.Single(repairedReport.Metrics, m => m.Name == "Density");
        Assert.True(repairedDensity.Value < density.Value);
    }

    [Fact]
    public async Task Step082_Acceptance_MelodicAlignmentRepair_OnlySwapsIntensities_NotWindowOrProps()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var window = new TimeWindow(0, 1_000_000);
        var melody = new XlightsAI.Studio.Melody.MelodyTrack(
            Guid.NewGuid(),
            "deadbeef",
            [
                new XlightsAI.Studio.Melody.NoteEvent(Guid.NewGuid(), 0, 100_000, MidiNote: 60, Velocity: 0.8, Confidence: 1.0, UserCorrected: false),
                new XlightsAI.Studio.Melody.NoteEvent(Guid.NewGuid(), 900_000, 1_000_000, MidiNote: 72, Velocity: 0.8, Confidence: 1.0, UserCorrected: false),
            ],
            []);
        var context = new ChoreographyQualityContext(Melody: melody);

        // Release gesture (falling) disagrees with the rising melody above -- MelodicAlignment scores 0.
        var gesture = Gesture(TransitionKind.Release, window, 0.9, 0.1, "Arches");
        var candidate = Attempt(run, 0, [gesture]);

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var alignment = Assert.Single(report.Metrics, m => m.Name == "MelodicAlignment");
        Assert.Equal(0.0, alignment.Value);

        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal("MelodicAlignment", outcome.TargetedMetricName);
        var repairedGesture = Assert.Single(outcome.Repaired.Gestures!);
        Assert.Equal(gesture.EndIntensity, repairedGesture.StartIntensity);
        Assert.Equal(gesture.StartIntensity, repairedGesture.EndIntensity);
        Assert.Equal(gesture.Window, repairedGesture.Window);
        Assert.Equal(gesture.TargetProps, repairedGesture.TargetProps);

        var repairedReport = await _evaluator.EvaluateAsync(run, outcome.Repaired, context, CancellationToken.None);
        var repairedAlignment = Assert.Single(repairedReport.Metrics, m => m.Name == "MelodicAlignment");
        Assert.Equal(1.0, repairedAlignment.Value);
    }

    [Fact]
    public async Task Step082_Acceptance_RepairTargetsTheSingleWeakestWeightedMetric()
    {
        // Timing is intentionally made the single weakest weighted metric (large beat offset); Density and
        // MelodicAlignment are left perfect, so only Timing should be addressed.
        var run = NewQueuedRun(Scope(models: ["Arches", "MegaTree"]));
        var offsetWindow = new TimeWindow(1_200_000, 2_200_000);
        var beatGrid = BeatGridWith(1_000_000, 1_500_000, 2_000_000, 2_500_000);
        var gesture = Gesture(TransitionKind.Transition, offsetWindow, 0.5, 0.5, "Arches", "MegaTree");
        var candidate = Attempt(run, 0, [gesture]);
        var context = new ChoreographyQualityContext(BeatGrid: beatGrid);

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal("Timing", outcome.TargetedMetricName);
    }

    // ---- Required acceptance, verbatim: "...and respects locks." ----

    [Fact]
    public async Task Step082_Acceptance_LegalEffectsRepair_DropsLockedOverlappingProp_RespectsLocks()
    {
        var run = NewQueuedRun(Scope(models: ["Arches", "MegaTree"]));
        var gesture = Gesture(TransitionKind.Build, run.Scope.Time, 0.1, 0.9, "Arches", "MegaTree");
        var candidate = Attempt(run, 0, [gesture]);
        var lockedEffect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Arches"), Layer: 0, StartTicks: 0, EndTicks: run.Scope.Time.EndTicks,
            RecipeId: "effect.on.v1", RecipeVersion: "1", Parameters: new Dictionary<string, string>(), Locked: true);
        var context = new ChoreographyQualityContext(LockedEffects: [lockedEffect]);

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var legal = Assert.Single(report.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(0.0, legal.Value);

        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal("LegalEffects", outcome.TargetedMetricName);
        var repairedGesture = Assert.Single(outcome.Repaired.Gestures!);
        Assert.DoesNotContain(new ModelId("Arches"), repairedGesture.TargetProps);
        Assert.Contains(new ModelId("MegaTree"), repairedGesture.TargetProps);
        Assert.Empty(outcome.Repaired.Violations);

        var repairedReport = await _evaluator.EvaluateAsync(run, outcome.Repaired, context, CancellationToken.None);
        var repairedLegal = Assert.Single(repairedReport.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(1.0, repairedLegal.Value);
    }

    [Fact]
    public async Task Step082_Acceptance_LegalEffectsRepair_ClipsOutOfScopeWindow()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var outOfBoundsWindow = new TimeWindow(0, run.Scope.Time.EndTicks + 1_000_000);
        var gesture = Gesture(TransitionKind.Build, outOfBoundsWindow, 0.1, 0.9, "Arches");
        var candidate = Attempt(run, 0, [gesture]);
        var context = ChoreographyQualityContext.Empty;

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal("LegalEffects", outcome.TargetedMetricName);
        var repairedGesture = Assert.Single(outcome.Repaired.Gestures!);
        Assert.Equal(run.Scope.Time, repairedGesture.Window);
        Assert.Empty(outcome.Repaired.Violations);
    }

    [Fact]
    public async Task Step082_Acceptance_LegalEffectsRepair_RespectLocksFalse_NeverDropsTheProp()
    {
        var run = NewQueuedRun(Scope(respectLocks: false, models: ["Arches"]));
        var gesture = Gesture(TransitionKind.Build, run.Scope.Time, 0.1, 0.9, "Arches");
        var candidate = Attempt(run, 0, [gesture]);
        var lockedEffect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Arches"), Layer: 0, StartTicks: 0, EndTicks: run.Scope.Time.EndTicks,
            RecipeId: "effect.on.v1", RecipeVersion: "1", Parameters: new Dictionary<string, string>(), Locked: true);
        var context = new ChoreographyQualityContext(LockedEffects: [lockedEffect]);

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        // LegalEffects already passes when RespectLocks is false -- nothing for this gate to repair;
        // density/timing/melody are all no-data, so no repair should be possible at all.
        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Null(outcome.TargetedMetricName);
        Assert.Same(candidate, outcome.Repaired);
    }

    // ---- Required failure/edge case, verbatim: "Malformed proposal... must not auto-commit." ----

    [Fact]
    public async Task Step082_NegativePath_MalformedProposal_EmptyGestures_ReturnedUnchanged_NeverFabricatesContent()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var candidate = Attempt(run, 0, [], violations: ["Edit scope names no permitted models; a candidate requires at least one target model."]);
        var context = ChoreographyQualityContext.Empty;

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Null(outcome.TargetedMetricName);
        Assert.Same(candidate, outcome.Repaired);
        Assert.NotEmpty(outcome.Repaired.Violations);
    }

    [Fact]
    public async Task Step082_NegativePath_LegalEffectsRepair_EverythingLockedOut_StaysRejected_NeverAutoCommits()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var gesture = Gesture(TransitionKind.Build, run.Scope.Time, 0.1, 0.9, "Arches");
        var candidate = Attempt(run, 0, [gesture]);
        var lockedEffect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Arches"), Layer: 0, StartTicks: run.Scope.Time.StartTicks, EndTicks: run.Scope.Time.EndTicks,
            RecipeId: "effect.on.v1", RecipeVersion: "1", Parameters: new Dictionary<string, string>(), Locked: true);
        var context = new ChoreographyQualityContext(LockedEffects: [lockedEffect]);

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var outcome = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal("LegalEffects", outcome.TargetedMetricName);
        Assert.Empty(outcome.Repaired.Gestures!);
        Assert.NotEmpty(outcome.Repaired.Violations);
        Assert.Equal(RefinementStatus.Rejected, outcome.Repaired.Status);
    }

    // ---- Required failure/edge case, verbatim: "no-improvement... must not auto-commit" (best-so-far preservation) ----

    [Fact]
    public async Task Step082_NegativePath_NoImprovement_BestSoFarIsPreserved_NeverCorruptedByWorseLaterAttempt()
    {
        var run = NewQueuedRun(Scope(models: ["Arches", "MegaTree"]));
        var context = ChoreographyQualityContext.Empty;

        var goodGesture = Gesture(TransitionKind.Transition, run.Scope.Time, 0.5, 0.5, "Arches", "MegaTree");
        var goodCandidate = Attempt(run, 0, [goodGesture]);
        var goodReport = await _evaluator.EvaluateAsync(run, goodCandidate, context, CancellationToken.None);

        var worseGesture = Gesture(TransitionKind.Transition, run.Scope.Time, 0.5, 0.5, "Arches");
        var worseCandidate = Attempt(run, 1, [worseGesture]);
        var worseReport = await _evaluator.EvaluateAsync(run, worseCandidate, context, CancellationToken.None);

        Assert.True(BestCandidateSelector.AggregateScore(worseReport) < BestCandidateSelector.AggregateScore(goodReport));

        var best = BestCandidateSelector.SelectBest(null, goodCandidate, goodReport);
        Assert.Equal(goodCandidate.Id, best!.Value.Attempt.Id);

        var afterWorseAttempt = BestCandidateSelector.SelectBest(best, worseCandidate, worseReport);

        Assert.Equal(goodCandidate.Id, afterWorseAttempt!.Value.Attempt.Id);
        Assert.Same(goodCandidate, afterWorseAttempt.Value.Attempt);
        Assert.Same(goodReport, afterWorseAttempt.Value.Report);
    }

    [Fact]
    public async Task Step082_NegativePath_BestSoFar_NeverPromotesAViolatingCandidate_EvenIfScoreWouldBeHigher()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var legalGesture = Gesture(TransitionKind.Transition, run.Scope.Time, 0.5, 0.5, "Arches");
        var legalCandidate = Attempt(run, 0, [legalGesture]);
        var legalReport = await _evaluator.EvaluateAsync(run, legalCandidate, ChoreographyQualityContext.Empty, CancellationToken.None);
        var best = BestCandidateSelector.SelectBest(null, legalCandidate, legalReport);

        var outOfScopeGesture = Gesture(TransitionKind.Transition, run.Scope.Time, 0.5, 0.5, "Arches", "MegaTree");
        var violatingCandidate = Attempt(run, 1, [outOfScopeGesture]);
        var violatingReport = await _evaluator.EvaluateAsync(run, violatingCandidate, ChoreographyQualityContext.Empty, CancellationToken.None);
        Assert.NotEmpty(violatingReport.Violations);

        var afterViolatingAttempt = BestCandidateSelector.SelectBest(best, violatingCandidate, violatingReport);

        Assert.Equal(legalCandidate.Id, afterViolatingAttempt!.Value.Attempt.Id);
    }

    [Fact]
    public void Step082_NegativePath_BestSoFar_EvaluatorVersionMismatch_PreservesExistingBest()
    {
        var candidateIdA = Guid.NewGuid();
        var attemptA = new CandidateAttempt(candidateIdA, Guid.NewGuid(), 0, 1, "hash-a", RefinementStatus.PreviewReady, [], DateTimeOffset.UtcNow);
        var reportA = new QualityReport(candidateIdA, "gesture-quality.v1", [new MetricValue("Density", 0.2, 1.0, false, "low")], []);
        var best = BestCandidateSelector.SelectBest(null, attemptA, reportA);

        var candidateIdB = Guid.NewGuid();
        var attemptB = new CandidateAttempt(candidateIdB, Guid.NewGuid(), 1, 2, "hash-b", RefinementStatus.PreviewReady, [], DateTimeOffset.UtcNow);
        var reportB = new QualityReport(candidateIdB, "gesture-quality.v2", [new MetricValue("Density", 0.9, 1.0, false, "high")], []);

        var result = BestCandidateSelector.SelectBest(best, attemptB, reportB);

        Assert.Equal(candidateIdA, result!.Value.Attempt.Id);
    }

    // ---- Reproducibility (PHASE-12 Section 7 cross-cutting test #1, applied to repair output) ----

    [Fact]
    public async Task Step082_Acceptance_Reproducibility_SameInputsYieldIdenticalRepairedAttempt()
    {
        var run = NewQueuedRun(Scope(models: ["Arches", "MegaTree", "Mini1"]));
        var gesture = Gesture(TransitionKind.Transition, run.Scope.Time, 0.5, 0.5, "Arches", "MegaTree", "Mini1");
        var candidate = Attempt(run, 0, [gesture]);
        var context = ChoreographyQualityContext.Empty;
        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);

        var first = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);
        var second = await _repair.RepairAsync(run, candidate, report, context, CancellationToken.None);

        Assert.Equal(first.Repaired.Id, second.Repaired.Id);
        Assert.Equal(first.Repaired.SnapshotHash, second.Repaired.SnapshotHash);
        Assert.Equal(
            first.Repaired.Gestures!.Select(g => (g.Kind, g.Window, Props: string.Join(',', g.TargetProps.Select(p => p.Value)), g.StartIntensity, g.EndIntensity)),
            second.Repaired.Gestures!.Select(g => (g.Kind, g.Window, Props: string.Join(',', g.TargetProps.Select(p => p.Value)), g.StartIntensity, g.EndIntensity)));
    }
}
