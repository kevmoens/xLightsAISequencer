using XlightsAI.Studio.Refinement.Harness;

namespace XlightsAI.Studio.Refinement.Tests.Harness;

public sealed class QualityReportDiffTests
{
    /// <summary>
    /// Build-plan Phase 7d's own required acceptance criterion: "the convergence-discipline diff check
    /// actually catches at least one regression during testing." Reproduces the real incident
    /// <c>C:\xLights\EXPORT_AUDIT.md</c> documents verbatim: "the pass that fixed the black intro and the
    /// dead breakdown simultaneously made 8 of 11 loud sections dimmer." Iteration N+1's targeted metric
    /// (undeclared black frames) improves from failing to passing -- a revision loop looking only at
    /// targets would call this a clean win -- while an untouched metric (pixel-heavy prop duty cycle,
    /// standing in for "loud section brightness") quietly regresses. The diff must surface the regression
    /// even though the targeted metric improved.
    /// </summary>
    [Fact]
    public void Compare_TargetedMetricImprovesButUnrelatedMetricRegresses_SurfacesTheRegression()
    {
        var candidateId = Guid.NewGuid();

        var iterationN = new QualityReport(candidateId, "v1",
            [
                new MetricValue("UndeclaredBlackFrames", 0.0, 1.0, HardGate: true, "3 undeclared black frames in the breakdown."),
                new MetricValue("PixelHeavyDutyCycle", 0.82, 1.0, HardGate: false, "Matrix/Tree duty cycle well above target."),
                new MetricValue("MeanHueFamilies", 0.70, 1.0, HardGate: false, "Hue families within target."),
            ],
            ["UndeclaredBlackFrames: 3 undeclared black frames in the breakdown."]);

        // The revision targeted the black-frame hard gate and fixed it -- but doing so (per the real
        // incident) dimmed 8 of 11 loud sections, measured here as PixelHeavyDutyCycle dropping sharply.
        // MeanHueFamilies is untouched, included to prove the diff does not flag metrics that did not move.
        var iterationNPlus1 = new QualityReport(candidateId, "v1",
            [
                new MetricValue("UndeclaredBlackFrames", 1.0, 1.0, HardGate: true, "No undeclared black frames."),
                new MetricValue("PixelHeavyDutyCycle", 0.31, 1.0, HardGate: false, "Matrix/Tree duty cycle dropped well below target."),
                new MetricValue("MeanHueFamilies", 0.70, 1.0, HardGate: false, "Hue families within target."),
            ],
            []);

        var diff = QualityReportDiff.Compare(iterationN, iterationNPlus1);

        var regressions = diff.Regressions;
        var regression = Assert.Single(regressions);
        Assert.Equal("PixelHeavyDutyCycle", regression.MetricName);
        Assert.Equal(0.82, regression.PreviousValue);
        Assert.Equal(0.31, regression.CurrentValue);

        // The targeted, now-passing hard gate must NOT itself be reported as a regression.
        Assert.DoesNotContain(regressions, r => r.MetricName == "UndeclaredBlackFrames");
        // An unchanged metric must not be flagged either.
        Assert.DoesNotContain(regressions, r => r.MetricName == "MeanHueFamilies");
    }

    [Fact]
    public void Compare_SmallFluctuationBelowThreshold_IsNotARegression()
    {
        var id = Guid.NewGuid();
        var previous = new QualityReport(id, "v1", [new MetricValue("MeanHueFamilies", 0.70, 1.0, false, "ok")], []);
        var current = new QualityReport(id, "v1", [new MetricValue("MeanHueFamilies", 0.68, 1.0, false, "ok")], []);

        Assert.Empty(QualityReportDiff.Compare(previous, current).Regressions);
    }

    [Fact]
    public void Compare_ImprovedMetric_IsNotARegression()
    {
        var id = Guid.NewGuid();
        var previous = new QualityReport(id, "v1", [new MetricValue("MeanHueFamilies", 0.40, 1.0, false, "ok")], []);
        var current = new QualityReport(id, "v1", [new MetricValue("MeanHueFamilies", 0.90, 1.0, false, "ok")], []);

        Assert.Empty(QualityReportDiff.Compare(previous, current).Regressions);
    }

    [Fact]
    public void Compare_DifferentEvaluatorVersions_StillDiffsButFlagsVersionChange()
    {
        var id = Guid.NewGuid();
        var previous = new QualityReport(id, "v1", [new MetricValue("MeanHueFamilies", 0.90, 1.0, false, "ok")], []);
        var current = new QualityReport(id, "v2", [new MetricValue("MeanHueFamilies", 0.10, 1.0, false, "ok")], []);

        var diff = QualityReportDiff.Compare(previous, current);

        Assert.True(diff.EvaluatorVersionChanged);
        Assert.Single(diff.Regressions);
    }
}
