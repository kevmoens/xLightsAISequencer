using System.Text;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>Everything a Director prompt is built from that is NOT the per-song volatile content (architecture §14's own "stable prefix" -- effect vocabulary, craft rules, exemplars, seed multi-layer templates).</summary>
/// <param name="Templates">
/// Build-plan Phase 10 §6b task 3: the shippable multi-layer Tree/Matrix template catalog
/// (<see cref="MultiLayerTemplateCatalogLoader.Load"/>) -- defaults to an empty catalog so every existing
/// caller that does not yet pass one keeps compiling, but a real composition root should always load and
/// pass the real one so the Director actually draws from it.
/// </param>
public sealed record DirectorKnowledgeContext(
    EffectVocabularyCatalog Vocabulary,
    CraftRules CraftRules,
    ExemplarIndex Exemplars,
    MultiLayerTemplateCatalog? Templates = null)
{
    public MultiLayerTemplateCatalog TemplatesOrEmpty => Templates ?? EmptyCatalog;

    private static readonly MultiLayerTemplateCatalog EmptyCatalog = new(0, DateTimeOffset.MinValue, []);
}

/// <summary>The two halves of one assembled Director call. <see cref="StablePrefix"/> must be byte-for-byte identical across every call sharing the same <see cref="DirectorKnowledgeContext"/>/style, or a provider's own prompt cache silently invalidates (architecture §14).</summary>
public sealed record DirectorPrompt(string StablePrefix, string VolatileSuffix);

/// <summary>
/// Build-plan Phase 5 task 3/4: "Assemble the Director prompt from MusicModel, Layout, style selection,
/// and retrieved exemplars with the effect vocabulary and craft rules as cached context... structure the
/// prompt so the large stable prefix... precedes the small per-song volatile content." The stable prefix
/// depends only on <see cref="DirectorKnowledgeContext"/> and <paramref name="style"/> (never on the
/// song itself), so two different songs compiled against the same corpus/style share byte-for-byte the
/// same prefix -- the exact property a provider's prompt cache keys on.
/// </summary>
public static class DirectorPromptBuilder
{
    private const int ExemplarsPerSectionKind = 2;

    public static DirectorPrompt Build(DirectorKnowledgeContext knowledge, string style, MusicModel music, LayoutSummary layout)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentException.ThrowIfNullOrWhiteSpace(style);
        ArgumentNullException.ThrowIfNull(music);
        ArgumentNullException.ThrowIfNull(layout);

        return new DirectorPrompt(BuildStablePrefix(knowledge, style, music), BuildVolatileSuffix(music, layout));
    }

    private static string BuildStablePrefix(DirectorKnowledgeContext knowledge, string style, MusicModel music)
    {
        var preset = StylePresetCatalog.Resolve(style);

        var sb = new StringBuilder();
        sb.AppendLine("You are the Director for an automated Christmas light show choreography system.");
        sb.AppendLine("Produce a Show Bible: a flat, reviewable creative plan for one song, as a single JSON object.");
        sb.AppendLine($"Style: {preset.Name}. {preset.Description}");
        sb.AppendLine();

        AppendRequiredJsonShape(sb);

        sb.AppendLine("## Style discipline (architecture §10 -- a coherent bundle, not a cosmetic preset)");
        sb.AppendLine($"Palette discipline: {preset.PaletteDiscipline.MinActiveSlots}-{preset.PaletteDiscipline.MaxActiveSlots} active slots. {preset.PaletteDiscipline.Description}");
        sb.AppendLine($"Density target: {preset.Density.MinChangeRatePerSecond:0.##}-{preset.Density.MaxChangeRatePerSecond:0.##} composition changes/second, duty cycle {preset.Density.MinDutyCycle:P0}-{preset.Density.MaxDutyCycle:P0}. {preset.Density.Description}");
        if (preset.PropRoleBias.Count > 0)
        {
            sb.AppendLine("Prop-role bias: " + string.Join(", ", preset.PropRoleBias.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value:0.0}")));
        }

        sb.AppendLine();
        sb.AppendLine("## Effect vocabulary");
        var effectNames = preset.EffectPool.Count > 0
            ? preset.EffectPool.Where(e => knowledge.Vocabulary.Effects.ContainsKey(e)).OrderBy(n => n, StringComparer.Ordinal).ToList()
            : knowledge.Vocabulary.Effects.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
        sb.AppendLine(preset.EffectPool.Count > 0
            ? "This style's own preferred effect pool -- every Look layer's Effect should be one of these:"
            : "Every Look layer's Effect must be exactly one of these real xLights effect names:");
        sb.AppendLine(string.Join(", ", effectNames));
        sb.AppendLine();

        sb.AppendLine("## Show length");
        sb.AppendLine(ShowLengthGuidance.PromptText);
        sb.AppendLine();

        sb.AppendLine("## Baseline craft guidance");
        sb.AppendLine("Always true, independent of any corpus rules below:");
        sb.AppendLine("- Not every prop/group needs to be lit every second. Deliberate negative space -- some props dark or idle while others carry a section -- reads as composed; everything active all the time reads as cluttered and amateur.");
        sb.AppendLine("- Vary which effect carries each Look. A single effect (e.g. Color Wash) repeated across most Looks reads as static regardless of color changes -- prefer a genuine mix across the library.");
        sb.AppendLine("- Match perceived motion to the section's actual energy. A high-energy/chorus/drop section needs effects that visibly track the beat (bound to a labelled timing track, short duty cycles, chase/strobe-family motion) -- a slow color wash under an intense section reads as unreactive no matter how bright or saturated it is, even at a high EnergyCeiling.");
        sb.AppendLine("- Design for pops and build-ups, not a flat energy floor. A pop is a sudden, unmistakable jump (a hard cut to full brightness/full canvas after a held or sparse moment) -- it needs the quiet moment right before it to land; a build-up is a deliberate climb (adding props/density/brightness over several bars, never all at once) that resolves into that pop or into a chorus reveal. Every song needs both somewhere, usually more than once.");
        sb.AppendLine("- The section list below may classify long stretches of this song under the same Kind (common for a repetitive/loop-based song) -- never treat \"same Kind\" as \"identical treatment.\" Within a run of same-Kind sections, still plan real variation: hold back a Look/prop/palette-accent for a later repeat so it reads as an escalation, and use PeakMoments/Withholding to mark which specific repeat is THE peak, not every one of them equally.");
        sb.AppendLine("- A \"hold\"-motion layer is a steady state, never a dead one. Confirmed against a real exported show: a layer using the plain \"On\" effect, reused across several consecutive sections for the same subject/group, renders as one long, visually unbroken, boring block -- exactly as lifeless as the bare negative-space problem above, just on the opposite (always-on) extreme. \"On\" has no animation of its own, so reserve it for brief transitional moments (well under a bar) -- never as the sole, long-duration content of a \"hold\" layer or a Look reused across many consecutive sections. For an actual held/steady look, pick an effect with its own real per-frame animation instead (Color Wash, Twinkle, SingleStrand, Bars, ...) -- it still reads as calm and steady at a low duty cycle, but it is never a single dead frame stretched over a minute.");
        sb.AppendLine("- Give every section's subject layer a long-duration base layer spanning most of that section's own window -- a real hand-sequenced reference for this exact craft brief runs one steady color-wash/VU-meter-style base layer across nearly the whole song, with short accent effects layered on top of it, never standing alone. A generation that compiles only short discrete hits (a handful of seconds each, repeated) has no anchor for a listener's eye to settle on during a quiet stretch, even if the hits themselves are well-placed. This is additive, not a replacement: keep the short accent/overlay layers this guidance already calls for elsewhere -- bind one of a Look's own layers (ordinarily the subject, timing-track-bound in Mode A, e.g. a long Color Wash/Twinkle/SingleStrand spanning the whole section) to be that base, and let the rest ride on top of it.");
        sb.AppendLine("- Stack real layers on high-resolution canvas props, not just one effect at a time. Tree and Matrixes/Matrix-2/Matrix3 (and similarly dense multi-node props) have enough pixels to carry a base effect PLUS one or two overlay effects simultaneously (Z-ordered layers, blended with Additive/Max/Average so the base shows through) -- a single flat effect on these props under-uses them and reads as plain motion rather than genuine texture. Favor Looks with 2-3 layers on these specific props: a slower base wash or chase for the subject's color/identity, plus a faster-moving accent layer (Twinkle, Sparkles, Snowflakes) riding on top for sparkle and detail. This is not occasional set-dressing -- for Tree/Matrixes specifically, a single-layer Look should be the exception, reserved for quiet/sparse moments, not the default. Low-resolution props (short strings, small accent models) should usually stay single-layer -- they rarely have enough pixels for a second layer to read as anything but noise.");
        sb.AppendLine("- Use Butterfly sparingly. It is a valid effect, not a forbidden one, but it reads as a strong, busy, high-duty-cycle signature move -- reserve it for a genuine rare moment (a specific peak/pop), not a default go-to for a hold layer or a Tree/Matrixes accent layer. When in doubt, prefer Twinkle, Sparkles, Snowflakes, Color Wash, or SingleStrand instead.");
        sb.AppendLine("- User feedback: increase the overall number of effects and layers across the whole show, not just on Tree/Matrixes. Layering is not limited to those two props -- any prop or group with enough resolution for a second layer to read as real texture (not noise) benefits from one during a Chorus/Drop/peak section: a base effect carrying color/identity plus a brighter or faster accent layer on top. Push for MORE total Looks using 2+ layers, MORE distinct Looks overall (use more of the MinLooks-MaxLooks range, not the floor), and faster composition-change rates toward the upper end of this style's own density band below -- while still respecting negative space during genuinely quiet sections. \"More layered/more frequent\" and \"everything on constantly\" are not the same goal; the first makes high-energy sections feel richer, the second just reads as noise.");
        sb.AppendLine("- A stated minimum, not just \"more\": a real hand-sequenced reference for this exact craft brief ran up to 8 concurrent EffectLayers on its richest prop and 3 on typical secondary props, against a generated show that peaked at 3 on its richest prop and 0-2 on secondary ones. Concretely: a Chorus/Drop/peak-energy Look's subject prop should carry 3 or more concurrent layers (the multi-layer template catalog below demonstrates real corpus-derived 2-layer patterns at every energy band; this floor pushes higher specifically for peak-energy sections, on top of that catalog, not instead of it); a Verse/ballad/low-energy Look's subject prop should still carry at least 2 (one steady base, one accent) -- never a bare single-layer Look outside a genuinely sparse/negative-space moment. Secondary props on a Chorus/Drop/peak section should carry 2-3 layers where their own resolution supports it, not just the subject.");
        sb.AppendLine("- Also use a genuinely wide slice of the real effect vocabulary above, not a narrow handful repeated everywhere: the same reference used 22 distinct effect names across the whole song, with its two most-used effects together covering only about half of all effects; a generated show that leans on 2 effects for well over a third of everything reads as monotone regardless of how its colors or timing vary. Spread Look design across many different effects from the vocabulary -- reserve any one effect (however good it looks) for a clear minority of this song's own Looks.");
        sb.AppendLine();

        AppendMultiLayerTemplates(sb, knowledge.TemplatesOrEmpty);

        sb.AppendLine("## Verified craft rules");
        sb.AppendLine("Rules below were induced from a real sequence corpus and mechanically re-verified; follow them.");
        if (knowledge.CraftRules.Promoted.Count == 0)
        {
            sb.AppendLine("(None available for this run -- corpus mining has not been run yet. Rely on the baseline guidance above.)");
        }

        foreach (var rule in knowledge.CraftRules.Promoted.OrderByDescending(r => r.Confidence))
        {
            sb.AppendLine($"- {rule.Statement} (confidence {rule.Confidence:P0}, seen in {rule.SongCount} songs)");
        }

        sb.AppendLine();
        sb.AppendLine("## Retrieved exemplars");
        sb.AppendLine("Real corpus passages most similar to this song's own sections, as worked examples:");
        foreach (var kind in Enum.GetValues<SectionKind>())
        {
            var query = new ExemplarFingerprint(
                SongTitle: "(query)", Provenance: CorpusProvenanceTag.UserAuthored,
                StartBarIndex: 0, EndBarIndexExclusive: 0,
                TempoBpm: AverageBpm(music), EnergyPercentile: 0.5, SectionKind: kind,
                BandBalance: null, VocalActivityHint: null);

            foreach (var exemplar in knowledge.Exemplars.FindSimilar(query, ExemplarsPerSectionKind))
            {
                sb.AppendLine($"- [{kind}] \"{exemplar.Fingerprint.SongTitle}\" bars {exemplar.Fingerprint.StartBarIndex}-{exemplar.Fingerprint.EndBarIndexExclusive}, {exemplar.Fingerprint.TempoBpm:0} BPM: {exemplar.Effects.Count} effects placed.");
            }
        }

        return sb.ToString();
    }

    private static string BuildVolatileSuffix(MusicModel music, LayoutSummary layout)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## This song");
        sb.AppendLine($"Duration: {TimeSpan.FromTicks(music.SongDurationTicks).TotalSeconds:0.0}s.");

        var tempo = music.BeatGrid.SelectedTempo;
        sb.AppendLine(tempo is not null
            ? $"Tempo: {tempo.Bpm:0.0} BPM ({tempo.MeterGuess})."
            : "Tempo: not confidently detected.");

        sb.AppendLine($"Phrase period: {(music.Phrases.Phrases.Count > 0 ? music.Phrases.Phrases[0].Bars : 0)} bars (measured per song, not assumed).");

        var bandProfiles = BandEnergyGroupRoleMapper.ComputeSectionProfiles(music);
        var bandProfilesByStartBar = bandProfiles.ToDictionary(p => p.StartBarIndex);

        sb.AppendLine("Sections:");
        foreach (var section in music.Sections.Labels.OrderBy(l => l.StartBarIndex))
        {
            var bandSuffix = music.BandEnergy is not null && bandProfilesByStartBar.TryGetValue(section.StartBarIndex, out var profile) && profile.DominantBand is { } dominant
                ? $" -- {dominant}-dominant (bass {profile.BassShare:P0}, mid {profile.MidShare:P0}, high {profile.HighShare:P0})"
                : string.Empty;
            sb.AppendLine($"- {section.Kind} bars [{section.StartBarIndex},{section.EndBarIndexExclusive}){bandSuffix}");
        }

        if (music.VocalActivity is { } vocals)
        {
            sb.AppendLine($"Vocal-active regions: {vocals.ActiveRegions.Count}.");
        }

        sb.AppendLine();
        sb.AppendLine(layout.ToPromptText());

        AppendBandEnergyGroupRoleMapping(sb, music, layout, bandProfiles);

        return sb.ToString();
    }

    /// <summary>
    /// Build-plan Phase 10 §2b/§3d: a named, concrete, song-grounded version of the bass->foundation/
    /// mid->subject/highs->accent rule, naming the real foundation/subject/accent groups from
    /// <paramref name="layout"/> and this song's own measured band balance (<see cref="BandEnergyGroupRoleMapper.ComputeSongWideShare"/>)
    /// -- so group-role assignment is a standing rule for the whole song, consistent with this style's own
    /// <see cref="StylePreset.PropRoleBias"/> weights, rather than re-decided per Look. Appends nothing
    /// when there is no real <see cref="MusicModel.BandEnergy"/> to ground it in, or the layout has no
    /// group/model name recognizable as any of the three roles.
    /// </summary>
    private static void AppendBandEnergyGroupRoleMapping(StringBuilder sb, MusicModel music, LayoutSummary layout, IReadOnlyList<SectionBandShare> bandProfiles)
    {
        if (music.BandEnergy is null)
        {
            return;
        }

        var classified = BandEnergyGroupRoleMapper.Classify(layout);
        var foundation = classified.Where(c => c.Role == BandRole.Foundation).Select(c => c.Reference).ToList();
        var subject = classified.Where(c => c.Role == BandRole.Subject).Select(c => c.Reference).ToList();
        var accent = classified.Where(c => c.Role == BandRole.Accent).Select(c => c.Reference).ToList();

        if (foundation.Count == 0 && subject.Count == 0 && accent.Count == 0)
        {
            return;
        }

        var (bassShare, midShare, highShare) = BandEnergyGroupRoleMapper.ComputeSongWideShare(bandProfiles);

        sb.AppendLine();
        sb.AppendLine("## Band-to-group mapping (measured for this song -- a standing rule, not re-decided per Look)");
        sb.AppendLine($"This song's own measured band balance (song-wide average across its sections): bass {bassShare:P0}, mid {midShare:P0}, high {highShare:P0}.");
        sb.AppendLine("Give each frequency band a consistent physical home for the whole song, the same way a real DJ/VJ assigns kick/bass/lead their own visual layer:");
        if (foundation.Count > 0)
        {
            sb.AppendLine($"- Bass -> foundation groups ({string.Join(", ", foundation)}): a full-canvas pulse/flash reading as impact, not detail.");
        }
        if (subject.Count > 0)
        {
            sb.AppendLine($"- Mids (melody/harmony) -> subject prop(s) ({string.Join(", ", subject)}): the Look's subject layer should track the melodic line's energy here.");
        }
        if (accent.Count > 0)
        {
            sb.AppendLine($"- Highs (hats, sparkle, air) -> accent groups ({string.Join(", ", accent)}): the natural home for a Twinkle/Sparkles-family overlay layer.");
        }
        sb.AppendLine("Keep this mapping consistent across the whole Look library -- it is what makes the show read as mixed rather than flat.");
    }

    /// <summary>
    /// Confirmed against real model calls (both a local Ollama model and real Anthropic Claude): with no
    /// explicit schema, a model reasonably invents its own shape (e.g. adding a "title" field), which the
    /// deliberately-strict deserializer (<see cref="StructuredJsonAiClient{TResponse}"/>'s
    /// <c>JsonUnmappedMemberHandling.Disallow</c>) then rejects -- the retry loop forwards only the single
    /// violating field's name, not the full correct shape, so a model can spend all its retry attempts
    /// fixing one field at a time rather than converging. This schema is hand-maintained to match
    /// <see cref="ShowBibleContent"/>/<see cref="LookLibrary"/>/<see cref="Look"/>'s real C# shape exactly
    /// (property names are case-insensitive on read, but must otherwise match exactly, no more, no fewer)
    /// -- keep it in sync with those types rather than letting it drift.
    /// </summary>
    private static void AppendRequiredJsonShape(StringBuilder sb)
    {
        sb.AppendLine("## Required JSON shape (exact -- no extra top-level or nested keys, no title/summary/commentary fields)");
        sb.AppendLine("Output ONLY the JSON object below, with exactly these keys at each level. Omit optional keys entirely rather than setting them to null, except where shown as nullable.");
        sb.AppendLine($"Provide exactly {ShowBibleValidator.MinPalettes}-{ShowBibleValidator.MaxPalettes} palettes and exactly {ShowBibleValidator.MinLooks}-{ShowBibleValidator.MaxLooks} Looks -- not fewer, not more.");
        sb.AppendLine("You do not know each effect's own real parameter names (they vary per effect and are not listed here) -- OMIT \"Params\" entirely on every layer unless you are certain of a real setting key; every effect already has sensible defaults, so an empty Params is always safe and preferred over a guessed one.");
        sb.AppendLine("\"Subject\"/\"Props\" accept either a real individual prop name OR a real group reference from the Layout section below (e.g. \"group:Matrixes\", \"group:MiniTrees\", \"group:Outline\") -- use a group reference when every member of that group should genuinely carry the exact same effect at the same time (it compiles to one real, editable group-level effect instead of silently repeating the same effect once per member model). This is a MIX, not a default: a Look library that leans on \"group:Everything\"/a couple of broad groups for most of its Looks is exactly as wrong as one that never uses a group at all -- individual high-value props (the matrices, the tree, a hero arch) deserve their own direct moments too, especially for a peak/solo Look naming one specific subject. Vary it deliberately across the library, the same way a real designer alternates between a full-canvas moment and a single prop carrying a line.");
        sb.AppendLine("Reach across the WHOLE real prop/group list below over the course of the Look library, not just the handful of broad groups and heaviest individual props -- a prop that never appears as any Look's own Subject/Props (not even inside a group) never gets its own moment in the finished show. Spread deliberate attention across small/outline props too (an arch, a mini tree, the front door), not only the matrices and the tree.");
        sb.AppendLine("A literal \"moving from one prop to the next\" sweep (light prop A, then B, then C in turn) is NOT something a single group-targeted effect can produce -- a group effect always treats every member identically and simultaneously. For that specific traveling effect, declare \"PropDelays\" on a Look whose Props/Subject is a group with genuinely different-sized members (mass/mid/accent tiers by real pixel weight -- e.g. \"{\\\"mass\\\":\\\"+0ms\\\",\\\"mid\\\":\\\"+80ms\\\",\\\"accent\\\":\\\"+160ms\\\"}\"), which staggers each tier's own reveal instead of firing every member at once -- reserve this for Looks where that graduated, traveling reveal is the actual intent, not every Look using a group.");
        sb.AppendLine("""
            {
              "Palettes": [ { "Id": "string", "Name": "string", "Colors": [ { "HexColor": "#RRGGBB", "Role": "base|key|accent" } ] } ],
              "PropRoles": [ { "Target": "string (a real prop or group name)", "MusicalRole": "bass|melody|highs" } ],
              "Looks": {
                "Looks": [
                  {
                    "Id": "string, unique",
                    "Name": "string",
                    "Intent": "one sentence creative intent",
                    "EnergyCeiling": 0.0,
                    "Motion": "hold|chase|build|pop|decay",
                    "PaletteRef": "must match a Palettes[].Id above",
                    "Subject": "the one prop/group this Look's subject layer targets",
                    "Layers": [
                      {
                        "Z": 0,
                        "Props": "a prop or group name",
                        "Effect": "must be exactly one of the effect vocabulary names below",
                        "Role": "subject|support|secondary",
                        "Blend": "Normal|Bright|Additive|Max",
                        "Duty": 0.0,
                        "Timescale": "Beats|Bars|Phrases (optional, omit if unused -- use these exact real track names, not a paraphrase like \"beat\" or \"bar\")"
                      }
                    ],
                    "Variations": ["optional array of strings, omit if none"],
                    "DeclaredDarkness": false,
                    "AllowedSectionKinds": ["optional array, e.g. Intro, Verse, PreChorus, Chorus, Bridge, Solo, Outro -- omit for unrestricted"],
                    "TemplateRef": "optional -- the Id of a seed multi-layer template (from the list below) this Look's Layers were directly modeled on; omit entirely when this Look does not draw on any of them"
                  }
                ]
              },
              "EnergyArc": [ { "Kind": "Intro|Verse|PreChorus|Chorus|Bridge|Solo|Outro", "EnergyCeiling": 0.0, "TargetChangeRatePerSecond": 0.0, "TargetHueFamilyCount": 1 } ],
              "PeakMoments": [ { "LookId": "must match a Looks[].Id above", "Distinction": "what makes this peak unmistakable" } ],
              "SignatureGestures": [ { "Name": "string", "Description": "string" } ],
              "Withholding": { "HeldBackLookIds": ["Look ids held back until the final chorus"], "FinalChorusAce": "what the withheld ace is" }
            }
            """);
        sb.AppendLine();
    }

    /// <summary>
    /// Build-plan Phase 10 §6b task 3: names the shippable seed multi-layer templates in the Director's
    /// own stable prefix, so a Tree/Matrixes Look can be modeled directly on one of these real, derived
    /// patterns instead of the Director inventing a layer composition from scratch every time. Appends
    /// nothing when no catalog was supplied (this run's own disclosed-empty-catalog case, same
    /// convention as the "corpus mining has not been run yet" message above for craft rules).
    /// </summary>
    private static void AppendMultiLayerTemplates(StringBuilder sb, MultiLayerTemplateCatalog templates)
    {
        sb.AppendLine("## Seed multi-layer Tree/Matrix templates");
        sb.AppendLine("Generic, palette-free, song-free layer-composition patterns derived from real multi-layer sequences (never a literal palette or song-specific timing) -- model a Tree/Matrixes/Mega Tree Look's own Layers directly on one of these when it fits the section's energy, and set that Look's \"TemplateRef\" to the template's Id below.");
        sb.AppendLine("REQUIRED: at least one Look in this Show Bible whose Subject or a Layer's Props targets Tree/Matrix/Matrixes/Mega Tree must directly copy one template's own Layer shape (same Effect per Role, Duty inside its declared range) from the list below and declare that template's exact Id as \"TemplateRef\" -- pick whichever template's EnergyBand best matches that Look's own EnergyCeiling/section. Every other Look is free to omit \"TemplateRef\" when it does not draw on any of these.");
        if (templates.Templates.Count == 0)
        {
            sb.AppendLine("(None available for this run.)");
            sb.AppendLine();
            return;
        }

        foreach (var template in templates.Templates)
        {
            sb.AppendLine($"- Id \"{template.Id}\" -- {template.Name} ({template.EnergyBand} energy, props: {string.Join("/", template.Props)}, motion: {template.Motion}):");
            foreach (var layer in template.Layers)
            {
                sb.AppendLine($"  - {layer.Role}: {layer.Effect}, duty {layer.DutyMin:0.00}-{layer.DutyMax:0.00}, cadence {layer.Cadence}. {layer.Notes}");
            }
        }

        sb.AppendLine();
    }

    private static double AverageBpm(MusicModel music) => music.BeatGrid.SelectedTempo?.Bpm ?? 0;
}
