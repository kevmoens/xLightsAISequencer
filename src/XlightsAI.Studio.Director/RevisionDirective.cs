using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
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

    /// <summary>
    /// User feedback (2026-10-31): a prop (e.g. an Arch) silently ending up with zero effects for an
    /// entire show was previously only a soft prompt instruction the Director could ignore with nothing
    /// checking compliance. <see cref="ChoreographyLegalityValidator.ValidateShow"/>'s
    /// <see cref="ChoreographyLegalityValidator.RuleModelCoverage"/> rule now detects it after compile;
    /// this turns each such finding into a real, self-healing targeted regeneration directive rather than
    /// a violation nobody acts on. Every uncovered model is directed at the single longest section (by
    /// window duration) -- the section with the most real screen-time to plausibly fit one more prop's
    /// moment into, and a single, deterministic, defensible choice rather than round-robining across every
    /// section (which would regenerate far more than the "only affected SectionScripts re-run" principle
    /// calls for) or guessing a section from the violation text. A show with no sections at all (so no
    /// window to target) produces no directive here -- nothing this deriver can act on.
    /// </summary>
    public static IReadOnlyList<RevisionDirective> FromModelCoverageViolations(
        IReadOnlyList<LegalityViolation> violations, IReadOnlyDictionary<Guid, (long Start, long End)> sectionWindowsById)
    {
        ArgumentNullException.ThrowIfNull(violations);
        ArgumentNullException.ThrowIfNull(sectionWindowsById);

        var uncoveredModelIds = violations
            .Where(v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage && v.AffectedModelId is not null)
            .Select(v => v.AffectedModelId!)
            .ToList();

        if (uncoveredModelIds.Count == 0 || sectionWindowsById.Count == 0)
        {
            return [];
        }

        var longestSectionId = sectionWindowsById
            .OrderByDescending(kv => kv.Value.End - kv.Value.Start)
            .First().Key;

        return uncoveredModelIds
            .Select(modelId => new RevisionDirective(
                longestSectionId, "model-coverage",
                $"Model '{modelId}' has received zero effects anywhere in this show so far. Give it a real, " +
                "deliberate moment somewhere in THIS section specifically -- bind at least one Look layer to it " +
                "(directly, or via a group that includes it).",
                [$"model-coverage:{modelId}"]))
            .ToList();
    }

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 1 (2026-11-01): turns each
    /// <see cref="TimedCueComplianceChecker.FindMissedCues"/> result into a real, targeted repair
    /// directive -- a user-requested moment (e.g. "hit BANG near 0:24") the previous attempt did not
    /// satisfy. Targets whichever real section's own window overlaps the cue's tolerance-expanded window
    /// and sits closest (by window midpoint) to the cue's own stated time -- when more than one section
    /// overlaps (a cue near a section boundary), the closer one is the more natural fit for "near this
    /// moment." A cue whose window overlaps NO section at all (a time past the end of the song, or a
    /// gap in bar-grid coverage) produces no directive -- nothing this deriver can act on, same
    /// "unmapped == no directive" convention as <see cref="FromNumericFindings"/>.
    /// </summary>
    public static IReadOnlyList<RevisionDirective> FromMissedTimedCues(
        IReadOnlyList<TimedCue> missedCues, IReadOnlyDictionary<Guid, (long Start, long End)> sectionWindowsById)
    {
        ArgumentNullException.ThrowIfNull(missedCues);
        ArgumentNullException.ThrowIfNull(sectionWindowsById);

        var directives = new List<RevisionDirective>();
        foreach (var cue in missedCues)
        {
            var windowStartTicks = cue.WindowStart.Ticks;
            var windowEndTicks = cue.WindowEnd.Ticks;
            var cueMidpointTicks = (cue.Start.Ticks + cue.EffectiveEnd.Ticks) / 2;

            var overlapping = sectionWindowsById
                .Where(kv => kv.Value.Start < windowEndTicks && kv.Value.End > windowStartTicks)
                .ToList();

            if (overlapping.Count == 0)
            {
                continue;
            }

            var targetSectionId = overlapping
                .OrderBy(kv => Math.Abs((kv.Value.Start + kv.Value.End) / 2 - cueMidpointTicks))
                .First().Key;

            var rangeText = cue.End is { } end && end != cue.Start
                ? $"{FormatTimestamp(cue.Start)}-{FormatTimestamp(end)}"
                : FormatTimestamp(cue.Start);

            directives.Add(new RevisionDirective(
                targetSectionId, "timed-cue",
                $"A user-requested cue near {rangeText} was NOT satisfied in the previous attempt: " +
                $"\"{cue.Instruction}\". Add a short, discrete (brief, instantaneous-feeling) effect as " +
                "close as you reasonably can to that real-time moment, somewhere in THIS section specifically.",
                [$"timed-cue:{cue.Start}"]));
        }

        return directives;
    }

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 8 (2026-11-02): turns each
    /// <see cref="SingleModelComplianceChecker.FindGroupedViolations"/> result into a real, targeted repair
    /// directive -- the user explicitly asked for this model to be targeted individually (via
    /// <see cref="SingleModelDirectiveParser"/>), but the previous attempt only ever reached it through a
    /// group. Targets the single longest section, same deterministic, defensible choice as
    /// <see cref="FromModelCoverageViolations"/> for the same reasoning (the most real screen-time to
    /// plausibly fit one more direct binding into). A show with no sections produces no directive --
    /// nothing this deriver can act on.
    /// </summary>
    public static IReadOnlyList<RevisionDirective> FromSingleModelViolations(
        IReadOnlyList<ModelId> violatedModels, IReadOnlyDictionary<Guid, (long Start, long End)> sectionWindowsById)
    {
        ArgumentNullException.ThrowIfNull(violatedModels);
        ArgumentNullException.ThrowIfNull(sectionWindowsById);

        if (violatedModels.Count == 0 || sectionWindowsById.Count == 0)
        {
            return [];
        }

        var longestSectionId = sectionWindowsById
            .OrderByDescending(kv => kv.Value.End - kv.Value.Start)
            .First().Key;

        return violatedModels
            .Select(modelId => new RevisionDirective(
                longestSectionId, "single-model-not-group",
                $"Model '{modelId.Value}' has only ever been targeted through a group effect so far, but the user explicitly " +
                "asked for it to be targeted individually, not (only) as part of a group. Add at least one effect in THIS " +
                $"section that binds directly to '{modelId.Value}' by its own name, not a group containing it.",
                [$"single-model-not-group:{modelId.Value}"]))
            .ToList();
    }

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): turns a user's own explicit
    /// touch-up request (e.g. "at 0:24-0:25, make it flash harder") into a directive for EVERY real
    /// section whose own window overlaps the cue's tolerance-expanded window -- deliberately every
    /// overlapping section, not just the single closest one <see cref="FromModelCoverageViolations"/>/
    /// <see cref="FromMissedTimedCues"/> pick for an automatically-DETECTED miss: this is a direct,
    /// user-authored edit naming its own exact range, so a range spanning a section boundary should touch
    /// every section it actually overlaps, not guess at the one "most likely" section. A cue whose window
    /// overlaps no section at all (a timestamp past the end of the song, or a typo) produces no directive --
    /// nothing this deriver can act on, same "unmapped == no directive" convention as every other method
    /// here.
    /// </summary>
    public static IReadOnlyList<RevisionDirective> FromPostProcessingCues(
        IReadOnlyList<TimedCue> cues, IReadOnlyDictionary<Guid, (long Start, long End)> sectionWindowsById)
    {
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(sectionWindowsById);

        var directives = new List<RevisionDirective>();
        foreach (var cue in cues)
        {
            var windowStartTicks = cue.WindowStart.Ticks;
            var windowEndTicks = cue.WindowEnd.Ticks;
            var rangeText = cue.End is { } end && end != cue.Start
                ? $"{FormatTimestamp(cue.Start)}-{FormatTimestamp(end)}"
                : FormatTimestamp(cue.Start);

            foreach (var (sectionId, window) in sectionWindowsById)
            {
                if (window.Start >= windowEndTicks || window.End <= windowStartTicks)
                {
                    continue;
                }

                directives.Add(new RevisionDirective(
                    sectionId, "post-processing-edit",
                    $"Touch-up request for around {rangeText}: \"{cue.Instruction}\". Apply ONLY this change -- keep " +
                    "everything else about this section's existing treatment as close to its current form as reasonable.",
                    [$"post-processing:{cue.Start}"]));
            }
        }

        return directives;
    }

    private static string FormatTimestamp(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";
}
