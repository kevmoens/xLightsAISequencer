using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement.Tests;

/// <summary>
/// PHASE-12 Step 081 -- "Implement quality evaluators: timing, melodic alignment, density, motif reuse,
/// legal effects." Required acceptance (verbatim): "Known good/bad fixtures produce expected scores and
/// findings." Required failure/edge case (verbatim): "Silence, invalid timing boundaries and missing media
/// must be handled explicitly." These tests exercise the real <see cref="GestureQualityEvaluator"/> against
/// deterministic, directly-constructed fixtures (no mocking) -- the same convention Steps 079/080 already
/// established.
/// </summary>
public sealed class Step081AcceptanceTests
{
    private readonly GestureQualityEvaluator _evaluator = new();

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

    [Fact]
    public async Task Step081_Acceptance_KnownGoodFixture_PassesLegalGate_WithExpectedExplanation()
    {
        // Required acceptance, verbatim: "Known good/bad fixtures produce expected scores and findings."
        var run = NewQueuedRun(Scope(models: ["Arches", "MegaTree"]));
        var gesture = Gesture(TransitionKind.Build, run.Scope.Time, 0.1, 0.9, "Arches", "MegaTree");
        var candidate = Attempt(run, 0, [gesture]);

        var report = await _evaluator.EvaluateAsync(run, candidate, ChoreographyQualityContext.Empty, CancellationToken.None);

        var legal = Assert.Single(report.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(1.0, legal.Value);
        Assert.True(legal.HardGate);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public async Task Step081_Acceptance_KnownBadFixture_OutOfScopeProp_FailsLegalGate_WithFinding()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var gesture = Gesture(TransitionKind.Build, run.Scope.Time, 0.1, 0.9, "Arches", "MegaTree");
        var candidate = Attempt(run, 0, [gesture]);

        var report = await _evaluator.EvaluateAsync(run, candidate, ChoreographyQualityContext.Empty, CancellationToken.None);

        var legal = Assert.Single(report.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(0.0, legal.Value);
        Assert.Contains("MegaTree", legal.Explanation);
        Assert.NotEmpty(report.Violations);
        Assert.Contains(report.Violations, v => v.Contains("MegaTree", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Step081_Acceptance_Density_ComputesFractionOfPermittedPropsTargeted()
    {
        var run = NewQueuedRun(Scope(models: ["Arches", "MegaTree", "Mini1", "Mini2"]));
        var gesture = Gesture(TransitionKind.Transition, run.Scope.Time, 0.5, 0.5, "Arches", "MegaTree");
        var candidate = Attempt(run, 0, [gesture]);

        var report = await _evaluator.EvaluateAsync(run, candidate, ChoreographyQualityContext.Empty, CancellationToken.None);

        var density = Assert.Single(report.Metrics, m => m.Name == "Density");
        Assert.Equal(0.5, density.Value);
        Assert.False(density.HardGate);
        Assert.Contains("2/4", density.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Step081_Acceptance_Timing_PerfectBeatSnap_ScoresHigherThanLargeOffset()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var window = new TimeWindow(1_000_000, 2_000_000);
        var beatGrid = BeatGridWith(1_000_000, 1_500_000, 2_000_000, 2_500_000);

        var snapped = Attempt(run, 0, [Gesture(TransitionKind.Transition, window, 0.5, 0.5, "Arches")]);
        var offsetWindow = new TimeWindow(1_200_000, 2_200_000);
        var offset = Attempt(run, 1, [Gesture(TransitionKind.Transition, offsetWindow, 0.5, 0.5, "Arches")]);

        var context = new ChoreographyQualityContext(BeatGrid: beatGrid);
        var snappedReport = await _evaluator.EvaluateAsync(run, snapped, context, CancellationToken.None);
        var offsetReport = await _evaluator.EvaluateAsync(run, offset, context, CancellationToken.None);

        var snappedTiming = Assert.Single(snappedReport.Metrics, m => m.Name == "Timing");
        var offsetTiming = Assert.Single(offsetReport.Metrics, m => m.Name == "Timing");
        Assert.Equal(1.0, snappedTiming.Value);
        Assert.Equal(1.0, snappedTiming.Weight);
        Assert.True(offsetTiming.Value < snappedTiming.Value);
    }

    [Fact]
    public async Task Step081_Acceptance_MelodicAlignment_RisingPitchAgreesWithBuild_ButNotRelease()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var window = new TimeWindow(0, 1_000_000);
        var melody = new MelodyTrack(
            Guid.NewGuid(),
            "deadbeef",
            [
                new NoteEvent(Guid.NewGuid(), 0, 100_000, MidiNote: 60, Velocity: 0.8, Confidence: 1.0, UserCorrected: false),
                new NoteEvent(Guid.NewGuid(), 900_000, 1_000_000, MidiNote: 72, Velocity: 0.8, Confidence: 1.0, UserCorrected: false),
            ],
            []);
        var context = new ChoreographyQualityContext(Melody: melody);

        var buildCandidate = Attempt(run, 0, [Gesture(TransitionKind.Build, window, 0.1, 0.9, "Arches")]);
        var releaseCandidate = Attempt(run, 1, [Gesture(TransitionKind.Release, window, 0.9, 0.1, "Arches")]);

        var buildReport = await _evaluator.EvaluateAsync(run, buildCandidate, context, CancellationToken.None);
        var releaseReport = await _evaluator.EvaluateAsync(run, releaseCandidate, context, CancellationToken.None);

        var buildAlignment = Assert.Single(buildReport.Metrics, m => m.Name == "MelodicAlignment");
        var releaseAlignment = Assert.Single(releaseReport.Metrics, m => m.Name == "MelodicAlignment");
        Assert.Equal(1.0, buildAlignment.Value);
        Assert.Equal(1.0, buildAlignment.Weight);
        Assert.Equal(0.0, releaseAlignment.Value);
    }

    [Fact]
    public async Task Step081_Acceptance_MotifReuse_HighestForShapeMatchingPriorAttempt()
    {
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var window = run.Scope.Time;
        var priorMatching = Attempt(run, 0, [Gesture(TransitionKind.Build, window, 0.1, 0.9, "Arches")]);
        var priorDifferent = Attempt(run, 1, [Gesture(TransitionKind.Blackout, window, 0.0, 0.0, "Arches")]);
        var context = new ChoreographyQualityContext(PriorAttempts: [priorMatching, priorDifferent]);

        var candidate = Attempt(run, 2, [Gesture(TransitionKind.Build, window, 0.12, 0.88, "Arches")]);

        var report = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);

        var motif = Assert.Single(report.Metrics, m => m.Name == "MotifReuse");
        Assert.True(motif.Value > 0.9);
        Assert.Equal(1.0, motif.Weight);
    }

    [Fact]
    public async Task Step081_NegativePath_Silence_EmptyGestures_FailsLegalGate_WithExplicitExplanation()
    {
        // Required failure/edge case, verbatim: "Silence... must be handled explicitly."
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var candidate = Attempt(run, 0, [], violations: ["Edit scope names no permitted models; a candidate requires at least one target model."]);

        var report = await _evaluator.EvaluateAsync(run, candidate, ChoreographyQualityContext.Empty, CancellationToken.None);

        var legal = Assert.Single(report.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(0.0, legal.Value);
        Assert.Contains("no gestures", legal.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upstream violation", legal.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(report.Violations);
    }

    [Fact]
    public async Task Step081_NegativePath_InvalidTimingBoundary_WindowExceedsScope_FailsLegalGate()
    {
        // Required failure/edge case, verbatim: "invalid timing boundaries... must be handled explicitly."
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var outOfBoundsWindow = new TimeWindow(0, run.Scope.Time.EndTicks + 1_000_000);
        var gesture = Gesture(TransitionKind.Build, outOfBoundsWindow, 0.1, 0.9, "Arches");
        var candidate = Attempt(run, 0, [gesture]);

        var report = await _evaluator.EvaluateAsync(run, candidate, ChoreographyQualityContext.Empty, CancellationToken.None);

        var legal = Assert.Single(report.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(0.0, legal.Value);
        Assert.Contains("exceeds the run's permitted time scope", legal.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Step081_NegativePath_MissingMedia_ReportsWeightZero_NotFabricatedScore()
    {
        // Required failure/edge case, verbatim: "missing media must be handled explicitly."
        var run = NewQueuedRun(Scope(models: ["Arches"]));
        var candidate = Attempt(run, 0, [Gesture(TransitionKind.Build, run.Scope.Time, 0.1, 0.9, "Arches")]);

        var report = await _evaluator.EvaluateAsync(run, candidate, ChoreographyQualityContext.Empty, CancellationToken.None);

        var timing = Assert.Single(report.Metrics, m => m.Name == "Timing");
        var melody = Assert.Single(report.Metrics, m => m.Name == "MelodicAlignment");
        var motif = Assert.Single(report.Metrics, m => m.Name == "MotifReuse");

        foreach (var metric in new[] { timing, melody, motif })
        {
            Assert.Equal(0.0, metric.Weight);
            Assert.Equal(0.5, metric.Value);
            Assert.False(string.IsNullOrWhiteSpace(metric.Explanation));
        }
    }

    [Fact]
    public async Task Step081_Acceptance_LockedEffectOverlap_FailsLegalGate_UnlessRespectLocksIsFalse()
    {
        var scope = Scope(models: ["Arches"]);
        var run = NewQueuedRun(scope);
        var gesture = Gesture(TransitionKind.Build, run.Scope.Time, 0.1, 0.9, "Arches");
        var candidate = Attempt(run, 0, [gesture]);
        var lockedEffect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Arches"), Layer: 0, StartTicks: 0, EndTicks: 4_000_000,
            RecipeId: "effect.on.v1", RecipeVersion: "1", Parameters: new Dictionary<string, string>(), Locked: true);
        var context = new ChoreographyQualityContext(LockedEffects: [lockedEffect]);

        var blockedReport = await _evaluator.EvaluateAsync(run, candidate, context, CancellationToken.None);
        var blockedLegal = Assert.Single(blockedReport.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(0.0, blockedLegal.Value);
        Assert.Contains("locked effect", blockedLegal.Explanation, StringComparison.OrdinalIgnoreCase);

        var bypassRun = run with { Scope = scope with { RespectLocks = false } };
        var bypassReport = await _evaluator.EvaluateAsync(bypassRun, candidate, context, CancellationToken.None);
        var bypassLegal = Assert.Single(bypassReport.Metrics, m => m.Name == "LegalEffects");
        Assert.Equal(1.0, bypassLegal.Value);
    }

    [Fact]
    public void Step081_Acceptance_EvaluatorVersionMismatch_Flagged()
    {
        // PHASE-12 Section 7 cross-cutting test #7, verbatim: "Quality metric evaluator-version mismatch flagged."
        var candidateId = Guid.NewGuid();
        var reportA = new QualityReport(candidateId, "gesture-quality.v1", [], []);
        var reportB = new QualityReport(candidateId, "gesture-quality.v2", [], []);
        var reportC = new QualityReport(candidateId, "gesture-quality.v1", [], []);

        Assert.False(QualityReport.AreComparable(reportA, reportB));
        Assert.True(QualityReport.AreComparable(reportA, reportC));
    }

    [Fact]
    public void Step081_NegativePath_QualityReportValidate_RejectsFailedHardGateWithNoViolation()
    {
        var failedHardGate = new MetricValue("LegalEffects", 0.0, Weight: 1.0, HardGate: true, "deliberately inconsistent fixture");
        var report = new QualityReport(Guid.NewGuid(), GestureQualityEvaluator.EvaluatorVersion, [failedHardGate], Violations: []);

        var ex = Assert.Throws<RefinementRunInvalidException>(report.Validate);
        Assert.Contains("failed hard gate", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

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
}
