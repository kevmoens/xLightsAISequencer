using System.Text;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 6 task 1/2: the Choreographer's own stable/volatile split, mirroring
/// <see cref="DirectorPromptBuilder"/>'s shape but scoped to L3's much smaller job -- *select and rebind*
/// a Look the Director already designed, never invent one. <see cref="StablePrefix"/> depends only on the
/// <see cref="ShowBible"/> (and the knowledge context), so it is byte-for-byte identical across every
/// section of one song -- exactly the prefix architecture §14/the build-plan's own task 2 means by
/// "sharing the cached Show Bible + vocabulary + craft-rules prefix" across the ~30 per-section calls.
/// </summary>
public static class SectionScriptPromptBuilder
{
    /// <summary>
    /// Build-plan Phase 6 task 1: assembles the one stable prefix every section call for <paramref name="bible"/>
    /// shares. Deliberately carries only each Look's id/intent/energyCeiling/motion/subject/paletteRef/
    /// variations/allowed section kinds -- never the full per-layer JSON (architecture §2's reasoning-tax
    /// argument: the Choreographer is picking among already-designed Looks, not re-deriving their layers).
    /// </summary>
    public static string BuildStablePrefix(ShowBibleContent bible, DirectorKnowledgeContext knowledge, string style, bool enableSectionTimelines = false)
    {
        ArgumentNullException.ThrowIfNull(bible);
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentException.ThrowIfNullOrWhiteSpace(style);

        var preset = StylePresetCatalog.Resolve(style);

        var sb = new StringBuilder();
        sb.AppendLine("You are the Choreographer for an automated Christmas light show choreography system.");
        sb.AppendLine("For ONE section at a time, produce a SectionScript: a single flat JSON object naming which");
        sb.AppendLine("Look this section uses and, optionally, which of that Look's layers should target a different");
        sb.AppendLine("real prop/group than the Look's own default. You never invent a new Look, a new effect, or a");
        sb.AppendLine("new parameter -- only select from the Look library below and rebind layers to real props.");
        sb.AppendLine($"Style: {preset.Name}. {preset.Description}");
        sb.AppendLine($"Palette discipline: {preset.PaletteDiscipline.MinActiveSlots}-{preset.PaletteDiscipline.MaxActiveSlots} active slots. Density target: {preset.Density.MinChangeRatePerSecond:0.##}-{preset.Density.MaxChangeRatePerSecond:0.##} changes/second.");
        sb.AppendLine();

        if (enableSectionTimelines)
        {
            sb.AppendLine("## Holds, accents and takeovers");
            sb.AppendLine("At a beat or other emphasis, choose freely: continue a hold unchanged, add a short independent accent on unheld models, or explicitly take over a held target. A bright hold through several hits is valid; a hit need not change the display. Expand groups mentally: an independent accent may not touch even one held member.");
            sb.AppendLine("For back-to-back bright accents, create strong dim-before-bright contrast. Before Full, dim the whole display except running holds; before Bright, dim only that accent's targets. Holds and takeovers last for their full declared span; choose a measured effect appropriate for the intensity. Off leaves the span dark.");
            sb.AppendLine();
        }
        else
        {
            AppendContrastDrivenEmphasisGuidance(sb);
        }

        sb.AppendLine("## Show Bible Look library");
        sb.AppendLine("Pick LookId from exactly these (id: intent, energyCeiling, motion, subject, paletteRef, allowed section kinds, variations):");
        foreach (var look in bible.Looks.Looks)
        {
            var allowed = look.AllowedSectionKindsOrEmpty.Count == 0 ? "any" : string.Join("/", look.AllowedSectionKindsOrEmpty);
            var variations = look.VariationsOrEmpty.Count == 0 ? "none" : string.Join("/", look.VariationsOrEmpty);
            sb.AppendLine($"- {look.Id}: \"{look.Intent}\" | ceiling={look.EnergyCeiling:0.00} | motion={look.Motion} | subject={look.Subject} | palette={look.PaletteRef} | sections={allowed} | variations={variations}");
            foreach (var layer in look.Layers.OrderBy(l => l.Z))
            {
                var effect = DirectorPromptBuilder.EnrichEffectName(layer.Effect, knowledge.EffectVisualCharacters);
                var annotationSuffix = string.IsNullOrWhiteSpace(layer.Annotation) ? string.Empty : $" annotation=\"{layer.Annotation}\"";
                sb.AppendLine($"    layer z={layer.Z} role={layer.Role} effect={effect} props={layer.Props}{annotationSuffix}");
            }
        }

        // Build-plan Phase 13 §2b: computed once from bible.Looks (never varies across this song's own
        // per-section calls), so it belongs in the stable prefix proper -- not re-derived per call as a
        // "volatile" signal the way occurrenceIndex/motifLink are, since both of those genuinely do vary
        // per section while this does not.
        var effectUsage = bible.Looks.Looks
            .SelectMany(l => l.Layers)
            .GroupBy(l => l.Effect, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ToList();
        if (effectUsage.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Effect usage across this song's own Look library (build-plan Phase 13 §2b -- avoid over-relying on the same effect)");
            sb.AppendLine($"This library's {bible.Looks.Looks.Count} Look(s) use {effectUsage.Count} distinct effect(s) across all layers: {string.Join(", ", effectUsage.Select(g => $"{g.Key}x{g.Count()}"))}.");
            sb.AppendLine("When more than one Look would serve a section equally well, favor one whose own effects are not already the heaviest-used above -- a Choreographer that keeps reaching for the same one or two heavily-used effects across many sections reads as monotone over a whole song, even though each individual pick looked reasonable on its own.");
        }

        sb.AppendLine();
        sb.AppendLine("## Layer annotations (optional, grounded -- build-plan Phase 18 §7)");
        sb.AppendLine("Each layer line above may already show an \"annotation=\" -- the Director's own grounded note on what that layer is meant to visually express. When you rebind a layer to a different prop/group via propBindings, that layer's original annotation no longer necessarily applies to the new target -- you MAY add an \"annotation\" field to that propBindings entry to state what the REBOUND layer now expresses instead; omitting it leaves the Look's own original annotation (if any) as-is.");
        sb.AppendLine("Same grounding rule as the Director's own annotations, judged against each layer's own enriched \"effect=\" text above: write a specific visual claim (shape/motion/direction) only for an effect shown with a real AI-observed description (no \"(not visually exercised...)\" qualifier) -- never for one marked documented-behavior-only or shown with no description at all. When in doubt, omit the annotation entirely rather than guess.");
        sb.AppendLine();
        sb.AppendLine("## Palettes");
        foreach (var palette in bible.Palettes)
        {
            sb.AppendLine($"- {palette.Id}: {string.Join(", ", palette.Colors.Select(c => $"{c.HexColor}({c.Role})"))}");
        }

        sb.AppendLine();
        sb.AppendLine("## Melodic contour guidance (build-plan Phase 10 §2c -- which way should something move?)");
        sb.AppendLine("Each section below carries a measured melodic contour digest. Use it to decide direction and shape of motion, not just how much energy to show:");
        sb.AppendLine("- Rising contour -> prefer upward/outward-reading motion or a brightness ramp that climbs across the phrase, landing the peak exactly on the phrase's highest note -- this is \"built to the lyric,\" not just \"built to the beat.\"");
        sb.AppendLine("- Falling contour -> the inverse: a settling, cascading, or dimming motion, used deliberately to mark a release after a held high note, a verse ending, or a pre-chorus breath.");
        sb.AppendLine("- High note density (fast runs, melismatic vocal lines) -> favor a faster Timescale/shorter duty cycle on the subject layer for that section specifically, independent of the section's overall energy ceiling -- a florid vocal run deserves visibly busier motion even inside an otherwise calm Verse.");
        sb.AppendLine("- Static/Sparse contour (a long held note or very little melodic movement) -> the opposite: this is exactly the moment for the \"hold is a steady state, never a dead one\" rule -- a long low-duty-cycle animated hold, not a flurry of changes, because the melody itself isn't moving.");

        sb.AppendLine();
        sb.AppendLine("## Motif leitmotif guidance (build-plan Phase 10 §2d -- which EARLIER section is this one musically the same idea as?)");
        sb.AppendLine("When a section's own prompt names a specific motif-linked predecessor section, that section shares real melodic/phrase content with this one (a recurring hook, not merely the same SectionKind) -- treat it the way a film score treats a leitmotif:");
        sb.AppendLine("- Reuse a recognizable visual signature from that earlier section -- the same accent palette slot, the same subject prop, or the same signature effect -- so a listener who remembers that earlier section visually recognizes this one as the same musical idea returning.");
        sb.AppendLine("- Then escalate rather than repeat identically: add a layer, raise duty cycle, or bring in an additional group compared to that earlier section -- never render this repeat as a visual copy-paste of it.");
        sb.AppendLine("- This is distinct from (and more specific than) the \"repeat N of M\" same-Kind guidance above -- a motif link can cross Kinds entirely, e.g. a Bridge that quotes the Chorus's own hook should still echo the Chorus's visual signature even though it is not itself classified as a Chorus.");

        sb.AppendLine();
        sb.AppendLine("## Phrase-scoped sub-section staging (build-plan Phase 10 §2e -- optional)");
        sb.AppendLine("A long/multi-phrase section does not have to render as one flat Look for its entire duration. When a section lists 2 or more of its own phrases below, you MAY declare up to 3 \"phraseVariations\" entries -- one per phrase you want to stage differently -- instead of (or on top of) the one Look you already picked for the section:");
        sb.AppendLine("- HARD REQUIREMENT: only declare phraseVariations for THIS call's own section, and only when this section's own prompt below actually lists a numbered phrase list (\"This section has N phrase(s):\"). If no phrase list appears below for this section, omit phraseVariations entirely (null/empty) -- never declare one anyway, even by analogy to a different section that did have phrases.");
        sb.AppendLine("- A typical staging: the FIRST phrase establishes the Look as-is (no entry needed for it); a MIDDLE phrase introduces a variation -- an added layer (name every layer z that should be active for that phrase, including the ones already active) or a palette-accent swap (a propBindings override that applies only to that phrase); the LAST phrase either escalates toward the next section (more active layers, a brighter/accent group swapped in) or recedes to set one up (fewer active layers).");
        sb.AppendLine("- This is entirely optional -- a short or simple section (few phrases, or nothing worth staging) does not need manufactured phrase-level variation. Never invent phrase variation just to use the feature.");
        sb.AppendLine("- Each phraseVariations entry names a 1-based phraseIndex (matching the numbered phrase list this section's own prompt gives you below), optionally activeLayerZs (the COMPLETE set of this Look's own layer z-values active for that phrase -- not an incremental add; omit to use every layer), and optionally propBindings scoped to just that phrase (same shape as the section-level propBindings above, overriding it for that phrase only).");
        sb.AppendLine("- Concrete worked example (build-plan Phase 13 §2d): a 3-phrase Verse section with phrases at bars [16,20), [20,24), [24,28) might declare exactly one entry -- phraseIndex 2, activeLayerZs [1,2] (introducing layer z=2, which was not active in phrase 1), with a propBindings entry rebinding that layer 2 to a brighter accent group. Phrase 1 needs no entry (it is simply the Look's own unmodified default); phrase 3 also needs no entry (it continues phrase 2's now-escalated state through the section's close). A single well-placed entry like this is a complete, valid staging -- you do not need all 3 phrases to have one.");

        sb.AppendLine();
        sb.AppendLine("## Verified craft rules");
        foreach (var rule in knowledge.CraftRules.Promoted.OrderByDescending(r => r.Confidence))
        {
            sb.AppendLine($"- {rule.Statement} (confidence {rule.Confidence:P0}, seen in {rule.SongCount} songs)");
        }

        return sb.ToString();
    }

    /// <summary>Build-plan Phase 6 task 1: this one section's own volatile music context + real layout, kept small -- the per-call cost this task exists to keep cheap.</summary>
    /// <param name="occurrenceIndex">1-based position of this section among every section sharing its own <see cref="SectionMusicContext.Kind"/> (e.g. the 3rd Chorus-kind section in the song) -- the one cross-section signal a stateless per-section call can cheaply carry, since it never sees a sibling section's own chosen Look.</param>
    /// <param name="totalOccurrences">How many sections total share this section's own Kind.</param>
    /// <param name="melodicBrief">Build-plan Phase 10 §3b: this section's own real melodic contour digest (<see cref="MelodicContourBriefBuilder.ComputeSectionBriefs"/>), or <see langword="null"/> when no real <see cref="MusicModel.Melody"/> was available for this song -- rendered as nothing rather than an invented/placeholder line.</param>
    /// <param name="motifLink">Build-plan Phase 10 §3c: the one specific earlier section this section's own real motif-clustering data ties it to (<see cref="MotifLinkedPredecessorBuilder.ComputeSectionLinks"/>), or <see langword="null"/> when this section has no motif-linked predecessor -- rendered as nothing rather than an invented/placeholder line.</param>
    /// <param name="phrases">Build-plan Phase 10 §3e: this section's own real phrases (<c>PhraseSegmentationAnalysis.Phrases</c> filtered to this section, ordered by <c>PhraseCandidate.StartBarIndex</c>), or <see langword="null"/>/empty when no real phrase segmentation is available for this section -- rendered as nothing (and the phraseVariations JSON field goes unmentioned) rather than an invented phrase list.</param>
    /// <param name="customPrompt">
    /// Build-plan Phase 14 §2c: the user's own free-text creative direction for this run (plain
    /// string, not the <c>CustomGenerationPrompt</c> record -- Director already has the full record
    /// by the time it calls <see cref="ChoreographyGenerationPipeline"/>, which unwraps it once
    /// rather than handing every one of the ~30 per-section calls the whole record), or
    /// <see langword="null"/>/empty for the pre-existing default. Appended into this volatile suffix
    /// only, never <see cref="BuildStablePrefix"/> -- same reasoning as <see cref="DirectorPromptBuilder"/>.
    /// </param>
    /// <summary>100ns ticks/second -- same unit/constant as every other tick field in this codebase.</summary>
    private const long TicksPerSecond = 10_000_000L;

    /// <param name="timedCues">
    /// Build-plan "richer analysis tools" Phase 1 (2026-11-01): only the <see cref="TimedCue"/>s
    /// (parsed once from the whole custom prompt via <see cref="CustomPromptTimedCueParser"/>) whose own
    /// tolerance-expanded window overlaps THIS section's real time window -- the caller
    /// (<see cref="ChoreographyGenerationPipeline"/>) is responsible for that overlap filtering, so this
    /// method never has to re-derive it. <see langword="null"/>/empty for the pre-existing default (no
    /// custom prompt, or none of its cues land in this section).
    /// </param>
    /// <param name="salientVocalNotes">
    /// User-requested real investigation (2026-11-08): "is there processing to split out singing from the
    /// rest of the song and notes?" -- yes, and until now that real, already-extracted vocal-note data
    /// never reached this prompt at all, only a coarse contour summary did. This section's own loudest
    /// sung-note onsets (<see cref="VocalNoteEmphasisBriefBuilder"/>, already capped/filtered by the
    /// caller), or <see langword="null"/>/empty when this section has no melody data or no sounding notes
    /// -- rendered as nothing rather than an invented note list.
    /// </param>
    /// <param name="transientHits">
    /// Build-plan Phase 22 §2b: this section's own real, measured loud percussive transients (<see cref="XlightsAI.Studio.Rhythm.TransientHit"/>)
    /// whose own time falls within this section's real time window -- the third of the three "repetition
    /// is the fact that emphasis-worthy moments recur constantly" signal types this phase's own contrast
    /// guidance names by name (transient hits, vocal-note onsets, timed cues), alongside the vocal-note
    /// emphasis line already below. <see langword="null"/>/empty when this song has none, or none land in
    /// this section -- rendered as nothing, never an invented hit.
    /// </param>
    /// <param name="enableSectionTimelines">
    /// Build-plan Phase 28a §6: disclosed-default-off gate for every §4 input-side addition below
    /// (real timing marks, the lead-instrument-window line, the section-local melodic/band curves, and
    /// the "cues" field documented in the required-JSON-shape line) -- mirrors
    /// <c>XlightsAI.Studio.Director.ChoreographyGenerationSettings.EnableSectionTimelines</c>. Defaults
    /// to <see langword="false"/> so every pre-existing caller/test produces byte-identical prompt text;
    /// a caller opts in explicitly once it is also prepared to branch its own response handling to
    /// <see cref="Choreography.SectionTimelineValidator"/>/<see cref="Choreography.SectionTimelineCompiler"/>.
    /// </param>
    /// <param name="leadInstrumentWindows">
    /// Build-plan Phase 28a §4d: this song's own real <see cref="XlightsAI.Studio.Rhythm.LeadInstrumentWindow"/>s
    /// (<c>MusicModel.LeadInstrumentWindows</c>) -- only rendered (filtered to this section's own real
    /// time window) when <paramref name="enableSectionTimelines"/> is <see langword="true"/>.
    /// </param>
    /// <param name="melodicCurve">Build-plan Phase 28a §4c: this section's own real, ordered melodic-movement samples (<see cref="MelodicContourBriefBuilder.ComputeSectionCurve"/>) -- only rendered when <paramref name="enableSectionTimelines"/> is <see langword="true"/>.</param>
    /// <param name="bandCurve">Build-plan Phase 28a §4c: this section's own real, ordered band-energy samples (<see cref="BandEnergyGroupRoleMapper.ComputeSectionCurve"/>) -- only rendered when <paramref name="enableSectionTimelines"/> is <see langword="true"/>.</param>
    public static string BuildVolatileSuffix(
        SectionLabelCandidate section, SectionMusicContext context, LayoutSummary layout, int occurrenceIndex = 1, int totalOccurrences = 1,
        SectionMelodicBrief? melodicBrief = null, SectionMotifLink? motifLink = null, IReadOnlyList<PhraseCandidate>? phrases = null,
        string? customPrompt = null, IReadOnlyList<TimedCue>? timedCues = null, IReadOnlyList<XlightsAI.Studio.Melody.NoteEvent>? salientVocalNotes = null,
        IReadOnlyList<XlightsAI.Studio.Rhythm.TransientHit>? transientHits = null, bool enableSectionTimelines = false,
        IReadOnlyList<XlightsAI.Studio.Rhythm.LeadInstrumentWindow>? leadInstrumentWindows = null,
        IReadOnlyList<MelodicContourSample>? melodicCurve = null, IReadOnlyList<SectionBandCurveSample>? bandCurve = null,
        SectionTimingCatalog? timingCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);

        var sb = new StringBuilder();
        sb.AppendLine("## This section");
        sb.AppendLine($"SectionId: {section.Id}");
        sb.AppendLine($"Kind: {context.Kind}");
        sb.AppendLine($"Bars: [{section.StartBarIndex},{section.EndBarIndexExclusive})");
        sb.AppendLine($"Real time: {FormatTimestamp(context.Window.StartTicks)}-{FormatTimestamp(context.Window.EndTicks)} (mm:ss, approximate to the nearest frame).");
        if (totalOccurrences > 1)
        {
            sb.AppendLine($"This is repeat {occurrenceIndex} of {totalOccurrences} sections classified as {context.Kind} in this song.");
            sb.AppendLine(occurrenceIndex == totalOccurrences
                ? "This is the LAST repeat of this Kind -- this is usually where a withheld Look/prop/palette-accent should finally appear, and/or the highest-energy Look available for this Kind. Do not treat it like an earlier, more restrained repeat."
                : "An earlier repeat of this same Kind exists elsewhere in the song -- favor restraint here (a lower-energy Look, fewer props, or holding an accent back) so the later repeat still has somewhere to go. Never assume this is the only occurrence.");
        }

        sb.AppendLine($"Declared energy ceiling: {context.EnergyCeiling:0.00}");
        if (!double.IsPositiveInfinity(context.TargetChangeRatePerSecond))
        {
            sb.AppendLine($"Target change-rate for this section: ~{context.TargetChangeRatePerSecond:0.00} effect starts/sec (derived from its own declared ceiling above -- a low target is exactly when the \"long base layer spanning most of this section\" guidance matters most, not a reason to leave the section empty).");
        }

        sb.AppendLine($"Vocals active for the majority of this section: {context.VocalActive}");
        sb.AppendLine($"Timing tracks available: {string.Join(", ", context.TimingTracksByName.Keys.OrderBy(k => k, StringComparer.Ordinal))}");
        AppendMelodicContourLine(sb, context, melodicBrief);
        AppendVocalNoteEmphasisLine(sb, salientVocalNotes);
        AppendTransientHitLine(sb, transientHits);
        AppendMotifLinkedPredecessorLine(sb, context, motifLink);
        AppendPhraseListLines(sb, phrases);
        AppendTimedCueLines(sb, context, timedCues);

        if (enableSectionTimelines)
        {
            AppendTimingMarks(sb, timingCatalog ?? SectionTimingCatalog.Build(context, phrases, transientHits, salientVocalNotes, leadInstrumentWindows));
            AppendLeadInstrumentWindowLine(sb, context, leadInstrumentWindows);
            AppendMelodicCurveLines(sb, melodicCurve);
            AppendBandCurveLines(sb, bandCurve);
            AppendCueConcurrencyGuidance(sb, context);
        }

        sb.AppendLine();
        sb.AppendLine(layout.ToPromptText(includeRelativePosition: enableSectionTimelines));

        // Build-plan Phase 14 §2d.2: the required JSON response contract below is kept stated AFTER
        // this block, not before -- so the required output shape is never something a user's own
        // creative-direction text could plausibly be read as overriding.
        CustomPromptBlockFormatter.AppendBlock(sb, customPrompt);

        sb.AppendLine();
        sb.AppendLine("## Filler effects (build-plan Phase 15 -- REQUIRED every section)");
        sb.AppendLine("Any prop/group this section does not otherwise animate still needs SOMETHING playing in its negative space (the \"always-moving\" rule -- no model holds frozen longer than one bar). You MUST declare \"fillerEffects\": a JSON array of at least 6 DISTINCT real effect names from this song's own vocabulary (the same effect names used as \"effect=\" values in the Look library above, e.g. \"On\", \"Bars\", \"SingleStrand\", \"Color Wash\", \"Twinkle\") -- never an invented name, and never fewer than 6 distinct ones (a name may repeat in the array to make it rotate less often, but the DISTINCT count must still be at least 6).");
        sb.AppendLine("You MAY also declare \"fillerEffectsByRole\": an object keyed by \"Foundation\"/\"Subject\"/\"Accent\" (the same foundation/subject/accent prop classification used elsewhere in this system), each value its own array of at least 6 distinct real effect names -- use this only when you want a DIFFERENT filler rotation for a specific role than the flat fillerEffects list (e.g. calmer filler on Foundation props, busier filler on Accent props); omit it (or leave it null/empty) to use the flat fillerEffects list for every prop.");
        if (customPrompt is { Length: > 0 })
        {
            sb.AppendLine("Respect this section's own custom creative direction above when choosing filler effects too -- an excluded effect must never appear in fillerEffects or fillerEffectsByRole either, the same as it must never appear in your lookId/propBindings choices.");
        }

        sb.AppendLine();
        sb.AppendLine("Respond with a single JSON object: {\"sectionId\":\"...\",\"lookId\":\"...\",\"variation\":null,\"propBindings\":[{\"layerZ\":1,\"props\":\"...\",\"annotation\":null}],\"phraseVariations\":null,\"fillerEffects\":[\"On\",\"Bars\",\"SingleStrand\",\"Color Wash\",\"Twinkle\",\"Shockwave\"],\"fillerEffectsByRole\":null}.");
        if (enableSectionTimelines)
        {
            sb.AppendLine("Optional \"cues\": [{\"kind\":\"hold|accent|takeover\",\"anchorId\":\"transient-2\",\"props\":\"...\",\"targets\":null,\"effect\":\"...\",\"intensity\":\"Off|Dim|Bright|Full\",\"role\":\"subject|support|secondary|foundation\",\"holdUntilAnchorId\":null,\"handoffFromProps\":null,\"annotation\":null}]. Use props for one model/group or targets for a list of real models/groups instead. Bound accents with the next intended end mark; use ordered members one at a time for a chase. Consider a matrix or canvas group for a continuous sweep, or several targets at a major shift without requiring a dominant hold. A takeover ends the running hold on its targets or HandoffFromProps. Omitted kind means accent.");
            sb.AppendLine("Build-plan Phase 28b.1: if you DO declare cues, do not put every one of them on the same single Props value -- see \"Minimum real Props breadth for this section\" above for the exact real count your cues plus this Look's own base layers must together reach.");
        }
        sb.AppendLine("Only choose a lookId whose declared section kinds include this section's Kind (or declares none, meaning unrestricted).");
        sb.AppendLine("Omit propBindings (or leave it an empty array) if the Look's own default props are fine for this section.");
        sb.AppendLine("If you do rebind a layer, \"props\" accepts a real group reference (e.g. \"group:Matrixes\") exactly like the Look's own default -- prefer it over an individual prop name whenever the whole group should carry the same effect, same as the Look library's own convention above.");
        sb.AppendLine("\"annotation\" on a propBindings entry is optional -- omit it (or set null) unless you are rebinding that layer's props AND have something grounded to say about what the rebound layer now expresses (see \"Layer annotations\" above).");
        sb.AppendLine("Omit phraseVariations (or leave it null/an empty array) unless this section's own phrase list above makes staging worth it (see \"Phrase-scoped sub-section staging\" above) -- when you do declare it, each entry is {\"phraseIndex\":2,\"activeLayerZs\":[1,2],\"propBindings\":[{\"layerZ\":2,\"props\":\"...\"}]}, at most 3 entries, phraseIndex 1-based and matching the numbered phrase list above.");
        sb.AppendLine("fillerEffects is REQUIRED (not optional like propBindings/phraseVariations/fillerEffectsByRole) -- a response missing it, or declaring fewer than 6 distinct names, will be rejected and you will be asked to correct it.");

        return sb.ToString();
    }

