using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 28a §3 ("Closing the intent loop"): the two rules that make
/// <see cref="SectionCue"/> sequencing causally real instead of advisory prose -- the contrast rule (a
/// Bright/Full cue on some Props must be preceded by Off/Dim on that same Props, or declare a deliberate
/// <see cref="SectionCue.HandoffFromProps"/>) and the concurrency rule (the real floor
/// <see cref="ChoreographyLegalityValidator.RuleSectionModelBreadth"/> already enforces post-compile, now
/// also checked pre-compile against the union of this timeline's own Cues and its base Look's layers) --
/// plus the mechanism that makes "never invent a timestamp" actually enforced: every
/// <see cref="SectionCue.AnchorId"/>/<see cref="SectionCue.HoldUntilAnchorId"/> must resolve against
/// <see cref="SectionTimingMarks.Resolve"/>'s own real enumeration for this section. Same "never throws,
/// always returns the full violation list, retry-worthy" discipline as <see cref="SectionScriptValidator"/>
/// -- callers already know how to turn a <see cref="SectionScriptValidationError"/> list into a
/// correction prompt, so this validator reuses that exact type rather than inventing a parallel one.
/// </summary>
public static class SectionTimelineValidator
{
    private const string RuleContrast = "section-timeline-contrast";
    private const string RuleConcurrency = "section-timeline-concurrency";
    private const string RuleUnknownAnchor = "section-timeline-unknown-anchor";

    /// <param name="timeline">The Choreographer's own response, whose <see cref="SectionTimeline.CuesOrEmpty"/> may be empty -- an empty-Cues timeline is validated as a plain flat selection (nothing here to check) and the caller falls back to <see cref="SectionScriptValidator"/> for the rest, per the migration plan's own backward-compatibility seam.</param>
    /// <param name="context">This section's own real music context -- the same one <see cref="SectionTimelineCompiler"/> compiles against, so <see cref="SectionTimingMarks.Resolve"/> sees identical real marks here and at compile time.</param>
    /// <param name="phrases">This section's own real phrases, or <see langword="null"/>/empty -- same optional signal <c>XlightsAI.Studio.Director.SectionScriptPromptBuilder</c> already threads per section.</param>
    /// <param name="transientHits">This section's own real transient hits, or <see langword="null"/>/empty.</param>
    /// <param name="vocalNotes">This section's own real salient vocal-note onsets, or <see langword="null"/>/empty.</param>
    /// <param name="leadInstrumentWindows">This section's own real lead-instrument windows, or <see langword="null"/>/empty.</param>
    public static IReadOnlyList<SectionScriptValidationError> Validate(
        SectionTimeline timeline, LookLibrary lookLibrary, DisplayGraph layout, SectionMusicContext context,
        IReadOnlyList<PhraseCandidate>? phrases = null, IReadOnlyList<TransientHit>? transientHits = null,
        IReadOnlyList<NoteEvent>? vocalNotes = null, IReadOnlyList<LeadInstrumentWindow>? leadInstrumentWindows = null)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(lookLibrary);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(context);

        var errors = new List<SectionScriptValidationError>();

        var look = lookLibrary.Find(timeline.LookId);
        if (look is null)
        {
            errors.Add(new SectionScriptValidationError("look-ref", $"SectionTimeline for section '{timeline.SectionId}' references Look '{timeline.LookId}', which is not in the supplied Show Bible's Look library."));
            return errors;
        }

        if (timeline.CuesOrEmpty.Count == 0)
        {
            return errors;
        }

        var marks = SectionTimingMarks.Resolve(context, phrases, transientHits, vocalNotes, leadInstrumentWindows);

        foreach (var cue in timeline.CuesOrEmpty)
        {
            if (!marks.ContainsKey(cue.AnchorId))
            {
                errors.Add(new SectionScriptValidationError(RuleUnknownAnchor,
                    $"SectionTimeline for section '{timeline.SectionId}' cue on Props '{cue.Props}' names AnchorId '{cue.AnchorId}', which is not one of the real timing marks offered for this section."));
            }

            if (cue.HoldUntilAnchorId is { Length: > 0 } holdId && !marks.ContainsKey(holdId))
            {
                errors.Add(new SectionScriptValidationError(RuleUnknownAnchor,
                    $"SectionTimeline for section '{timeline.SectionId}' cue on Props '{cue.Props}' names HoldUntilAnchorId '{holdId}', which is not one of the real timing marks offered for this section."));
            }
        }

        var resolvableCues = timeline.CuesOrEmpty.Where(c => marks.ContainsKey(c.AnchorId)).ToList();
        foreach (var group in resolvableCues.GroupBy(c => c.Props, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(c => marks[c.AnchorId]).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                var current = ordered[i];
                if (!IntensityTierMapping.IsBrightTier(current.Intensity) || !IntensityTierMapping.IsBrightTier(previous.Intensity))
                {
                    continue;
                }

                var isDeliberateHandoff = current.HandoffFromProps is { Length: > 0 } handoffFrom &&
                    !string.Equals(handoffFrom, current.Props, StringComparison.Ordinal) &&
                    WasAlreadyBright(resolvableCues, handoffFrom, marks[current.AnchorId], marks);

                if (!isDeliberateHandoff)
                {
                    errors.Add(new SectionScriptValidationError(RuleContrast,
                        $"SectionTimeline for section '{timeline.SectionId}': cue on Props '{current.Props}' at anchor '{current.AnchorId}' (Intensity={current.Intensity}) immediately follows another cue on the SAME Props at anchor '{previous.AnchorId}' (Intensity={previous.Intensity}) with no real dim-before-bright contrast, and no HandoffFromProps naming an already-bright different prop -- a Bright/Full cue must be preceded by Off/Dim on its own Props, or declare a deliberate handoff."));
                }
            }
        }

        var distinctProps = timeline.CuesOrEmpty.Select(c => c.Props)
            .Concat(look.Layers.Select(l => l.Props))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var floor = ChoreographyLegalityValidator.MinDistinctModelsForSectionKind(context.Kind);
        if (distinctProps < floor)
        {
            errors.Add(new SectionScriptValidationError(RuleConcurrency,
                $"SectionTimeline for section '{timeline.SectionId}' names only {distinctProps} distinct Props across its Cues and base Look '{look.Id}' -- expected at least {floor} for this section kind."));
        }

        return errors;
    }

    private static bool WasAlreadyBright(IReadOnlyList<SectionCue> cues, string props, long atOrBeforeTick, IReadOnlyDictionary<string, long> marks) =>
        cues.Where(c => string.Equals(c.Props, props, StringComparison.Ordinal) && marks.TryGetValue(c.AnchorId, out var t) && t <= atOrBeforeTick)
            .OrderByDescending(c => marks[c.AnchorId])
            .FirstOrDefault() is { } latest && IntensityTierMapping.IsBrightTier(latest.Intensity);
}
