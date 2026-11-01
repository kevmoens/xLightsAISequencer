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
    public static string BuildStablePrefix(ShowBibleContent bible, DirectorKnowledgeContext knowledge, string style)
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

        sb.AppendLine("## Show Bible Look library");
        sb.AppendLine("Pick LookId from exactly these (id: intent, energyCeiling, motion, subject, paletteRef, allowed section kinds, variations):");
        foreach (var look in bible.Looks.Looks)
        {
            var allowed = look.AllowedSectionKindsOrEmpty.Count == 0 ? "any" : string.Join("/", look.AllowedSectionKindsOrEmpty);
            var variations = look.VariationsOrEmpty.Count == 0 ? "none" : string.Join("/", look.VariationsOrEmpty);
            sb.AppendLine($"- {look.Id}: \"{look.Intent}\" | ceiling={look.EnergyCeiling:0.00} | motion={look.Motion} | subject={look.Subject} | palette={look.PaletteRef} | sections={allowed} | variations={variations}");
            foreach (var layer in look.Layers.OrderBy(l => l.Z))
            {
                sb.AppendLine($"    layer z={layer.Z} role={layer.Role} effect={layer.Effect} props={layer.Props}");
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
    public static string BuildVolatileSuffix(
        SectionLabelCandidate section, SectionMusicContext context, LayoutSummary layout, int occurrenceIndex = 1, int totalOccurrences = 1,
        SectionMelodicBrief? melodicBrief = null, SectionMotifLink? motifLink = null, IReadOnlyList<PhraseCandidate>? phrases = null,
        string? customPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);

        var sb = new StringBuilder();
        sb.AppendLine("## This section");
        sb.AppendLine($"SectionId: {section.Id}");
        sb.AppendLine($"Kind: {context.Kind}");
        sb.AppendLine($"Bars: [{section.StartBarIndex},{section.EndBarIndexExclusive})");
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
        AppendMotifLinkedPredecessorLine(sb, context, motifLink);
        AppendPhraseListLines(sb, phrases);
        sb.AppendLine();
        sb.AppendLine(layout.ToPromptText());

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
        sb.AppendLine("Respond with a single JSON object: {\"sectionId\":\"...\",\"lookId\":\"...\",\"variation\":null,\"propBindings\":[{\"layerZ\":1,\"props\":\"...\"}],\"phraseVariations\":null,\"fillerEffects\":[\"On\",\"Bars\",\"SingleStrand\",\"Color Wash\",\"Twinkle\",\"Shockwave\"],\"fillerEffectsByRole\":null}.");
        sb.AppendLine("Only choose a lookId whose declared section kinds include this section's Kind (or declares none, meaning unrestricted).");
        sb.AppendLine("Omit propBindings (or leave it an empty array) if the Look's own default props are fine for this section.");
        sb.AppendLine("If you do rebind a layer, \"props\" accepts a real group reference (e.g. \"group:Matrixes\") exactly like the Look's own default -- prefer it over an individual prop name whenever the whole group should carry the same effect, same as the Look library's own convention above.");
        sb.AppendLine("Omit phraseVariations (or leave it null/an empty array) unless this section's own phrase list above makes staging worth it (see \"Phrase-scoped sub-section staging\" above) -- when you do declare it, each entry is {\"phraseIndex\":2,\"activeLayerZs\":[1,2],\"propBindings\":[{\"layerZ\":2,\"props\":\"...\"}]}, at most 3 entries, phraseIndex 1-based and matching the numbered phrase list above.");
        sb.AppendLine("fillerEffects is REQUIRED (not optional like propBindings/phraseVariations/fillerEffectsByRole) -- a response missing it, or declaring fewer than 6 distinct names, will be rejected and you will be asked to correct it.");

        return sb.ToString();
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
