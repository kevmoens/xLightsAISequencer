using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Refinement.Harness;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement.Tests.Harness;

/// <summary>
/// Build-plan Phase 7b: exercises the real <see cref="SequenceMetricBattery"/> against deterministic,
/// directly-constructed fixtures (no mocking) -- the same convention Step 081's
/// <see cref="GestureQualityEvaluator"/> tests already established. The required validation task
/// (build plan Phase 7b, docs/AI-CHOREOGRAPHY-ARCHITECTURE.md §9) is the group of tests whose names start
/// with <c>ExportAudit_</c>: each deliberately reproduces one real historical defect shape from
/// <c>C:\xLights\EXPORT_AUDIT.md</c> and asserts the battery actually flags it.
/// </summary>
public sealed class SequenceMetricBatteryTests
{
    private static readonly ModelId ModelA = new("Matrix-1");
    private static readonly ModelId ModelB = new("Tree-1");
    private static readonly ModelId ModelC = new("MiniStar-1");

    private static PreviewFrame Frame(long ticks, params (ModelId Model, double X, double Y, byte R, byte G, byte B)[] points) =>
        new(ticks, points.Select(p => new PixelPoint(p.Model, p.X, p.Y, p.R, p.G, p.B)).ToList(), []);

    private static BeatGrid BeatGridWith(params long[] beatTicks) => new(
        Guid.NewGuid(), new SongId(Guid.NewGuid()), "deadbeef", Guid.NewGuid(), Guid.NewGuid(), "test-analyzer.v1",
        new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: 10_000), SelectedTempo: null,
        Beats: beatTicks.Select((t, i) => new BeatMarker(Guid.NewGuid(), t, i % 4, i / 4, Confidence: 1.0, UserCorrected: false)).ToArray(),
        Candidates: [], CorrectionVersion: 0);

    [Fact]
    public void Evaluate_ChangeRateWithinStyleBand_FlagsASectionFasterThanItsStyleAllows()
    {
        // 10 composition changes across a 1-second section is a 10 changes/s rate -- far outside a
        // "Traditional / Classic"-shaped [0.1, 0.5] band (build-plan Phase 8 task 1: style density band).
        var frames = Enumerable.Range(0, 11)
            .Select(i => i % 2 == 0 ? Frame(i * 1_000_000L, (ModelA, 0.1, 0.1, 100, 0, 0)) : Frame(i * 1_000_000L, (ModelB, 0.1, 0.1, 0, 100, 0)))
            .ToArray();
        var sections = new[]
        {
            new HarnessSection(new TimeWindow(0, 10_000_000L), EnergyCeiling: 0.5, DeclaredPaletteSlotCount: 2,
                StyleMinPaletteSlots: 1, StyleMaxPaletteSlots: 3, StyleMinChangeRatePerSecond: 0.1, StyleMaxChangeRatePerSecond: 0.5),
        };
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 10, Sections: sections);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        var metric = Assert.Single(report.Metrics, m => m.Name == "ChangeRateWithinStyleBand");
        Assert.Equal(0.0, metric.Value);
    }

    [Fact]
    public void Evaluate_EmptyContext_ReportsEveryMetricAsNoData_NeverFabricatesAScore()
    {
        var context = new HarnessSequenceContext(Frames: [], TotalDisplayPointCount: 100);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        Assert.Equal(13, report.Metrics.Count);
        Assert.All(report.Metrics, m => Assert.True(m.Weight == 0.0 || m.HardGate, $"{m.Name} unexpectedly scored with no inputs."));
        Assert.All(report.Metrics.Where(m => m.Weight == 0.0), m => Assert.Equal(0.5, m.Value));
    }

    [Fact]
    public void Evaluate_WellBehavedSequence_PassesEveryHardGate()
    {
        var frames = Enumerable.Range(0, 20)
            .Select(i => Frame(i * 50, (ModelA, 0.1, 0.1, 100, 0, 0)))
            .ToArray();
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 10);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        Assert.Empty(report.Violations);
        Assert.DoesNotContain(report.Metrics, m => m.HardGate && m.Value == 0.0);
    }

    // ---- EXPORT_AUDIT.md reproduction: finding #4, "the whole show collapses into ~5 discrete pixel
    // levels, and 52% of runtime sits at exactly one value" ----
    [Fact]
    public void ExportAudit_Finding4_RepeatedPixelLevelPlateau_IsFlagged()
    {
        var frames = new List<PreviewFrame>();
        for (var i = 0; i < 100; i++)
        {
            // 60 of 100 frames light exactly 3 nodes on ModelA (the plateau); the rest vary.
            var litCount = i < 60 ? 3 : (i % 10) + 1;
            var points = Enumerable.Range(0, litCount).Select(n => (ModelA, 0.1, 0.1, (byte)100, (byte)0, (byte)0)).ToArray();
            frames.Add(Frame(i * 50, points));
        }

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), new HarnessSequenceContext(frames, TotalDisplayPointCount: 50));

        var metric = report.Metrics.Single(m => m.Name == "LargestPixelPlateau");
        Assert.True(metric.Value < 0.5, $"Expected the 60%-at-one-value plateau to score well below target; got {metric.Value}.");
    }

    // ---- EXPORT_AUDIT.md reproduction: findings #3/#6, breakdown/outro/intro going fully black outside
    // a declared blackout window ----
    [Fact]
    public void ExportAudit_Findings3And6_UndeclaredBlackFrames_FailsHardGate()
    {
        var frames = new List<PreviewFrame>();
        for (var i = 0; i < 40; i++)
        {
            frames.Add(i is >= 10 and < 30 ? Frame(i * 50) : Frame(i * 50, (ModelA, 0.1, 0.1, 100, 0, 0)));
        }

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), new HarnessSequenceContext(frames, TotalDisplayPointCount: 10));

        var metric = report.Metrics.Single(m => m.Name == "UndeclaredBlackFrames");
        Assert.Equal(0.0, metric.Value);
        Assert.True(metric.HardGate);
        Assert.Contains(report.Violations, v => v.Contains("fully-black", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeclaredBlackoutWindow_DoesNotFailTheHardGate()
    {
        var frames = Enumerable.Range(0, 10).Select(i => Frame(i * 50)).ToArray(); // all black
        var context = new HarnessSequenceContext(
            frames, TotalDisplayPointCount: 10,
            DeclaredBlackoutWindows: [new TimeWindow(0, 1000)]);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        var metric = report.Metrics.Single(m => m.Name == "UndeclaredBlackFrames");
        Assert.Equal(1.0, metric.Value);
        Assert.Empty(report.Violations);
    }

    // ---- EXPORT_AUDIT.md reproduction: finding #7, "52% of our frames show 4+ distinct hue families at
    // once (reference: 16%)... reads as scattered confetti" ----
    [Fact]
    public void ExportAudit_Finding7_TooManyHueFamiliesPerFrame_IsFlagged()
    {
        var frames = Enumerable.Range(0, 10).Select(i => Frame(
            i * 50,
            (ModelA, 0.1, 0.1, 255, 0, 0),     // red
            (ModelB, 0.2, 0.2, 0, 255, 0),     // green
            (ModelC, 0.3, 0.3, 0, 0, 255),     // blue
            (ModelA, 0.4, 0.4, 255, 255, 0))).ToArray(); // yellow -- 4 distinct hue families

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), new HarnessSequenceContext(frames, TotalDisplayPointCount: 10));

        var metric = report.Metrics.Single(m => m.Name == "MeanHueFamilies");
        Assert.True(metric.Value < 1.0, $"Expected 4 hue families/frame to score below the ideal; got {metric.Value}.");
    }

    // ---- EXPORT_AUDIT.md reproduction: finding #9, "matrices render grey/static ~20% of lit time" /
    // finding #2, "69% of pixels treated as one on/off block" -- expressed here as a matrix/tree duty
    // cycle well below the audit's own verification-table targets (Matrix >= 65%, Tree >= 40%) ----
    [Fact]
    public void ExportAudit_Findings2And9_LowPixelHeavyDutyCycle_IsFlagged()
    {
        // ModelA (Matrix) and ModelB (Tree) are each lit in only 1 of 10 frames -- far under target.
        var frames = Enumerable.Range(0, 10)
            .Select(i => i == 0 ? Frame(i * 50, (ModelA, 0.1, 0.1, 100, 0, 0), (ModelB, 0.2, 0.2, 100, 0, 0)) : Frame(i * 50, (ModelC, 0.3, 0.3, 100, 0, 0)))
            .ToArray();
        var categories = new Dictionary<ModelId, ModelCategory> { [ModelA] = ModelCategory.Matrix, [ModelB] = ModelCategory.Tree, [ModelC] = ModelCategory.Star };

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), new HarnessSequenceContext(frames, TotalDisplayPointCount: 10, CategoryByModel: categories));

        var metric = report.Metrics.Single(m => m.Name == "PixelHeavyDutyCycle");
        Assert.Equal(0.0, metric.Value);
    }

    // ---- EXPORT_AUDIT.md reproduction: finding #10, "the song's single biggest musical moment... is a
    // near pixel-for-pixel repeat of an earlier drop's composition" ----
    [Fact]
    public void ExportAudit_Finding10_RepeatedPeakComposition_IsFlaggedByPeakSimilarity()
    {
        var frames = new[]
        {
            Frame(0, (ModelA, 0.1, 0.1, 100, 0, 0), (ModelB, 0.2, 0.2, 100, 0, 0)),
            Frame(100_000, (ModelA, 0.1, 0.1, 100, 0, 0), (ModelB, 0.2, 0.2, 100, 0, 0)), // identical composition at the "second drop"
        };
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 10, PeakTimestampTicks: [0, 100_000]);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        var metric = report.Metrics.Single(m => m.Name == "PeakSimilarity");
        Assert.Equal(0.0, metric.Value);
    }

    // Also finding #10 / #12: one lit-model-set composition dominating the whole runtime.
    [Fact]
    public void ExportAudit_Finding10_RepeatedCompositionAcrossRuntime_IsFlagged()
    {
        var dominant = Frame(0, (ModelA, 0.1, 0.1, 100, 0, 0));
        var frames = Enumerable.Range(0, 100)
            .Select(i => i < 40 ? dominant with { TimeTicks = i * 50 } : Frame(i * 50, (ModelB, 0.2, 0.2, (byte)(i % 255), 0, 0)))
            .ToArray();

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), new HarnessSequenceContext(frames, TotalDisplayPointCount: 10));

        var metric = report.Metrics.Single(m => m.Name == "RepeatedCompositionShare");
        Assert.True(metric.Value < 0.5, $"Expected a 40%-of-runtime repeated composition to score well below target; got {metric.Value}.");
    }

    [Fact]
    public void SectionEnergyCeiling_Breach_FailsHardGateAndRecordsViolation()
    {
        var frames = new[] { Frame(0, (ModelA, 0.1, 0.1, 255, 255, 255)) }; // full-brightness single point
        var sections = new[] { new HarnessSection(new TimeWindow(0, 1000), EnergyCeiling: 0.05, DeclaredPaletteSlotCount: 2, StyleMinPaletteSlots: 1, StyleMaxPaletteSlots: 3) };
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 1, Sections: sections);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        var metric = report.Metrics.Single(m => m.Name == "SectionEnergyCeiling");
        Assert.Equal(0.0, metric.Value);
        Assert.True(metric.HardGate);
        Assert.Contains(report.Violations, v => v.Contains("exceeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SectionEnergyCeiling_WithinBudget_PassesHardGate()
    {
        var frames = new[] { Frame(0, (ModelA, 0.1, 0.1, 10, 0, 0)) };
        var sections = new[] { new HarnessSection(new TimeWindow(0, 1000), EnergyCeiling: 0.5, DeclaredPaletteSlotCount: 2, StyleMinPaletteSlots: 1, StyleMaxPaletteSlots: 3) };
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 1, Sections: sections);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        var metric = report.Metrics.Single(m => m.Name == "SectionEnergyCeiling");
        Assert.Equal(1.0, metric.Value);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void RmsCorrelation_StrongPositiveMatch_ScoresHigh()
    {
        var frames = Enumerable.Range(0, 20).Select(i => Frame(i * 1000, Enumerable.Range(0, i).Select(_ => (ModelA, 0.1, 0.1, (byte)255, (byte)255, (byte)255)).ToArray())).ToArray();
        var rms = Enumerable.Range(0, 20).Select(i => ((long)(i * 1000), i / 19.0)).ToArray();
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 20, RmsEnvelope: rms);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        var metric = report.Metrics.Single(m => m.Name == "RmsCorrelation");
        Assert.True(metric.Value > 0.9, $"Expected a near-perfectly-correlated curve to score near 1.0; got {metric.Value}.");
    }

    [Fact]
    public void EffectBeatDeviation_BoundariesOnBeat_ScoresHigh()
    {
        var frames = Enumerable.Range(0, 10).Select(i => Frame(i * 25)).ToArray(); // 25-tick frame spacing
        var beatGrid = BeatGridWith(0, 1000, 2000, 3000);
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 10, BeatGrid: beatGrid, EffectBoundaryTicks: [0, 1000, 2000, 3000]);

        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);

        var metric = report.Metrics.Single(m => m.Name == "EffectBeatDeviation");
        Assert.Equal(1.0, metric.Value);
    }

    [Fact]
    public void ActivePaletteSlots_OutsideStyleBand_ScoresBelowOne()
    {
        var sections = new[]
        {
            new HarnessSection(new TimeWindow(0, 1000), 0.5, DeclaredPaletteSlotCount: 6, StyleMinPaletteSlots: 1, StyleMaxPaletteSlots: 3),
            new HarnessSection(new TimeWindow(1000, 2000), 0.5, DeclaredPaletteSlotCount: 2, StyleMinPaletteSlots: 1, StyleMaxPaletteSlots: 3),
        };
        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), new HarnessSequenceContext([], TotalDisplayPointCount: 10, Sections: sections));

        var metric = report.Metrics.Single(m => m.Name == "ActivePaletteSlots");
        Assert.Equal(0.5, metric.Value);
    }

    [Fact]
    public void Report_IsReproducible_SameInputsSameReportShape()
    {
        var frames = new[] { Frame(0, (ModelA, 0.1, 0.1, 100, 0, 0)) };
        var context = new HarnessSequenceContext(frames, TotalDisplayPointCount: 10);
        var candidateId = Guid.NewGuid();

        var report1 = SequenceMetricBattery.Evaluate(candidateId, context);
        var report2 = SequenceMetricBattery.Evaluate(candidateId, context);

        Assert.Equal(report1.Metrics.Select(m => (m.Name, m.Value)), report2.Metrics.Select(m => (m.Name, m.Value)));
        Assert.Equal(SequenceMetricBattery.EvaluatorVersion, report1.EvaluatorVersion);
    }
}
