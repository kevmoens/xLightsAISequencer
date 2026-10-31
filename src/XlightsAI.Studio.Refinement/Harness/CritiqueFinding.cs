namespace XlightsAI.Studio.Refinement.Harness;

/// <summary>How serious one <see cref="CritiqueFinding"/> is.</summary>
public enum CritiqueSeverity
{
    /// <summary>A soft metric landed outside its target band but broke no invariant.</summary>
    Warning,

    /// <summary>A hard gate failed -- this candidate must never be auto-committed (mirrors <see cref="QualityReport.Violations"/>).</summary>
    Blocking,
}

/// <summary>
/// Build-plan Phase 7c "numeric critique": one mechanical, grounded finding derived directly from a
/// <see cref="QualityReport"/>'s own already-validated <see cref="MetricValue"/>s -- never an LLM call,
/// never a judgment the measured data does not support. Architecture §9's own governing constraint: "every
/// critique here must be grounded in something the generator never saw" -- for the numeric channel, that is
/// the measured metric itself, restated in words a revision step can act on.
/// </summary>
/// <param name="MetricName">The <see cref="MetricValue.Name"/> this finding came from.</param>
/// <param name="Severity"><see cref="CritiqueSeverity.Blocking"/> for a failed hard gate, else <see cref="CritiqueSeverity.Warning"/>.</param>
/// <param name="Message">Human-readable restatement of <see cref="MetricValue.Explanation"/> plus the measured value, never fabricated beyond what the metric itself reports.</param>
public sealed record CritiqueFinding(string MetricName, CritiqueSeverity Severity, string Message);

/// <summary>
/// Default, pure numeric critic. Deterministic: the same <see cref="QualityReport"/> always produces the
/// same finding list.
/// </summary>
public static class NumericCritic
{
    /// <summary>
    /// A <see cref="CritiqueSeverity.Blocking"/> finding for every failed hard gate (<see cref="MetricValue.HardGate"/>
    /// true, <see cref="MetricValue.Value"/> <c>0.0</c>), plus a <see cref="CritiqueSeverity.Warning"/>
    /// finding for every soft metric below <paramref name="softMetricWarningThreshold"/> that was actually
    /// evaluated (<see cref="MetricValue.Weight"/> <c>&gt; 0</c> -- a <c>Weight: 0</c> "no data" metric is
    /// never reported as a defect, since nothing was actually measured). Ordered blocking-first, then by
    /// ascending <see cref="MetricValue.Value"/> (worst first) within each severity.
    /// </summary>
    public static IReadOnlyList<CritiqueFinding> Critique(QualityReport report, double softMetricWarningThreshold = 0.5)
    {
        ArgumentNullException.ThrowIfNull(report);
        report.Validate();

        var findings = new List<CritiqueFinding>();

        foreach (var metric in report.Metrics)
        {
            if (metric.HardGate)
            {
                if (metric.Value == 0.0)
                {
                    findings.Add(new CritiqueFinding(metric.Name, CritiqueSeverity.Blocking, metric.Explanation));
                }

                continue;
            }

            if (metric.Weight > 0.0 && metric.Value < softMetricWarningThreshold)
            {
                findings.Add(new CritiqueFinding(metric.Name, CritiqueSeverity.Warning,
                    $"{metric.Explanation} (scored {metric.Value:0.00}, below the {softMetricWarningThreshold:0.00} warning threshold)."));
            }
        }

        return findings
            .OrderBy(f => f.Severity == CritiqueSeverity.Blocking ? 0 : 1)
            .ThenBy(f => report.Metrics.First(m => m.Name == f.MetricName).Value)
            .ToList();
    }
}
