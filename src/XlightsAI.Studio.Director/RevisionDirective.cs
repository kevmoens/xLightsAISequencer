using XlightsAI.Studio.Refinement.Harness;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 7d: "RevisionDirective schema targeting specific sections." Architecture §9's own
/// "Revise" discipline, verbatim: "Critique emits targeted RevisionDirectives against specific sections,
/// not a request to regenerate the song. Only affected SectionScripts re-run; the expensive Show Bible
/// call is usually untouched." A section with no directive is never regenerated.
/// </summary>
/// <param name="SectionId">Which section this directive targets.</param>
/// <param name="Kind">A short, stable tag for what kind of finding produced this directive (e.g. a <see cref="CritiqueFinding.MetricName"/>, or <c>"visual"</c> for a visual-critique loss).</param>
/// <param name="Instruction">Plain-text instruction appended to that one section's own volatile Choreographer prompt suffix -- never applied to any other section.</param>
/// <param name="SourceFindingIds">Which numeric/visual findings this directive was derived from, for traceability.</param>
public sealed record RevisionDirective(Guid SectionId, string Kind, string Instruction, IReadOnlyList<string> SourceFindingIds);

/// <summary>
/// Turns 7c's own critique outputs (<see cref="CritiqueFinding"/>, <see cref="SectionVisualCritique"/>)
/// into <see cref="RevisionDirective"/>s. Pure and deterministic: the same findings always produce the
/// same directives. A section with no actionable finding gets no directive.
/// </summary>
public static class RevisionDirectiveDeriver
{
    /// <summary>
    /// Numeric findings are not inherently section-scoped (a <see cref="CritiqueFinding"/> describes a
    /// whole-sequence metric), so the caller supplies which sections that metric implicates --
    /// <paramref name="sectionsByFinding"/> maps a finding's own identity (its <see cref="CritiqueFinding.MetricName"/>)
    /// to the sections it should target (e.g. the sections whose declared energy ceiling the per-section-
    /// energy metric found exceeded). A metric with no mapped sections produces no directive -- it is
    /// recorded as a finding but has nowhere actionable to send a re-prompt.
    /// </summary>
    public static IReadOnlyList<RevisionDirective> FromNumericFindings(
        IReadOnlyList<CritiqueFinding> findings, IReadOnlyDictionary<string, IReadOnlyList<Guid>> sectionsByFinding)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(sectionsByFinding);

        var directives = new List<RevisionDirective>();
        foreach (var finding in findings)
        {
            if (!sectionsByFinding.TryGetValue(finding.MetricName, out var sectionIds) || sectionIds.Count == 0)
            {
                continue;
            }

            foreach (var sectionId in sectionIds)
            {
                directives.Add(new RevisionDirective(
                    sectionId, finding.MetricName,
                    $"Numeric review found: {finding.Message} Adjust this section's choice so this measurement improves without breaking any other declared rule.",
                    [finding.MetricName]));
            }
        }

        return directives;
    }

    /// <summary>
    /// One <see cref="SectionVisualCritique"/> is already section-scoped. Only a confident, consistent loss
    /// (both orderings agreed the reference/corpus frame won -- <see cref="SectionVisualCritique.FlippedBetweenOrderings"/>
    /// <see langword="false"/> and <see cref="SectionVisualCritique.AgreedWinner"/> <see cref="PairwiseWinner.Reference"/>)
    /// becomes a directive. A flipped (low-confidence) verdict produces no directive -- architecture §9's
    /// own calibration discipline means a judge that disagreed with itself is not grounds to act on.
    /// </summary>
    public static IReadOnlyList<RevisionDirective> FromVisualCritiques(IReadOnlyList<SectionVisualCritique> critiques, IReadOnlyDictionary<Guid, string> sectionIntentById)
    {
        ArgumentNullException.ThrowIfNull(critiques);
        ArgumentNullException.ThrowIfNull(sectionIntentById);

        var directives = new List<RevisionDirective>();
        foreach (var critique in critiques)
        {
            if (critique.FlippedBetweenOrderings || critique.AgreedWinner != PairwiseWinner.Reference)
            {
                continue;
            }

            var intent = sectionIntentById.TryGetValue(critique.SectionId, out var text) ? text : "the section's declared creative intent";
            directives.Add(new RevisionDirective(
                critique.SectionId, "visual",
                $"A visual review judged this section's rendered frame as NOT delivering its stated intent (\"{intent}\") " +
                "as well as the prior/reference version, in both left-right orderings. Choose a different Look or prop binding " +
                "that more clearly delivers that intent.",
                [$"visual:{critique.SectionId}"]));
        }

        return directives;
    }
}