    /// <summary>
    /// Build-plan Phase 28a §4a: a direct, mechanical re-render of real timing marks this pipeline
    /// already computes -- <see cref="Choreography.SectionTimingMarks.Resolve"/>'s own enumeration,
    /// sorted by real tick -- as the lookup table a <c>SectionCue.AnchorId</c> cites. Appends nothing
    /// when no real marks resolve beyond the always-available <see cref="Choreography.SectionTimingMarks.SectionStartAnchorId"/>
    /// anchor alone (never an invented mark).
    /// </summary>
    private static void AppendTimingMarks(StringBuilder sb, SectionTimingCatalog catalog)
    {
        sb.AppendLine();
        sb.AppendLine("## Real timing marks in this section (use these exact AnchorId values -- never invent a timestamp)");
        var pairedIds = catalog.Sustains.SelectMany(s => new[] { s.StartId, s.EndId, s.ShortStartId, s.ShortEndId })
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var (id, ticks) in catalog.Marks.Where(m => !pairedIds.Contains(m.Key)).OrderBy(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"- {id} @ {FormatTimestamp(ticks)}");
        }
        if (catalog.Sustains.Count > 0)
        {
            sb.AppendLine("On these spans, keep the background steady instead of restarting it on every beat. A foreground hold is optional. If you choose one, set kind=hold and copy the paired IDs exactly into anchorId and holdUntilAnchorId.");
        }
        foreach (var span in catalog.Sustains)
        {
            var note = span.Note;
            sb.Append($"- Sustain {span.ShortStartId ?? span.StartId} to {span.ShortEndId ?? span.EndId}: {note.StartTicks / (double)TicksPerSecond:0.000}s–{note.EndTicks / (double)TicksPerSecond:0.000}s, {(note.EndTicks - note.StartTicks) / (double)TicksPerSecond:0.000}s, strength {note.Velocity:0.00}");
            if (span.EstimatedWord is { } word)
            {
                sb.Append($", estimated word: {word}");
            }
            if (span.ConcurrentHits.Count > 0)
            {
                sb.Append($", concurrent hits: {string.Join(", ", span.ConcurrentHits)}");
            }
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Build-plan Phase 28a §4d: mirrors <see cref="AppendTransientHitLine"/>/<see cref="AppendVocalNoteEmphasisLine"/>
    /// exactly -- renders only the real <see cref="XlightsAI.Studio.Rhythm.LeadInstrumentWindow"/>s whose
    /// own real time overlaps this section, so a cue can target the new <see cref="Choreography.AnchorKind.LeadInstrumentWindowStart"/>
    /// anchor directly instead of a lead-instrument moment only ever being checked post-compile.
    /// </summary>
    private static void AppendLeadInstrumentWindowLine(StringBuilder sb, SectionMusicContext context, IReadOnlyList<XlightsAI.Studio.Rhythm.LeadInstrumentWindow>? leadInstrumentWindows)
    {
        if (leadInstrumentWindows is not { Count: > 0 })
        {
            return;
        }

        var overlapping = leadInstrumentWindows
            .Where(w => w.StartTicks < context.Window.EndTicks && w.EndTicks > context.Window.StartTicks)
            .OrderBy(w => w.StartTicks)
            .ToList();
        if (overlapping.Count == 0)
        {
            return;
        }

        var times = overlapping.Select(w => FormatTimestamp(Math.Max(w.StartTicks, context.Window.StartTicks)));
        sb.AppendLine($"Lead-instrument prominence window(s) (real, measured -- a real lead instrument/section genuinely stands out here): {string.Join(", ", times)}.");
        sb.AppendLine("Consider landing a distinct hit/accent at one of these real moments via the \"LeadInstrumentWindowStart\" anchor kind above, same discipline as a transient hit or vocal-note onset.");
    }

    /// <summary>Build-plan Phase 28a §4c: renders <see cref="MelodicContourBriefBuilder.ComputeSectionCurve"/>'s own real, ordered within-section samples -- appends nothing when empty/null (never an invented curve).</summary>
    private static void AppendMelodicCurveLines(StringBuilder sb, IReadOnlyList<MelodicContourSample>? melodicCurve)
    {
        if (melodicCurve is not { Count: > 0 } samples)
        {
            return;
        }

        sb.AppendLine($"Melodic movement within this section (real note-onset samples, beat offset/direction/semitone interval): {string.Join(", ", samples.Select(s => $"beat {s.BeatOffset}:{s.Direction}{(s.IntervalSemitones != 0 ? $"({s.IntervalSemitones:+#;-#;0})" : string.Empty)}"))}.");
    }

    /// <summary>
    /// Build-plan Phase 28b.1: a real Carol of the Bells run found the model reliably gets cue
    /// MECHANICS right (anchors, hold-chains, dim-before-bright ordering) but defaults to putting every
    /// cue on a single Props value, never spreading breadth -- because the real distinct-Props floor
    /// (<see cref="ChoreographyLegalityValidator.RuleSectionModelBreadth"/>) was previously only ever
    /// surfaced as an opaque validator error AFTER the fact, never stated up front with anything like
    /// the same worked-example weight <see cref="AppendContrastDrivenEmphasisGuidance"/> already gives
    /// the contrast rule. This renders that same real number here, before generation, not only after a
    /// rejection.
    /// </summary>
    private static void AppendCueConcurrencyGuidance(StringBuilder sb, SectionMusicContext context)
    {
        var floor = ChoreographyLegalityValidator.MinDistinctModelsForSectionKind(context.Kind);
        sb.AppendLine();
        sb.AppendLine("## Minimum real Props breadth for this section (build-plan Phase 28b.1)");
        sb.AppendLine($"If you declare cues, your cues' own \"props\" values, counted together with whichever Look's own base layers you pick, must together name at least {floor} DISTINCT real Props/groups for this section's own kind -- the same real floor the compiled result is checked against. Putting every cue on one single Props value (e.g. four cues that all target the same one Matrix) is a real, common mistake this guidance exists to prevent: it produces a correct dim-before-bright pattern on that one prop, but still fails this section for having too few distinct Props overall. Spread your cues across more than one real Props value (or rely on the base Look's own other layers to cover the rest) whenever a single-prop cue sequence would leave this section under that floor.");
    }

    /// <summary>Build-plan Phase 28a §4c: renders <see cref="BandEnergyGroupRoleMapper.ComputeSectionCurve"/>'s own real, ordered sub-window samples -- appends nothing when empty/null (never an invented curve).</summary>
    private static void AppendBandCurveLines(StringBuilder sb, IReadOnlyList<SectionBandCurveSample>? bandCurve)
    {
        if (bandCurve is not { Count: > 0 } samples)
        {
            return;
        }

        sb.AppendLine($"Band-energy movement within this section (real sub-window samples, dominant band): {string.Join(" -> ", samples.Select(s => s.DominantBand ?? "none"))}.");
    }

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 1 (2026-11-01): one line per <see cref="TimedCue"/>
    /// already known to overlap this section (the caller filters; this method just renders), naming the
    /// cue's own approximate real-time window and its instruction verbatim. Appends nothing when
    /// <paramref name="timedCues"/> is <see langword="null"/>/empty -- never an invented cue.
    /// </summary>
    private static void AppendTimedCueLines(StringBuilder sb, SectionMusicContext context, IReadOnlyList<TimedCue>? timedCues)
    {
        if (timedCues is not { Count: > 0 })
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("## Timed cues from the user's own creative direction (approximate, not exact)");
        sb.AppendLine("The user asked for something specific to happen near each real-time moment below, which falls within (or close to the edge of) this section's own real time window above. Treat each timestamp as approximate, not frame-exact -- a hand-typed time is never going to land on the beat grid precisely. Where the instruction calls for a hard, instantaneous hit/flash, use a SHORT, discrete effect (not a long sustained one) placed as close as you reasonably can to the stated moment.");
        foreach (var cue in timedCues)
        {
            var rangeText = cue.End is { } end && end != cue.Start
                ? $"{FormatTimestamp(cue.Start)}-{FormatTimestamp(end)}"
                : FormatTimestamp(cue.Start);
            sb.AppendLine($"- Around {rangeText} (within about ±{cue.Tolerance.TotalSeconds:0.#}s): {cue.Instruction}");
        }
    }

    private static string FormatTimestamp(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    private static string FormatTimestamp(long ticks) => FormatTimestamp(TimeSpan.FromSeconds(ticks / (double)TicksPerSecond));

    /// <summary>
    /// Build-plan Phase 22 §2b: strong, repeated, worked-example guidance -- confirmed by the user as the
    /// PRIMARY mechanism for making a beat/note/word actually FEEL like an accent (never a one-line aside,
    /// and never a fallback to be superseded by the deterministic patch described in build-plan Phase 22
    /// §2d, which only repairs whatever real instant this guidance still misses). Lives in the STABLE
    /// prefix (unlike the per-section transient-hit/vocal-note-emphasis/timed-cue lines, which are
    /// necessarily volatile) because the rule itself never varies per section -- only which real instants
    /// it applies to does, and those are already named per-section below.
    /// </summary>
    private static void AppendContrastDrivenEmphasisGuidance(StringBuilder sb)
    {
        sb.AppendLine("## Contrast-driven emphasis -- READ THIS CAREFULLY, it is the single most important rule in this prompt");
        sb.AppendLine("A transition from one bright/full effect straight into another bright/full effect NEVER reads as an accent to a real viewer, no matter how precisely it lands on the beat. The ONLY thing that reads as a deliberate accent is a real intensity DROP immediately before the emphasis moment (dim, sparse, or literally off), followed by the emphasis itself landing bright/full. This is true for every kind of emphasis this prompt names below -- a real transient hit (a loud percussive beat), a real vocal-note onset (a sung note's own emphasis), and a user's own timed cue -- every single time one occurs, not just the first or most obvious one in a section.");
        sb.AppendLine("CONCRETE WORKED EXAMPLE: a section's own transient hits land at 0:42 and 0:44. WRONG: the same bright \"Color Wash\" effect plays straight through both hits with no change -- a viewer cannot tell either hit happened. RIGHT: the subject layer holds a Dim- or Off-tier effect (e.g. a sparse \"Twinkle\" at low duty, or literally nothing lit) for roughly the half-beat to one full beat immediately before 0:42, THEN switches to a Bright-tier effect (e.g. \"Color Wash\", \"Bars\", \"On\") exactly at 0:42 -- then repeats the same dim-then-bright pattern again before 0:44, every single time, not just once per section.");
        sb.AppendLine("This is the PRIMARY mechanism for making emphasis feel like emphasis -- not a nice-to-have on top of correct timing, and not something a later automated pass is relying on you to skip. When choosing a lookId/propBindings for a section that has real emphasis instants named below (transient hits, vocal-note emphasis, or timed cues), actively prefer whichever choice gives you this dim/off-then-bright shape on the prop(s) that actually carry the emphasis, over a choice that merely lands on the right timing with no real brightness change.");
        sb.AppendLine("This applies to EVERY recurring emphasis moment through the whole song, not just an occasional highlight -- a chorus hook that repeats four times needs this same dim-before-bright treatment on all four repeats, not only the first.");
        sb.AppendLine();
    }

    /// <summary>Build-plan Phase 10 §3e task 3: one numbered line per this section's own real phrase, e.g. "Phrase 1 of 4: bars [16,20)." -- the exact phraseIndex numbering a declared phraseVariations entry must reference. Appends nothing when <paramref name="phrases"/> is <see langword="null"/>/empty -- never an invented phrase list.</summary>
    private static void AppendPhraseListLines(StringBuilder sb, IReadOnlyList<PhraseCandidate>? phrases)
    {
        if (phrases is not { Count: > 0 })
        {
            return;
        }

        sb.AppendLine($"This section has {phrases.Count} phrase(s):");
        for (var i = 0; i < phrases.Count; i++)
        {
            var phrase = phrases[i];
            sb.AppendLine($"  Phrase {i + 1} of {phrases.Count}: bars [{phrase.StartBarIndex},{phrase.EndBarIndexExclusive}).");
        }
    }

    /// <summary>
    /// Build-plan Phase 10 §3b task 2: one short line per section, e.g. "Melodic contour: Rising, high
    /// note density, peak at beat 12 of 16." Appends nothing when <paramref name="melodicBrief"/> is
    /// <see langword="null"/> (no real melody data for this song) -- never an invented/placeholder line.
    /// </summary>
    private static void AppendMelodicContourLine(StringBuilder sb, SectionMusicContext context, SectionMelodicBrief? melodicBrief)
    {
        if (melodicBrief is not { } brief)
        {
            return;
        }

        var shapeText = brief.ContourShape switch
        {
            MelodicContourShape.Rising => "Rising",
            MelodicContourShape.Falling => "Falling",
            MelodicContourShape.Mixed => "Mixed",
            _ => "Static/Sparse",
        };

        var densityText = brief.NoteDensity switch
        {
            >= 1.0 => "high note density",
            <= 0.25 => "low note density",
            _ => "moderate note density",
        };

        var totalBeats = MelodicContourBriefBuilder.CountBeatsInWindow(context.BarGrid, context.Window);
        var peakClause = totalBeats > 0 ? $", peak at beat {brief.PeakNoteBeatOffset + 1} of {totalBeats}" : string.Empty;

        sb.AppendLine($"Melodic contour: {shapeText}, {densityText}{peakClause}.");
    }

    /// <summary>
    /// User-requested real investigation (2026-11-08): the real, isolated-vocal-stem, YIN-pitch-tracked
    /// note onsets this section's own loudest sung moments actually land on -- a categorically more
    /// reliable emphasis signal than lyric-word transcription (which this same investigation separately
    /// confirmed hallucinates on sung pop vocals even at the largest tested model size), since it comes
    /// from a real, deterministic monophonic pitch tracker, not a probabilistic language model. Appends
    /// nothing when <paramref name="salientVocalNotes"/> is <see langword="null"/>/empty (no melody data,
    /// or this section has no sounding notes) -- never an invented note list.
    /// </summary>
    private static void AppendVocalNoteEmphasisLine(StringBuilder sb, IReadOnlyList<XlightsAI.Studio.Melody.NoteEvent>? salientVocalNotes)
    {
        if (salientVocalNotes is not { Count: > 0 } notes)
        {
            return;
        }

        var times = notes.Select(n => FormatTimestamp(n.StartTicks));
        sb.AppendLine($"Vocal note emphasis (real, measured from the isolated vocal stem's own pitch track -- the loudest {notes.Count} sung-note onset(s) in this section, NOT lyric words): {string.Join(", ", times)}.");
        sb.AppendLine("Consider landing a quick accent effect (a pop, a brightness hit, a short overlay) at one or more of these exact real moments when this section's own creative intent calls for the lighting to visibly track the vocal -- these are measured onsets, not a guess.");
    }

    /// <summary>
    /// Build-plan Phase 22 §2b: the real, measured loud-transient onsets in this section -- the third
    /// named emphasis-signal type (alongside vocal-note emphasis above and timed cues below), surfaced here
    /// for the first time so the "dim/off before every real emphasis instant" guidance in the stable prefix
    /// has a concrete, real time to anchor a worked example against. Appends nothing when <paramref name="transientHits"/>
    /// is <see langword="null"/>/empty -- never an invented hit.
    /// </summary>
    private static void AppendTransientHitLine(StringBuilder sb, IReadOnlyList<XlightsAI.Studio.Rhythm.TransientHit>? transientHits)
    {
        if (transientHits is not { Count: > 0 } hits)
        {
            return;
        }

        var times = hits.OrderBy(h => h.TimeTicks).Select(h => FormatTimestamp(h.TimeTicks));
        sb.AppendLine($"Transient hits (real, measured loud percussive beats -- the strongest emphasis-worthy moments in this section): {string.Join(", ", times)}.");
        sb.AppendLine("Each one is a real candidate for the \"dim/off immediately before, bright/full exactly on the hit\" contrast pattern described above -- apply it on the prop(s) that already carry this section's own accent/subject layer at that moment, not every prop in the section.");
    }

    /// <summary>
    /// Build-plan Phase 10 §3c task 2: one short line naming the specific earlier section this section's
    /// own real motif-clustering data ties it to, e.g. "Motif link: this section's melodic motif also
    /// appears in the Chorus at bars [16,24) -- echo a recognizable visual signature from it, then escalate
    /// rather than repeat it identically." Appends nothing when <paramref name="motifLink"/> is
    /// <see langword="null"/>. Always rendered when a link exists, even when the linked predecessor shares
    /// this section's own Kind (§2d's own primary example is exactly this case -- "chorus repeats, a
    /// recurring hook") -- the existing "repeat N of M" text above never names a SPECIFIC earlier section
    /// by bar range, so this line is never fully redundant with it.
    /// </summary>
    private static void AppendMotifLinkedPredecessorLine(StringBuilder sb, SectionMusicContext context, SectionMotifLink? motifLink)
    {
        if (motifLink is not { } link)
        {
            return;
        }

        sb.AppendLine($"Motif link: this section's melodic motif also appears in the {link.LinkedPredecessorKind} at bars [{link.LinkedPredecessorStartBarIndex},{link.LinkedPredecessorEndBarIndexExclusive}) -- echo a recognizable visual signature from that section (same accent palette slot, same subject prop, or same signature effect), then escalate (more layers, higher duty, an added group) rather than rendering it identically.");
    }
}
