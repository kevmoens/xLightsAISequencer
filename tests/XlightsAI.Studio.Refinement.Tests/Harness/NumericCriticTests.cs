using XlightsAI.Studio.Refinement.Harness;

namespace XlightsAI.Studio.Refinement.Tests.Harness;

public sealed class NumericCriticTests
{
    [Fact]
    public void Critique_FailedHardGate_ProducesBlockingFinding()
    {
        var report = new QualityReport(Guid.NewGuid(), "v1",
            [new MetricValue("UndeclaredBlackFrames", 0.0, 1.0, HardGate: true, "2 undeclared black frame(s) found.")],
            ["UndeclaredBlackFrames: 2 undeclared black frame(s) found."]);

        var findings = NumericCritic.Critique(report);

        var finding = Assert.Single(findings);
        Assert.Equal("UndeclaredBlackFrames", finding.MetricName);
        Assert.Equal(CritiqueSeverity.Blocking, finding.Severity);
    }

    [Fact]
    public void Critique_LowSoftMetric_ProducesWarning()
    {
        var report = new QualityReport(Guid.NewGuid(), "v1",
            [new MetricValue("MeanHueFamilies", 0.1, 1.0, HardGate: false, "Too many hue families per frame.")],
            []);

        var findings = NumericCritic.Critique(report);

        var finding = Assert.Single(findings);
        Assert.Equal(CritiqueSeverity.Warning, finding.Severity);
        Assert.Contains("0.10", finding.Message);
    }

    [Fact]
    public void Critique_NoDataSoftMetric_IsIgnored()
    {
        var report = new QualityReport(Guid.NewGuid(), "v1",
            [new MetricValue("BeatHitRate", 0.5, 0.0, HardGate: false, "No beat grid supplied.")],
            []);

        Assert.Empty(NumericCritic.Critique(report));
    }

    [Fact]
    public void Critique_PassingMetrics_ProduceNoFindings()
    {
        var report = new QualityReport(Guid.NewGuid(), "v1",
            [
                new MetricValue("UndeclaredBlackFrames", 1.0, 1.0, HardGate: true, "No undeclared black frames."),
                new MetricValue("MeanHueFamilies", 0.9, 1.0, HardGate: false, "Hue families within target."),
            ],
            []);

        Assert.Empty(NumericCritic.Critique(report));
    }

    [Fact]
    public void Critique_OrdersBlockingBeforeWarnings_WorstFirst()
    {
        var report = new QualityReport(Guid.NewGuid(), "v1",
            [
                new MetricValue("SoftA", 0.4, 1.0, HardGate: false, "Below target."),
                new MetricValue("HardGate1", 0.0, 1.0, HardGate: true, "Failed."),
                new MetricValue("SoftB", 0.1, 1.0, HardGate: false, "Well below target."),
            ],
            ["HardGate1: Failed."]);

        var findings = NumericCritic.Critique(report);

        Assert.Equal(["HardGate1", "SoftB", "SoftA"], findings.Select(f => f.MetricName));
    }
}
