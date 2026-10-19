namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Section 3's own proposed signature, verbatim: <c>QualityReport(Guid CandidateId, string
/// EvaluatorVersion, IReadOnlyList&lt;MetricValue&gt; Metrics, IReadOnlyList&lt;string&gt; Violations)</c>.
/// Realized in full for this step ("Implement quality evaluators: timing, melodic alignment, density,
/// motif reuse, legal effects") -- the spec's own proposed shape needed no narrowing (unlike
/// <see cref="CandidateAttempt"/>/<c>Candidate</c> in Steps 079/080), because <c>MetricValue</c> and a
/// candidate-identity-keyed violation list are exactly what this step's own evaluator needs to produce.
/// </summary>
/// <param name="CandidateId">The <see cref="CandidateAttempt.Id"/> this report was computed for.</param>
/// <param name="EvaluatorVersion">
/// Identifies which evaluator (and its scoring-recipe version) produced this report. PHASE-12 Section 12's
/// own bounded-refinement pseudocode, verbatim: "<c>BetterThan</c> must compare quality reports produced
/// with the same evaluator version and normalized units" -- two reports with different
/// <see cref="EvaluatorVersion"/> values must never be compared as if their <see cref="Metrics"/> used the
/// same scoring recipe (PHASE-12 Section 7 cross-cutting test #7: "Quality metric evaluator-version
/// mismatch flagged"). See <see cref="AreComparable"/>.
/// </param>
/// <param name="Metrics">Every scored dimension -- both hard gates and soft metrics -- this evaluator computed for this candidate.</param>
/// <param name="Violations">
/// Every hard-gate failure and upstream malformed-proposal reason, in human-readable form. Non-empty here
/// means this candidate must never become <see cref="LoopRun.BestCandidateId"/> -- this step's own
/// required failure/edge case, verbatim (restated from Steps 079/080): a candidate that fails a hard gate
/// "must not auto-commit."
/// </param>
public sealed record QualityReport(
    Guid CandidateId,
    string EvaluatorVersion,
    IReadOnlyList<MetricValue> Metrics,
    IReadOnlyList<string> Violations)
{
    /// <exception cref="RefinementRunInvalidException">
    /// <see cref="CandidateId"/> is empty; <see cref="EvaluatorVersion"/> is blank; any entry of
    /// <see cref="Metrics"/> fails its own <see cref="MetricValue.Validate"/>; or a hard-gate metric with
    /// <see cref="MetricValue.Value"/> <c>== 0.0</c> has no corresponding entry in <see cref="Violations"/>.
    /// </exception>
    public void Validate()
    {
        if (CandidateId == Guid.Empty)
        {
            throw new RefinementRunInvalidException("A QualityReport's CandidateId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(EvaluatorVersion))
        {
            throw new RefinementRunInvalidException($"QualityReport '{CandidateId}' EvaluatorVersion must not be blank.");
        }

        ArgumentNullException.ThrowIfNull(Metrics);
        ArgumentNullException.ThrowIfNull(Violations);

        foreach (var metric in Metrics)
        {
            ArgumentNullException.ThrowIfNull(metric);
            metric.Validate();
        }

        var failedHardGates = Metrics.Where(m => m.HardGate && m.Value == 0.0).ToArray();
        if (failedHardGates.Length > 0 && Violations.Count == 0)
        {
            throw new RefinementRunInvalidException(
                $"QualityReport '{CandidateId}' has {failedHardGates.Length} failed hard gate(s) " +
                $"({string.Join(", ", failedHardGates.Select(m => m.Name))}) but an empty Violations list -- " +
                "a failed hard gate must always be reflected in Violations so a candidate cannot auto-commit.");
        }
    }

    /// <summary>
    /// <see langword="true"/> when <paramref name="a"/> and <paramref name="b"/> were produced by the same
    /// <see cref="EvaluatorVersion"/> and may therefore be compared metric-for-metric (PHASE-12 Section 12:
    /// "<c>BetterThan</c> must compare quality reports produced with the same evaluator version and
    /// normalized units"). <see langword="false"/> otherwise -- this step's own realization of PHASE-12
    /// Section 7 cross-cutting test #7, verbatim: "Quality metric evaluator-version mismatch flagged."
    /// </summary>
    public static bool AreComparable(QualityReport a, QualityReport b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return string.Equals(a.EvaluatorVersion, b.EvaluatorVersion, StringComparison.Ordinal);
    }
}
