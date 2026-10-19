namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 068's own full resolution outcome for one evaluation window: every candidate's own
/// admission decision, plus the resulting measured budget state ("concurrent focal count, event density,
/// brightness proxy, rest fraction" -- implementation guidance, verbatim).
/// </summary>
/// <param name="Results">Every input candidate's own resolved outcome, in the same relative order <see cref="IVisualBudgetResolver.Resolve"/> received them.</param>
/// <param name="ObservedMeanBrightness">The arithmetic mean <see cref="VisualBudgetCandidate.Brightness"/> across every admitted (<see cref="VisualBudgetDecision.Accepted"/> or <see cref="VisualBudgetDecision.Trimmed"/>) candidate; <c>0</c> when none were admitted.</param>
/// <param name="ObservedEventDensityPerSecond">Admitted candidate count divided by the evaluation window's own duration in seconds.</param>
/// <param name="ObservedPeakConcurrentFocalProps">The highest number of admitted focal candidates simultaneously overlapping at any single instant.</param>
/// <param name="ObservedDarknessFraction">The fraction of the evaluation window covered by no admitted candidate's own window.</param>
/// <param name="Warnings">Non-fatal observations -- e.g. the observed darkness fraction falling short of <see cref="VisualBudget.DarknessFractionTarget"/>. Never a reason a candidate was dropped (see <see cref="VisualBudgetCandidateResult.Reason"/> for that).</param>
public sealed record VisualBudgetResolution(
    IReadOnlyList<VisualBudgetCandidateResult> Results,
    double ObservedMeanBrightness,
    double ObservedEventDensityPerSecond,
    int ObservedPeakConcurrentFocalProps,
    double ObservedDarknessFraction,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Every result whose <see cref="VisualBudgetCandidateResult.Decision"/> is not <see cref="VisualBudgetDecision.Dropped"/>.</summary>
    public IEnumerable<VisualBudgetCandidateResult> Admitted => Results.Where(r => r.Decision != VisualBudgetDecision.Dropped);

    /// <summary>Every result whose <see cref="VisualBudgetCandidateResult.Decision"/> is <see cref="VisualBudgetDecision.Dropped"/>.</summary>
    public IEnumerable<VisualBudgetCandidateResult> Dropped => Results.Where(r => r.Decision == VisualBudgetDecision.Dropped);
}
