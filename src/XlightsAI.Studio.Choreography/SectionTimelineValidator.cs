using XlightsAI.Studio.Knowledge;
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
    private const string RuleConcurrency = "section-timeline-concurrency";
    private const string RuleUnknownAnchor = "section-timeline-unknown-anchor";
    private const string RuleUnknownEffect = "section-timeline-unknown-effect";

    /// <param name="timeline">The Choreographer's own response, whose <see cref="SectionTimeline.CuesOrEmpty"/> may be empty -- an empty-Cues timeline is validated as a plain flat selection (nothing here to check) and the caller falls back to <see cref="SectionScriptValidator"/> for the rest, per the migration plan's own backward-compatibility seam.</param>
    /// <param name="context">This section's own real music context -- the same one <see cref="SectionTimelineCompiler"/> compiles against, so <see cref="SectionTimingMarks.Resolve"/> sees identical real marks here and at compile time.</param>
    /// <param name="phrases">This section's own real phrases, or <see langword="null"/>/empty -- same optional signal <c>XlightsAI.Studio.Director.SectionScriptPromptBuilder</c> already threads per section.</param>
    /// <param name="transientHits">This section's own real transient hits, or <see langword="null"/>/empty.</param>
    /// <param name="vocalNotes">This section's own real salient vocal-note onsets, or <see langword="null"/>/empty.</param>
    /// <param name="leadInstrumentWindows">This section's own real lead-instrument windows, or <see langword="null"/>/empty.</param>
    /// <param name="vocabulary">
    /// User-reported real bug (2026-11-18): a cue whose <see cref="SectionCue.Effect"/> names something
    /// that isn't a real registered effect (e.g. the model echoing one of its own <see cref="IntensityTier"/>
    /// values, such as "Bright", into the Effect field instead of Intensity) previously sailed through this
    /// validator unnoticed and surfaced only as a hard <see cref="ChoreographyCompilationException"/> deep in
    /// <see cref="SectionTimelineCompiler"/> -- after the Choreographer call had already been accepted, with
    /// no way to ask the model to fix just that one cue. Passing the real generated vocabulary here (same
    /// one <see cref="SectionScriptValidator"/> already checks <c>FillerEffects</c> against) lets this join
    /// the same reject-and-retry discipline. <see langword="null"/> (the default) skips this check entirely --
    /// every existing caller/test that predates this parameter keeps compiling and behaving identically.
    /// </param>
    public static IReadOnlyList<SectionScriptValidationError> Validate(
        SectionTimeline timeline, LookLibrary lookLibrary, DisplayGraph layout, SectionMusicContext context,
        IReadOnlyList<PhraseCandidate>? phrases = null, IReadOnlyList<TransientHit>? transientHits = null,
        IReadOnlyList<NoteEvent>? vocalNotes = null, IReadOnlyList<LeadInstrumentWindow>? leadInstrumentWindows = null,
        EffectVocabularyCatalog? vocabulary = null, EffectVisualCharacterCatalog? brightnessCatalog = null,
        SectionTimingCatalog? catalog = null)
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

        var marks = (catalog ?? SectionTimingCatalog.Build(context, phrases, transientHits, vocalNotes, leadInstrumentWindows)).Marks;

        foreach (var cue in timeline.CuesOrEmpty)
        {
            if (string.IsNullOrWhiteSpace(cue.AnchorId) || !marks.ContainsKey(cue.AnchorId))
            {
                errors.Add(new SectionScriptValidationError(RuleUnknownAnchor,
                    $"SectionTimeline for section '{timeline.SectionId}' cue on Props '{cue.Props}' names AnchorId '{cue.AnchorId}', which is not one of the real timing marks offered for this section."));
            }
            else if (marks[cue.AnchorId] < context.Window.StartTicks || marks[cue.AnchorId] >= context.Window.EndTicks)
            {
                errors.Add(new SectionScriptValidationError("section-timeline-cue", $"Cue '{cue.AnchorId}' must start inside its owning section."));
            }

            if (cue.Kind is not (CueKind.Hold or CueKind.Accent or CueKind.Takeover) ||
                cue.Intensity is not (IntensityTier.Off or IntensityTier.Dim or IntensityTier.Bright or IntensityTier.Full))
            {
                errors.Add(new SectionScriptValidationError("section-timeline-cue", $"Cue '{cue.AnchorId}' must use a supported kind and intensity."));
            }

            if (cue.HoldUntilAnchorId is { Length: > 0 } holdId && !marks.ContainsKey(holdId))
            {
                errors.Add(new SectionScriptValidationError(RuleUnknownAnchor,
                    $"SectionTimeline for section '{timeline.SectionId}' cue on Props '{cue.Props}' names HoldUntilAnchorId '{holdId}', which is not one of the real timing marks offered for this section."));
            }
            else if (cue.HoldUntilAnchorId is { Length: > 0 } endId && !string.IsNullOrWhiteSpace(cue.AnchorId) &&
                marks.TryGetValue(cue.AnchorId, out var anchor) && marks[endId] <= anchor)
            {
                errors.Add(new SectionScriptValidationError("section-timeline-cue", $"Cue '{cue.AnchorId}' must end after it starts."));
            }

            if (cue.TargetReferences.Any(p => LookCompiler.TryResolveModels(p, layout).Count == 0) ||
                (cue.HandoffFromProps is { Length: > 0 } from && LookCompiler.TryResolveModels(from, layout).Count == 0))
            {
                errors.Add(new SectionScriptValidationError("section-timeline-target", $"Cue '{cue.AnchorId}' must name real targets for Props and HandoffFromProps."));
            }

            if (vocabulary is not null && !vocabulary.Effects.ContainsKey(cue.Effect))
            {
                errors.Add(new SectionScriptValidationError(RuleUnknownEffect,
                    $"SectionTimeline for section '{timeline.SectionId}' cue on Props '{cue.Props}' declares Effect '{cue.Effect}', which is not a real effect in the supplied vocabulary -- Effect must name an actual xLights effect, never an Intensity tier (Off/Dim/Bright/Full) or other value."));
            }
            if (brightnessCatalog?.Find(cue.Effect) is { } visual &&
                ((cue.Intensity == IntensityTier.Dim && visual.BrightnessTier == BrightnessTier.Bright) ||
                 (cue.Intensity is IntensityTier.Bright or IntensityTier.Full && visual.BrightnessTier != BrightnessTier.Bright)))
            {
                errors.Add(new SectionScriptValidationError("section-timeline-intensity",
                    $"Cue '{cue.AnchorId}' declares {cue.Intensity}, but '{cue.Effect}' is catalogued as {visual.BrightnessTier}. Choose a matching effect."));
            }
        }

        var windows = SectionCueWindow.Resolve(timeline, marks, context.Window, layout);
        var holds = windows.Where(w => w.IsHeld).ToArray();
        foreach (var window in windows)
        {
            if (window.Cue.Kind == CueKind.Accent && holds.Any(hold => window.OverlapsHeld(hold, windows)))
            {
                errors.Add(new SectionScriptValidationError("section-timeline-held-target",
                    $"Accent '{window.Cue.AnchorId}' overlaps a running hold on '{window.Cue.Props}' (including group members). Choose independent targets or declare a takeover."));
            }
            if (window.Cue.Kind == CueKind.Takeover && !window.ReplacesRunningHold(windows.Where(w => !ReferenceEquals(w, window)).ToArray()) &&
                catalog?.Sustains.Any(s => s.Note.StartTicks < context.Window.StartTicks && s.Note.EndTicks > window.Window.StartTicks) != true)
            {
                errors.Add(new SectionScriptValidationError("section-timeline-takeover",
                    $"Takeover '{window.Cue.AnchorId}' does not replace a running hold."));
            }
            if (window.Cue.Kind == CueKind.Hold && window.IsHeld && holds.Any(hold => hold.Index < window.Index && window.OverlapsHeld(hold, windows)))
            {
                errors.Add(new SectionScriptValidationError("section-timeline-held-target",
                    $"Hold '{window.Cue.AnchorId}' overlaps an already held model."));
            }
        }

        var distinctProps = timeline.CuesOrEmpty.SelectMany(c => c.TargetReferences)
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

}
