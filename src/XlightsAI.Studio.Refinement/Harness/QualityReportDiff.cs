namespace XlightsAI.Studio.Refinement.Harness;

/// <summary>
/// One metric's own change between two iterations' <see cref="QualityReport"/>s.
/// </summary>
/// <param name="MetricName">The <see cref="MetricValue.Name"/> this entry tracks.</param>
/// <param name="PreviousValue">The value in the previous iteration's report, or <see langword="null"/> if this metric did not exist there.</param>
/// <param name="CurrentValue">The value in the current iteration's report, or <see langword="null"/> if this metric does not exist there.</param>
/// <param name="IsRegression"><see langword="true"/> when this metric got meaningfully worse, independent of whether any other metric improved.</param>
public sealed record MetricDelta(string MetricName, double? PreviousValue, double? CurrentValue, bool IsRegression);

/// <summary>
/// Build-plan Phase 7d "Convergence discipline," architecture §9 verbatim: "every iteration re-runs the
/// full battery and diffs against the previous iteration, not just against targets. A metric that
/// regresses is a finding even when it still passes." This is that diff: a pure function of two
/// <see cref="QualityReport"/>s, blind to whether the *targeted* metric of this iteration's revision
/// improved -- it must surface a regression on any OTHER metric exactly as readily, which is the real
/// incident this discipline exists to catch (<c>EXPORT_AUDIT.md</c>: "the pass that fixed the black intro
/// ... simultaneously made 8 of 11 loud sections dimmer").
/// </summary>
public static class QualityReportDiff
{
    /// <summary>
    /// Compares <paramref name="current"/> against <paramref name="previous"/> metric-by-metric. A metric
    /// is a regression when its <see cref="MetricValue.Value"/> dropped by more than <paramref name="regressionThreshold"/>
    /// (default <c>0.05</c>, on the same <c>[0,1]</c> scale every metric already uses) -- never when it
    /// simply failed to improve, since a flat metric is not a regression. Throws nothing on an
    /// incomparable <see cref="QualityReport.EvaluatorVersion"/> pair -- it still diffs by name, since a
    /// genuine regression is informative even across evaluator versions, but the result's
    /// <see cref="QualityReportDiffResult.EvaluatorVersionChanged"/> flags that the comparison crosses a
    /// version boundary so a caller can discount it if it chooses.
    /// </summary>
    public static QualityReportDiffResult Compare(QualityReport previous, QualityReport current, double regressionThreshold = 0.05)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        previous.Validate();
        current.Validate();

        var previousByName = previous.Metrics.ToDictionary(m => m.Name, m => m.Value, StringComparer.Ordinal);
        var currentByName = current.Metrics.ToDictionary(m => m.Name, m => m.Value, StringComparer.Ordinal);

        var allNames = previousByName.Keys.Union(currentByName.Keys, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal);

        var deltas = new List<MetricDelta>();
        foreach (var name in allNames)
        {
            var hasPrevious = previousByName.TryGetValue(name, out var previousValue);
            var hasCurrent = currentByName.TryGetValue(name, out var currentValue);

            var isRegression = hasPrevious && hasCurrent && (previousValue - currentValue) > regressionThreshold;

            deltas.Add(new MetricDelta(name, hasPrevious ? previousValue : null, hasCurrent ? currentValue : null, isRegression));
        }

        return new QualityReportDiffResult(deltas, !QualityReport.AreComparable(previous, current));
    }
}

/// <param name="Deltas">Every metric present in either report, in stable name order.</param>
/// <param name="EvaluatorVersionChanged"><see langword="true"/> when the two reports were produced by different <see cref="QualityReport.EvaluatorVersion"/>s.</param>
public sealed record QualityReportDiffResult(IReadOnlyList<MetricDelta> Deltas, bool EvaluatorVersionChanged)
{
    /// <summary>Every metric that regressed, worst-first.</summary>
    public IReadOnlyList<MetricDelta> Regressions => Deltas.Where(d => d.IsRegression).OrderBy(d => d.CurrentValue - d.PreviousValue).ToList();
}
