namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 082's own heading, verbatim: "...preserve best-so-far candidate." Realizes PHASE-12
/// Section 12's own bounded-refinement pseudocode, verbatim: "if (report.Violations.Count == 0 &amp;&amp;
/// BetterThan(candidate, best)) best = candidate" -- <see cref="SelectBest"/> is this step's own
/// <c>BetterThan</c>, plus the violating/incomparable short-circuits the pseudocode's own prose implies but
/// does not spell out as code. A pure function of its own inputs; consults no store, mutates nothing, and
/// never returns a different <see cref="CandidateAttempt"/> instance than one of its own two inputs -- this
/// step's own implementation guidance, verbatim: "compare without corrupting prior candidate."
/// </summary>
public static class BestCandidateSelector
{
    /// <summary>
    /// Decides whether <paramref name="candidateAttempt"/> should replace <paramref name="currentBest"/> as
    /// a run's own best-so-far retained candidate.
    /// <list type="bullet">
    /// <item><description>A violating candidate (either <see cref="CandidateAttempt.Violations"/> or <see cref="QualityReport.Violations"/> non-empty) can never become best -- this step's own required failure/edge case, verbatim: "Malformed proposal... must not auto-commit." <paramref name="currentBest"/> is returned unchanged.</description></item>
    /// <item><description>When there is no <paramref name="currentBest"/> yet, a clean (non-violating) candidate always becomes best.</description></item>
    /// <item><description>When <paramref name="currentBest"/>'s own report and <paramref name="candidateReport"/> were produced by different <see cref="QualityReport.EvaluatorVersion"/>s (<see cref="QualityReport.AreComparable"/> is <see langword="false"/>), the two cannot be safely compared -- PHASE-12 Section 7 cross-cutting test #7, verbatim: "Quality metric evaluator-version mismatch flagged." <paramref name="currentBest"/> is preserved unchanged rather than guessed at.</description></item>
    /// <item><description>Otherwise, the candidate with the higher <see cref="AggregateScore"/> wins; a tie preserves <paramref name="currentBest"/> (never churns the retained candidate without a strict improvement -- this step's own required acceptance, verbatim, applied to preservation: a non-improving attempt must not displace a prior one).</description></item>
    /// </list>
    /// </summary>
    public static (CandidateAttempt Attempt, QualityReport Report)? SelectBest(
        (CandidateAttempt Attempt, QualityReport Report)? currentBest,
        CandidateAttempt candidateAttempt,
        QualityReport candidateReport)
    {
        ArgumentNullException.ThrowIfNull(candidateAttempt);
        ArgumentNullException.ThrowIfNull(candidateReport);

        if (candidateAttempt.Violations.Count > 0 || candidateReport.Violations.Count > 0)
        {
            return currentBest;
        }

        if (currentBest is not { } best)
        {
            return (candidateAttempt, candidateReport);
        }

        if (!QualityReport.AreComparable(best.Report, candidateReport))
        {
            return currentBest;
        }

        var candidateScore = AggregateScore(candidateReport);
        var bestScore = AggregateScore(best.Report);

        return candidateScore > bestScore ? (candidateAttempt, candidateReport) : currentBest;
    }

    /// <summary>
    /// A single weighted-average score in <c>[0, 1]</c> across every <see cref="QualityReport.Metrics"/>
    /// entry with <see cref="MetricValue.Weight"/> &gt; <c>0</c> -- a <c>Weight == 0</c> "no data" metric
    /// (Step 081's own required failure/edge case: "missing media must be handled explicitly") never
    /// contributes, so an absent analysis input can neither help nor hurt a candidate's own aggregate score.
    /// Returns <c>0.0</c> when no metric carries positive weight (nothing was actually evaluated).
    /// </summary>
    public static double AggregateScore(QualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var weighted = report.Metrics.Where(m => m.Weight > 0.0).ToArray();
        if (weighted.Length == 0)
        {
            return 0.0;
        }

        var totalWeight = weighted.Sum(m => m.Weight);
        return weighted.Sum(m => m.Value * m.Weight) / totalWeight;
    }
}
