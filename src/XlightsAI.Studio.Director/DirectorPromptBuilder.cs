using System.Text;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>Everything a Director prompt is built from that is NOT the per-song volatile content (architecture §14's own "stable prefix" -- effect vocabulary, craft rules, exemplars, seed multi-layer templates).</summary>
/// <param name="Templates">
/// Build-plan Phase 10 §6b task 3: the shippable multi-layer Tree/Matrix template catalog
/// (<see cref="MultiLayerTemplateCatalogLoader.Load"/>) -- defaults to an empty catalog so every existing
/// caller that does not yet pass one keeps compiling, but a real composition root should always load and
/// pass the real one so the Director actually draws from it.
/// </param>
/// <param name="EffectVisualCharacters">
/// Build-plan Phase 18 §6 (Phase E): the shippable, committed catalog of real AI-observed (and, for a
/// documented few, manual.xlights.org-sourced) visual descriptions per effect
/// (<see cref="Knowledge.EffectVisualCharacterCatalogLoader.Load"/>) -- defaults to <see langword="null"/>
/// so every existing caller that does not yet pass one keeps compiling and renders byte-for-byte the same
/// "## Effect vocabulary" section as before this phase; a real composition root should load and pass the
/// real embedded catalog so the Director's effect vocabulary is actually enriched.
/// </param>
/// <param name="PatternLibrary">
/// Build-plan Phase 20 §2c: the real, measured <see cref="Pipeline.PatternLibrary"/> a prior
/// <c>CorpusMiningRunner</c> run computed and persisted (<c>DensityBySection</c>/
/// <c>LayerCountPerModelDistribution</c> in particular) -- defaults to <see langword="null"/> so every
/// existing caller that does not yet pass one keeps compiling; a real composition root should load and
/// pass the real one (same <c>FileNotFoundException</c>-falls-back-to-empty convention as
/// <see cref="CraftRules"/>/<see cref="Exemplars"/>) so the Director's own baseline guidance above is
/// cross-checked against what real shows actually do, not left as pure hand-tuned prose.
/// </param>
/// <param name="WholeCanvasCoOccurrence">
/// Build-plan Phase 25 Part B.2/C: the real, measured <see cref="Pipeline.WholeCanvasCoOccurrenceStats"/>
/// a prior <c>CorpusTrainingRunner</c> run computed over external (non-exemplar-eligible) sequences --
/// defaults to <see langword="null"/> so every existing caller that does not yet pass one keeps compiling;
/// a real composition root should load it (from a persisted <c>CorpusTrainingReport</c>) so
/// <see cref="DirectorPromptBuilder.AppendWholeCanvasGroupSection"/> can state concrete, measured usage
/// guidance instead of withholding it pending real data.
/// </param>
/// <param name="LayerComplexity">
/// Build-plan Phase 25 Part C §3 bullet 3/loose end: the real, measured <see cref="Pipeline.LayerComplexityStats"/>
/// the same prior <c>CorpusTrainingRunner</c> run computed -- defaults to <see langword="null"/> so every
/// existing caller that does not yet pass one keeps compiling; a real composition root should load it so
/// <see cref="DirectorPromptBuilder.AppendLayerComplexityGuidance"/> can ground how often the Director is
/// told to reach for a genuinely layered moment in real numbers instead of pure hand-tuned prose.
/// </param>
public sealed record DirectorKnowledgeContext(
    EffectVocabularyCatalog Vocabulary,
    CraftRules CraftRules,
    ExemplarIndex Exemplars,
    MultiLayerTemplateCatalog? Templates = null,
    EffectVisualCharacterCatalog? EffectVisualCharacters = null,
    PatternLibrary? PatternLibrary = null,
    WholeCanvasCoOccurrenceStats? WholeCanvasCoOccurrence = null,
    LayerComplexityStats? LayerComplexity = null)
{
    public MultiLayerTemplateCatalog TemplatesOrEmpty => Templates ?? EmptyCatalog;

    public PatternLibrary PatternLibraryOrEmpty => PatternLibrary ?? EmptyPatternLibrary;

    public LayerComplexityStats LayerComplexityOrEmpty => LayerComplexity ?? EmptyLayerComplexity;

    private static readonly MultiLayerTemplateCatalog EmptyCatalog = new(0, DateTimeOffset.MinValue, []);

    private static readonly PatternLibrary EmptyPatternLibrary = new(
        GeneratedAtUtc: DateTimeOffset.MinValue, SongCount: 0, TotalEffectCount: 0,
        EffectFrequency: new Dictionary<string, int>(), EffectFrequencyBySection: new Dictionary<SectionKind, IReadOnlyDictionary<string, int>>(),
        DensityBySection: new Dictionary<SectionKind, double>(), LayerCountPerModelDistribution: new Dictionary<int, int>(),
        BlendModeUsage: new Dictionary<string, int>(), PaletteActiveSlotDistribution: new Dictionary<int, int>(),
        ModeACount: 0, ModeBCount: 0);

    private static readonly LayerComplexityTierStats EmptyTier = new(0, new Dictionary<string, int>(), null, null);

    private static readonly LayerComplexityStats EmptyLayerComplexity = new(EmptyTier, EmptyTier, EmptyTier);
}

/// <summary>
/// The two halves of one assembled Director call. <see cref="StablePrefix"/> must be byte-for-byte
/// identical across every call sharing the same <see cref="DirectorKnowledgeContext"/>/style, or a
/// provider's own prompt cache silently invalidates (architecture §14) -- with one deliberate exception:
/// <see cref="CustomGenerationPrompt.StayCloseToExemplars"/> set to <see langword="false"/> intentionally
/// makes the "## Retrieved exemplars" section of the prefix vary call to call (see
/// <see cref="ExemplarIndex.FindVaried"/>), trading that provider cache hit for actual effect-choice
/// variety on a user-requested "regenerate" -- an explicit, disclosed opt-out, not an oversight.
/// </summary>
/// <param name="ExemplarReuse">
/// Build-plan Phase 25 Part E.1: every exemplar draw this call's own "## Retrieved exemplars" section
/// made, recorded by the same <see cref="ExemplarReuseTracker"/> instance across the whole stable-prefix
/// build -- see <see cref="ShowBibleValidator.ValidateExemplarReuse"/> for how this is validated.
/// </param>
public sealed record DirectorPrompt(string StablePrefix, string VolatileSuffix, ExemplarReuseReport? ExemplarReuse = null);

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
    /// <summary>
    /// Build-plan Phase 16 §2d: replaces the old flat <c>ExemplarsPerSectionKind = 2</c> constant with a
    /// count that scales with how much real material exists for <paramref name="kind"/> -- one more
    /// requested exemplar per 20 real same-kind entries in the index, floored at 1 (so even a zero-entry
    /// kind still asks for one, surfacing <see cref="ExemplarIndex.FindSimilar"/>'s disclosed nearest-kind
    /// fallback rather than silently asking for nothing) and capped at 5 so the stable prefix does not
    /// grow unboundedly as later corpus-expansion phases add more songs. The exact curve/cap is not a
    /// confirmed design decision (build plan §2d) -- a simple, defensible default disclosed here, not a
    /// re-opened question.
    /// </summary>
    public static int ExemplarCountForKind(ExemplarIndex index, SectionKind kind)
    {
        var available = index.Entries.Count(e => e.Fingerprint.SectionKind == kind);
        return Math.Clamp(available / 20, 1, 5);
    }

    /// <param name="customPrompt">
    /// Build-plan Phase 14 §2c: the user's own free-text creative direction for this run, or
    /// <see langword="null"/>/empty for the pre-existing default behavior (every caller before this
    /// phase, and every caller that simply has none). Folded into <see cref="BuildVolatileSuffix"/>
    /// only, never <see cref="BuildStablePrefix"/> -- a per-run, user-authored value is exactly the
    /// kind of input §1b says must never be baked into the cacheable prefix.
    /// </param>
    public static DirectorPrompt Build(DirectorKnowledgeContext knowledge, string style, MusicModel music, LayoutSummary layout, CustomGenerationPrompt? customPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentException.ThrowIfNullOrWhiteSpace(style);
        ArgumentNullException.ThrowIfNull(music);
        ArgumentNullException.ThrowIfNull(layout);

        // User feedback (2026-10-31): unchecking "stay close to exemplars" deliberately makes this run's
        // own exemplar retrieval non-reproducible (see CustomGenerationPrompt.StayCloseToExemplars and
        // ExemplarIndex.FindVaried) -- a fresh, unseeded Random here means two Build calls for the same
        // song in "varied" mode draw different exemplars, which is the entire point of the toggle; it
        // would defeat that purpose to make this Random deterministic/seeded from the song itself.
        var stayCloseToExemplars = customPrompt?.StayCloseToExemplars ?? true;
        var reuseTracker = new ExemplarReuseTracker();
        var stablePrefix = BuildStablePrefix(knowledge, style, music, stayCloseToExemplars, stayCloseToExemplars ? null : new Random(), reuseTracker);
        return new DirectorPrompt(
            stablePrefix,
            BuildVolatileSuffix(music, layout, customPrompt, knowledge.WholeCanvasCoOccurrence),
            reuseTracker.BuildReport());
    }

    /// <summary>
    /// Build-plan Phase 25 Part D.4/loose end: a <see cref="SectionKind"/> whose own
    /// <see cref="DefaultEnergyCeilings.ArchitectureSection10"/> ceiling reaches this threshold is a
    /// genuine peak/featured moment (Chorus/Solo) -- exactly the kind of section the baseline craft
    /// guidance above already tells the Director to stack 3+ concurrent layers for. Requesting a real
    /// <see cref="LayerComplexityAnalyzer.DefaultMinDistinctLayers"/>+ exemplar for these kinds specifically
    /// (rather than leaving layer complexity an accidental byproduct of plain similarity ranking) is the
    /// one structural signal available at stable-prefix-build time -- before any Show Bible exists -- that
    /// stands in for "a section the show bible flags for added density."
    /// </summary>
    private const double ComplexityRequestCeilingThreshold = 0.75;

    /// <summary>
    /// User feedback (2026-11-12): the baseline craft guidance above already demands "a Verse/ballad/
    /// low-energy Look's subject prop should still carry at least 2" layers, but until now only kinds at
    /// or above <see cref="ComplexityRequestCeilingThreshold"/> (Chorus/Solo) ever requested a complexity
    /// floor at all -- every other kind (Verse, PreChorus, Bridge, Intro, Outro, Unknown) requested a
    /// plain similarity match with no floor, so the single-layer-dominant pool could win even where a
    /// real 2+-layer exemplar existed in the index. This is that floor for every non-peak kind; the
    /// existing "retry with no floor" fallback a few lines down still applies when nothing meets it.
    /// </summary>
    private const int SecondaryComplexityFloor = 2;

    private static string BuildStablePrefix(DirectorKnowledgeContext knowledge, string style, MusicModel music, bool stayCloseToExemplars, Random? variationRandom, ExemplarReuseTracker reuseTracker)
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
        sb.AppendLine(string.Join(", ", effectNames.Select(name => EnrichEffectName(name, knowledge.EffectVisualCharacters))));
        sb.AppendLine();
        AppendAnnotationGroundingGuidance(sb);

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
        sb.AppendLine("- Use Butterfly sparingly. It is a valid effect, not a forbidden one, but it reads as a strong, busy, high-duty-cycle signature move -- reserve it for a genuine rare moment (a specific peak/pop), not a default go-to for a hold layer or a Tree/Matrixes accent layer. When in doubt, prefer Twinkle, Sparkles, Snowflakes, or Color Wash instead.");
        sb.AppendLine("- SingleStrand reads as a single moving point/segment of color along one real axis -- it is most legible on Single Line models and Arches, where that one-axis motion is the whole prop's geometry. On a Tree or Matrix (dense, radial/2D props with plenty of pixels to spare), a single bare SingleStrand layer alone gets lost or reads as a flat shimmer rather than the intentional chase/wipe it is on a line or arch. User feedback (2026-11-12), confirmed against real corpus-mined multi-layer stacks: this is specifically about SingleStrand used ALONE as a Tree/Matrix's only layer -- 2-3 SingleStrand layers stacked together (different colors/offsets), or SingleStrand layered over a Color Wash base, is a genuinely different, denser visual (real shows do this) and is fine on Tree/Matrix. So: never a single standalone SingleStrand layer as a Tree/Matrix's whole look -- favor Twinkle, Sparkles, Snowflakes, or Color Wash for that single-layer case -- but SingleStrand stacked 2-3 deep with other layers is a legitimate Tree/Matrix choice, not just a Single Line/Arch one.");
        sb.AppendLine("- User feedback: increase the overall number of effects and layers across the whole show, not just on Tree/Matrixes. Layering is not limited to those two props -- any prop or group with enough resolution for a second layer to read as real texture (not noise) benefits from one during a Chorus/Drop/peak section: a base effect carrying color/identity plus a brighter or faster accent layer on top. Push for MORE total Looks using 2+ layers, MORE distinct Looks overall (use more of the MinLooks-MaxLooks range, not the floor), and faster composition-change rates toward the upper end of this style's own density band below -- while still respecting negative space during genuinely quiet sections. \"More layered/more frequent\" and \"everything on constantly\" are not the same goal; the first makes high-energy sections feel richer, the second just reads as noise.");
        sb.AppendLine("- A stated minimum, not just \"more\": a real hand-sequenced reference for this exact craft brief ran up to 8 concurrent EffectLayers on its richest prop and 3 on typical secondary props, against a generated show that peaked at 3 on its richest prop and 0-2 on secondary ones. Concretely: a Chorus/Drop/peak-energy Look's subject prop should carry 3 or more concurrent layers (the multi-layer template catalog below demonstrates real corpus-derived 2-layer patterns at every energy band; this floor pushes higher specifically for peak-energy sections, on top of that catalog, not instead of it); a Verse/ballad/low-energy Look's subject prop should still carry at least 2 (one steady base, one accent) -- never a bare single-layer Look outside a genuinely sparse/negative-space moment. Secondary props on a Chorus/Drop/peak section should carry 2-3 layers where their own resolution supports it, not just the subject.");
        sb.AppendLine("- Also use a genuinely wide slice of the real effect vocabulary above, not a narrow handful repeated everywhere: the same reference used 22 distinct effect names across the whole song, with its two most-used effects together covering only about half of all effects; a generated show that leans on 2 effects for well over a third of everything reads as monotone regardless of how its colors or timing vary. Spread Look design across many different effects from the vocabulary -- reserve any one effect (however good it looks) for a clear minority of this song's own Looks.");
        sb.AppendLine("- User follow-up (2026-11-10), measured directly against a real hand-sequenced reference show: its single dominant technique for making a hit/note actually feel like emphasis was a SHORT, full-brightness \"On\" flash (median ~200ms) landing on roughly 60% of every real detected transient hit -- used 326 times across one 81-second clip, nearly three-quarters of every effect it placed. A generated show compared against it instead ran every effect on the regular bar grid (median ~1700ms, i.e. about a whole bar) with only 4-19% of placements landing anywhere near a real hit -- reacting to the regular grid, never to the actual hits. Concretely: give at least one Look's own accent (or subject) layer a SHORT-duty (0.7-1.0, since the window itself is already brief -- not the low-duty trick used for a Beats/Bars-bound layer), non-timing-track-bound effect (e.g. \"On\", or any effect not in the seven-effect timing-track-bound family) with \"Timescale\": \"Transients\" (or \"VocalNotes\" for a vocal-driven section) whenever this song's own volatile section below reports real detected hits/notes -- this is what turns \"timed to the beat\" into \"timed to THIS song's own real hits,\" and it is the single highest-leverage change available for making emphasis actually read as emphasis. One real difference between the two: a \"Transients\" mark is always a short, fixed flash (a hit is a single instant, nothing to hold); a \"VocalNotes\" mark spans that REAL sung note's own actual duration whenever one is known -- a long held note gets a correspondingly long effect, a short one a short effect -- so the same high duty cycle now means \"stay lit for as long as the note is actually sung,\" not just \"flash briefly.\"");
        sb.AppendLine();

        AppendMultiLayerTemplates(sb, knowledge.TemplatesOrEmpty);
        AppendPatternLibraryDensity(sb, knowledge.PatternLibraryOrEmpty);
        AppendLayerComplexityGuidance(sb, knowledge.LayerComplexityOrEmpty);

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

            var count = ExemplarCountForKind(knowledge.Exemplars, kind);

            // Build-plan Phase 25 Part D.4/loose end: request a genuinely layered moment for a
            // peak/featured SectionKind first; fall back to the plain (no complexity floor) retrieval
            // rather than silently showing zero exemplars for this Kind when nothing in the index meets
            // that floor yet.
            var requestLayeredMoment = DefaultEnergyCeilings.ArchitectureSection10.TryGetValue(kind, out var ceiling) && ceiling >= ComplexityRequestCeilingThreshold;
            int? complexityFloor = requestLayeredMoment ? LayerComplexityAnalyzer.DefaultMinDistinctLayers : SecondaryComplexityFloor;

            // Build-plan Phase 26 bugfix: real-run finding (2026-11-11, project "Bang") -- several
            // SectionKinds with zero real same-kind corpus entries each fell back to the identical
            // cross-kind pool (the query vector is otherwise the same placeholder shape for every Kind,
            // see ExemplarFingerprint.ToComparisonVector) and deterministically drew the SAME single top
            // match every time, inflating one real exemplar's reuse count with no real variety ever having
            // been considered. Excluding whatever this run has already drawn (reuseTracker.DrawnKeys, built
            // up incrementally by the earlier Record calls below as this foreach loop proceeds kind by
            // kind) makes every later kind's own retrieval genuinely prefer a fresh exemplar first.
            var alreadyDrawn = reuseTracker.DrawnKeys;
            var retrieval = stayCloseToExemplars
                ? knowledge.Exemplars.FindSimilar(query, count, complexityFloor, alreadyDrawn)
                : knowledge.Exemplars.FindVaried(query, count, variationRandom!, complexityFloor, alreadyDrawn);
            if (retrieval.Entries.Count == 0 && complexityFloor is not null)
            {
                retrieval = stayCloseToExemplars
                    ? knowledge.Exemplars.FindSimilar(query, count, excludeKeys: alreadyDrawn)
                    : knowledge.Exemplars.FindVaried(query, count, variationRandom!, excludeKeys: alreadyDrawn);
            }

            reuseTracker.Record(retrieval.Entries);
            if (retrieval.Entries.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"Requesting {count} {kind} example(s):");
            foreach (var exemplar in retrieval.Entries)
            {
                // Build-plan Phase 16 §2a: label with the retrieved entry's OWN real kind, never the
                // query's kind unconditionally -- a prior version of this line always printed `kind`
                // here, which silently mislabeled a cross-kind fallback result (e.g. a Chorus snippet
                // printed as "[Solo]") as if it were a real example of the queried kind.
                var actualKind = exemplar.Fingerprint.SectionKind;
                var label = retrieval.IsFallback
                    ? $"{kind} -- no real {kind} example exists, nearest available: {actualKind}"
                    : actualKind.ToString();
                var description = string.IsNullOrWhiteSpace(exemplar.Description) ? string.Empty : $" {exemplar.Description}";
                sb.AppendLine($"- [{label}] \"{exemplar.Fingerprint.SongTitle}\" bars {exemplar.Fingerprint.StartBarIndex}-{exemplar.Fingerprint.EndBarIndexExclusive}, {exemplar.Fingerprint.TempoBpm:0} BPM: {exemplar.Effects.Count} effects placed.{description}");
            }
        }

        return sb.ToString();
    }

    private static string BuildVolatileSuffix(MusicModel music, LayoutSummary layout, CustomGenerationPrompt? customPrompt, WholeCanvasCoOccurrenceStats? wholeCanvasCoOccurrence)
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

        AppendRepeatingKindStructure(sb, music);
        AppendEnergyTransitions(sb, music);
        AppendKeyChanges(sb, music);
        AppendEmphasisTrackAvailability(sb, music);

        sb.AppendLine();
        sb.AppendLine(layout.ToPromptText());

        AppendBandEnergyGroupRoleMapping(sb, music, layout, bandProfiles);
        AppendWholeCanvasGroupSection(sb, layout, wholeCanvasCoOccurrence);

        CustomPromptBlockFormatter.AppendBlock(sb, customPrompt);

        return sb.ToString();
    }

    /// <summary>
    /// Build-plan Phase 24 §2b: groups this song's own real section list by <see cref="SectionKind"/> and,
    /// for any Kind with <see cref="ShowBibleValidator.MinOccurrencesForDiversityFloor"/>+ real occurrences,
    /// names every one of that Kind's own bar ranges together as a single explicit line -- the Director's
    /// stable prefix already carries prose telling it to "design real variation" across same-Kind repeats
    /// (see the baseline craft guidance above), but a flat per-section bar list gives it nothing to actually
    /// count against; this is the one piece of cross-section awareness a Director call can cheaply have (it
    /// already sees the whole song's section list -- this just re-groups it and states what it means),
    /// paired with <see cref="ShowBibleValidator"/>'s own hard per-Kind Look-diversity floor so the
    /// instruction is also mechanically checked, not just asked for in prose. Also calls out any occurrence
    /// whose own bar length differs from the Kind's typical (most common) length, so an unusually long/short
    /// repeat (e.g. an extended final chorus) is flagged as worth a correspondingly distinct treatment.
    /// Appends nothing when no Kind in this song repeats that many times.
    /// </summary>
    private static void AppendRepeatingKindStructure(StringBuilder sb, MusicModel music)
    {
        var groups = music.Sections.Labels
            .GroupBy(l => l.Kind)
            .Where(g => g.Count() >= ShowBibleValidator.MinOccurrencesForDiversityFloor)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var occurrences = group.OrderBy(o => o.StartBarIndex).ToList();
            var lengths = occurrences.Select(o => o.EndBarIndexExclusive - o.StartBarIndex).ToList();
            var typicalLength = lengths.GroupBy(l => l).OrderByDescending(g => g.Count()).First().Key;

            var ranges = string.Join(", ", occurrences.Select(o => $"[{o.StartBarIndex},{o.EndBarIndexExclusive})"));

            var outlierNotes = new List<string>();
            for (var i = 0; i < occurrences.Count; i++)
            {
                var length = lengths[i];
                if (length == typicalLength)
                {
                    continue;
                }

                var comparison = length > typicalLength ? "longer" : "shorter";
                outlierNotes.Add($"the {Ordinal(i + 1, occurrences.Count)} one is {length} bars, {comparison} than the others");
            }

            var outlierSuffix = outlierNotes.Count > 0 ? $" (note: {string.Join("; ", outlierNotes)})" : string.Empty;
            var requiredDistinctLooks = (int)Math.Ceiling(occurrences.Count / 2.0);
            sb.AppendLine($"{group.Key} repeats {occurrences.Count} times: bars {ranges}{outlierSuffix} -- design at least {requiredDistinctLooks} visibly distinct Looks across these {occurrences.Count} occurrences, not one Look reused. At least {requiredDistinctLooks} Looks must stay eligible for {group.Key} (AllowedSectionKinds names '{group.Key}', or is left unrestricted) or this Show Bible will fail validation -- see the REQUIRED rule above.");
        }
    }

    /// <summary>Renders a 1-based occurrence index as "first"/"last"/"2nd"/"3rd"/... for <see cref="AppendRepeatingKindStructure"/>'s own outlier notes.</summary>
    private static string Ordinal(int oneBasedIndex, int total)
    {
        if (oneBasedIndex == 1)
        {
            return "first";
        }

        if (oneBasedIndex == total)
        {
            return "last";
        }

        var suffix = (oneBasedIndex % 100) is 11 or 12 or 13
            ? "th"
            : (oneBasedIndex % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{oneBasedIndex}{suffix}";
    }

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 4 (2026-11-02): grounds the baseline craft guidance's own
    /// "design for pops and build-ups" line (above) with this song's actual measured moments, rather than
    /// leaving it pure unverified prose with nothing real to point at. Appends nothing when
    /// <see cref="MusicModel.EnergyTransitions"/> was never computed (no <see cref="MusicModel.BandEnergy"/>
    /// available) or came back empty (a song with no detectable sharp transition).
    /// </summary>
    private static void AppendEnergyTransitions(StringBuilder sb, MusicModel music)
    {
        if (music.EnergyTransitions is not { Count: > 0 } transitions)
        {
            return;
        }

        var rendered = transitions
            .OrderBy(t => t.TimeTicks)
            .Select(t => $"{TimeSpan.FromTicks(t.TimeTicks):m\\:ss} {(t.Kind == EnergyTransitionKind.Buildup ? "build-up" : "drop")}");
        sb.AppendLine($"Measured energy transitions (real, from the audio -- not assumed): {string.Join(", ", rendered)}.");
    }

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 7 (2026-11-02): surfaces this song's own real, measured
    /// key/mode changes (<see cref="KeyDetector"/>) as harmonic-shift moments worth a palette change --
    /// a real signal a Director previously had nothing like. Appends nothing when
    /// <see cref="MusicModel.KeyChanges"/> was never computed or came back empty (no confident key reading
    /// anywhere, or a genuinely single-key song).
    /// </summary>
    private static void AppendKeyChanges(StringBuilder sb, MusicModel music)
    {
        if (music.KeyChanges is not { Count: > 0 } keyChanges)
        {
            return;
        }

        var rendered = keyChanges
            .OrderBy(k => k.TimeTicks)
            .Select(k => $"{TimeSpan.FromTicks(k.TimeTicks):m\\:ss} -> {k.KeyName}");
        sb.AppendLine($"Measured key/mode changes (real, from the audio -- not assumed): {string.Join(", ", rendered)}. Consider a palette or accent-color shift at each one -- a harmonic change is a natural place for the visuals to shift too.");
    }

    /// <summary>
    /// User follow-up (2026-11-10): tells the Director, for THIS song specifically, whether the real
    /// "Transients"/"VocalNotes" timing tracks the baseline craft guidance above names will actually exist
    /// at compile time (<see cref="ChoreographyEngine.Compile"/> only emits either track when
    /// <see cref="ChoreographyEngine.Input.EmphasisInstants"/> carries a real, non-empty instant of that
    /// kind) -- so a Look is never authored to bind a layer to a track this song's own real detection
    /// turned up nothing for. Appends nothing when this song has neither signal at all.
    /// </summary>
    private static void AppendEmphasisTrackAvailability(StringBuilder sb, MusicModel music)
    {
        var hitCount = music.TransientHits?.Count ?? 0;
        var noteCount = music.Melody?.Notes.Count ?? 0;
        if (hitCount == 0 && noteCount == 0)
        {
            return;
        }

        sb.AppendLine($"Real emphasis timing tracks available for this song: {(hitCount > 0 ? $"\"Transients\" ({hitCount} real detected hits)" : "no \"Transients\" track (0 real detected hits)")}, {(noteCount > 0 ? $"\"VocalNotes\" ({noteCount} real detected sung-note onsets)" : "no \"VocalNotes\" track (0 real detected sung-note onsets)")}.");
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
    /// Build-plan Phase 25 Part B.1/B.2: a sibling section to "## Band-to-group mapping" for every
    /// <see cref="BandRole.WholeCanvas"/>-classified group (Part A's structural coverage-ratio
    /// detection) -- present only when one exists, same "append nothing when there's nothing to ground it
    /// in" convention <see cref="AppendBandEnergyGroupRoleMapping"/> already uses. States the measured fact
    /// (which group, how much of the layout it covers) and that it is a different *kind* of role than
    /// Foundation/Subject/Accent, plus -- once Part B.4's <see cref="WholeCanvasCoOccurrenceStats"/> has
    /// real samples (<paramref name="coOccurrence"/>) -- concrete, measured usage guidance for *when* a
    /// real designer actually fires a whole-canvas group, mirroring how
    /// <see cref="AppendBandEnergyGroupRoleMapping"/> already states its own rule as "measured for this
    /// song, not re-decided per Look." Falls back to disclosing that no training data exists yet (never a
    /// fabricated usage-frequency guess) when <paramref name="coOccurrence"/> is <see langword="null"/> --
    /// distinct (build-plan Phase 25 Part C real-corpus finding) from a real <c>CorpusTrainingRunner</c> run
    /// having actually completed and genuinely found zero whole-canvas windows across its own corpus
    /// (<see cref="WholeCanvasCoOccurrenceStats.WindowSampleCount"/> <c>== 0</c>) -- those are different
    /// facts (one says "run the tool", the other says "the tool ran and this really doesn't happen in your
    /// corpus yet") and must not share the same message.
    /// </summary>
    private static void AppendWholeCanvasGroupSection(StringBuilder sb, LayoutSummary layout, WholeCanvasCoOccurrenceStats? coOccurrence)
    {
        var wholeCanvasGroups = BandEnergyGroupRoleMapper.Classify(layout).Where(c => c.Role == BandRole.WholeCanvas).ToList();
        if (wholeCanvasGroups.Count == 0)
        {
            return;
        }

        var pixelsByReference = layout.Groups.Concat(layout.TopPixelHeavyModels)
            .GroupBy(e => e.Reference)
            .ToDictionary(g => g.Key, g => g.First().NodeCount);

        sb.AppendLine();
        sb.AppendLine("## Whole-canvas group");
        sb.AppendLine("Not tied to any single frequency band's spatial home -- a song-wide synchronized moment across the whole layout, never a fourth foundation/subject/accent peer:");
        foreach (var group in wholeCanvasGroups)
        {
            var coverage = layout.TotalPixels > 0 ? pixelsByReference[group.Reference] / (double)layout.TotalPixels : 0;
            sb.AppendLine($"- {group.Reference} ({coverage:P0} of this layout's total pixels)");
        }

        if (coOccurrence is null)
        {
            sb.AppendLine("Usage guidance: no real corpus training data is available yet for when a whole-canvas group actually fires (run the CorpusTrainer tool against an external sequence folder) -- use judgment, reserving it for a genuine peak moment rather than a default go-to.");
            return;
        }

        if (coOccurrence.WindowSampleCount == 0)
        {
            sb.AppendLine("Usage guidance: a real CorpusTrainer run completed and genuinely found no whole-canvas-group effect windows anywhere in that corpus yet -- a measured absence, not an untrained gap. Use judgment, reserving it for a genuine peak moment rather than a default go-to.");
            return;
        }

        sb.AppendLine($"Usage guidance (measured across {coOccurrence.SongsWithWholeCanvasCount} real corpus song(s), {coOccurrence.WindowSampleCount} whole-canvas window(s) -- not a guess):");
        sb.AppendLine($"- Co-fires with: Subject {coOccurrence.SubjectCoFireRatio:P0} of the time, Foundation {coOccurrence.FoundationCoFireRatio:P0}, Accent {coOccurrence.AccentCoFireRatio:P0}.");

        if (coOccurrence.SectionKindHistogram.Count > 0)
        {
            var total = coOccurrence.SectionKindHistogram.Values.Sum();
            var ranked = coOccurrence.SectionKindHistogram.OrderByDescending(kv => kv.Value).ToList();
            var topText = string.Join(", ", ranked.Take(2).Select(kv => $"{kv.Key} ({kv.Value / (double)total:P0})"));
            sb.AppendLine($"- Concentrated in: {topText}.");
        }

        if (coOccurrence.EnergyTierHistogram.Count > 0)
        {
            var total = coOccurrence.EnergyTierHistogram.Values.Sum();
            var dominant = coOccurrence.EnergyTierHistogram.OrderByDescending(kv => kv.Value).First();
            sb.AppendLine($"- Energy: {dominant.Key} most often ({dominant.Value / (double)total:P0} of windows).");
        }

        if (coOccurrence.MedianDurationBars is { } median)
        {
            sb.AppendLine($"- Typical duration: median {median:0.#} bar(s), P90 {coOccurrence.P90DurationBars:0.#} bar(s) -- a brief, discrete moment, not a long-held state.");
        }
    }

    /// <summary>
    /// Build-plan Phase 20 §2c: renders this corpus's own real, measured <see cref="PatternLibrary.DensityBySection"/>
    /// (effect-starts-per-second, by <see cref="SectionKind"/>) and <see cref="PatternLibrary.LayerCountPerModelDistribution"/>
    /// (how many distinct <c>EffectLayer</c>s a real model actually carries, across the whole corpus) next
    /// to the existing hand-tuned <see cref="DefaultEnergyCeilings"/>-style guidance above -- a grounding
    /// cross-check, never a replacement (the style preset's own density band above still drives what this
    /// run actually compiles to). Appends nothing available-but-empty disclosure when no corpus mining has
    /// ever been run (the same disclosed-empty convention <see cref="AppendMultiLayerTemplates"/> and the
    /// "## Verified craft rules" section below both already use).
    /// </summary>
    private static void AppendPatternLibraryDensity(StringBuilder sb, PatternLibrary library)
    {
        sb.AppendLine("## Real corpus density/layering baseline (measured, not hand-tuned)");
        sb.AppendLine("Cross-check only -- this song's own style preset density band above still governs what you actually author; these are real, measured corpus-wide averages, not a target to hit exactly:");
        if (library.SongCount == 0)
        {
            sb.AppendLine("(None available for this run -- corpus mining has not been run yet.)");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"Measured across {library.SongCount} real corpus songs ({library.TotalEffectCount} total effects):");
        if (library.DensityBySection.Count > 0)
        {
            var densityText = string.Join(", ", library.DensityBySection
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}={kv.Value:0.00}/s"));
            sb.AppendLine($"- Real section-change density (effect-starts/second, by section kind): {densityText}.");
        }

        if (library.LayerCountPerModelDistribution.Count > 0)
        {
            var layerText = string.Join(", ", library.LayerCountPerModelDistribution
                .OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key} layer(s) on {kv.Value} real model-occurrence(s)"));
            sb.AppendLine($"- Real per-model layer-depth distribution (how many distinct EffectLayers a real model actually carries, across the whole corpus): {layerText}.");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Build-plan Phase 25 Part C §3 bullet 3/loose end: grounds the baseline craft guidance's own "3+
    /// concurrent layers on a Chorus/Drop/peak-energy Look's subject prop" line with real, measured corpus
    /// numbers -- how many real windows across the external training corpus actually reached 3+/4+/5+
    /// concurrent layers, in which section kinds, and for how long -- the same "cross-check, never a
    /// replacement" framing <see cref="AppendPatternLibraryDensity"/> already uses for density. Appends a
    /// disclosed-empty message (same convention as that method) when no training pass has ever been run.
    /// </summary>
    private static void AppendLayerComplexityGuidance(StringBuilder sb, LayerComplexityStats stats)
    {
        sb.AppendLine("## Real corpus layer-complexity baseline (measured, not hand-tuned)");
        sb.AppendLine("Cross-check only, for the \"3+ concurrent layers\" baseline craft guidance above -- how often a real corpus moment actually reached that complexity, and where:");
        if (stats.ThreePlus.WindowCount == 0)
        {
            sb.AppendLine("(None available for this run -- run the CorpusTrainer tool against an external sequence folder to populate this.)");
            sb.AppendLine();
            return;
        }

        AppendLayerComplexityTier(sb, "3+ layers", stats.ThreePlus);
        AppendLayerComplexityTier(sb, "4+ layers", stats.FourPlus);
        AppendLayerComplexityTier(sb, "5+ layers", stats.FivePlus);
        sb.AppendLine();
    }

    private static void AppendLayerComplexityTier(StringBuilder sb, string label, LayerComplexityTierStats tier)
    {
        if (tier.WindowCount == 0)
        {
            sb.AppendLine($"- {label}: no real window reached this tier.");
            return;
        }

        var sectionText = tier.SectionKindHistogram.Count > 0
            ? ", most often in " + string.Join(", ", tier.SectionKindHistogram.OrderByDescending(kv => kv.Value).Take(2).Select(kv => kv.Key))
            : string.Empty;
        var durationText = tier.MedianDurationBars is { } median ? $", typical duration {median:0.#} bar(s) (P90 {tier.P90DurationBars:0.#})" : string.Empty;
        sb.AppendLine($"- {label}: {tier.WindowCount} real window(s){sectionText}{durationText}.");
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
        sb.AppendLine("Every layer within one Look's own \"Layers\" array MUST have a distinct \"Z\" value (0, 1, 2, ... in the order you list them) -- two layers left at the same Z (most commonly both left at the default-looking 0) is a hard validation failure, not a cosmetic detail. The first/base layer is Z=0, the next layer stacked on top of it is Z=1, and so on -- this applies to every multi-layer Look, including every Look built from a seed multi-layer template below (REQUIRED to have 2 layers): its subject layer is Z=0 and its secondary/accent layer is Z=1, never both Z=0.");
        sb.AppendLine("A literal \"moving from one prop to the next\" sweep (light prop A, then B, then C in turn) is NOT something a single group-targeted effect can produce -- a group effect always treats every member identically and simultaneously. For that specific traveling effect, declare \"PropDelays\" on a Look whose Props/Subject is a group with genuinely different-sized members (mass/mid/accent tiers by real pixel weight -- e.g. \"{\\\"mass\\\":\\\"+0ms\\\",\\\"mid\\\":\\\"+80ms\\\",\\\"accent\\\":\\\"+160ms\\\"}\"), which staggers each tier's own reveal instead of firing every member at once -- reserve this for Looks where that graduated, traveling reveal is the actual intent, not every Look using a group.");
        sb.AppendLine($"REQUIRED (build-plan Phase 24 §2a -- a hard validation failure, same tier as the {ShowBibleValidator.MinLooks}-{ShowBibleValidator.MaxLooks}-Look count rule above): a Look is \"eligible\" for a SectionKind when its own \"AllowedSectionKinds\" either omits that Kind's restriction entirely (unrestricted -- eligible for every Kind) or explicitly names it. For any SectionKind that repeats {ShowBibleValidator.MinOccurrencesForDiversityFloor}+ times in this song (named below, under \"This song\", with every one of that Kind's own real bar ranges listed together), at least ceil(occurrences/2) Looks must stay eligible for it. This means: if you give a Look an explicit \"AllowedSectionKinds\" list at all, make sure you still leave enough OTHER Looks eligible for every repeating Kind -- do not restrict so many Looks away from a repeating Kind that fewer than this minimum remain. The simplest way to always satisfy this is to leave \"AllowedSectionKinds\" omitted (unrestricted) on most Looks, and only restrict the few Looks where a real creative reason calls for it.");
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
                        "Timescale": "Beats|Bars|Phrases|Transients|VocalNotes (optional, omit if unused -- use these exact real track names, not a paraphrase like \"beat\" or \"bar\"; Transients/VocalNotes exist only when this song's own real detected hits/sung notes are available, see below)",
                        "Annotation": "optional -- what this layer is meant to visually express; see the grounding rule below the effect vocabulary, omit entirely rather than guess"
                      },
                      {
                        "Z": 1,
                        "Props": "a prop or group name (a second layer -- only when this Look genuinely needs one; a single-layer Look just omits this and every later array entry entirely)",
                        "Effect": "must be exactly one of the effect vocabulary names below",
                        "Role": "subject|support|secondary",
                        "Blend": "Normal|Bright|Additive|Max",
                        "Duty": 0.0
                      },
                      {
                        "Z": 2,
                        "Props": "a prop or group name (a third layer -- REQUIRED, not just allowed, on a Chorus/Solo/peak-energy Look's subject prop per the baseline craft guidance above; omit entirely on a Verse/low-energy or genuinely single-layer Look)",
                        "Effect": "must be exactly one of the effect vocabulary names below",
                        "Role": "subject|support|secondary",
                        "Blend": "Normal|Bright|Additive|Max",
                        "Duty": 0.0
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

    /// <summary>
    /// Build-plan Phase 18 §6 (Phase E): enriches a bare effect name with its real, known visual character
    /// when the shippable catalog has one -- additive only, the exact bare <paramref name="effectName"/>
    /// token is always kept as a prefix (never replaced/renamed), so existing exact-substring assertions on
    /// effect names keep passing unchanged. Prefers the documented manual.xlights.org behavior (what the
    /// effect is actually FOR) over the AI-observed description when both are present, since that is more
    /// useful to a choreographer than "no visible content" for the handful of effects this pipeline's
    /// isolated fixture could never meaningfully exercise. Returns the bare name, undecorated, when no
    /// catalog is available or this effect has no entry in it (including the hardware effects
    /// <see cref="EffectVisualCharacterCatalogBuilder.HardwareOnlyEffectNames"/> deliberately excludes).
    /// </summary>
    /// <summary>
    /// Build-plan Phase 18 §7 (Phase F): states the grounding rule for a <c>LookLayer</c>'s optional
    /// <c>Annotation</c> field, in terms of the three visible markers the effect vocabulary list above
    /// already renders via <see cref="EnrichEffectName"/> -- no separate data structure is introduced here,
    /// the same three buckets (a real appended description, a "(not visually exercised...)" marker, or a
    /// bare name) already distinguish confirmed/documented-only/unknown for both roles sharing this helper.
    /// </summary>
    private static void AppendAnnotationGroundingGuidance(StringBuilder sb)
    {
        sb.AppendLine("## Layer annotations (optional, grounded -- build-plan Phase 18 §7)");
        sb.AppendLine("A Look layer may optionally carry an \"Annotation\": a short free-text note of what that specific layer is meant to visually express (e.g. \"mimics 'I get up' with an upward chase on the Mega Tree\"). This must be a GROUNDED claim, never a plausible-sounding guess:");
        sb.AppendLine("- An effect name above followed by \" -- <description>\" (no \"(not visually exercised...)\" qualifier) has a real, AI-observed visual character -- you may write an Annotation claiming a specific shape/motion/direction for that layer ONLY if that exact description actually supports the claim (e.g. only call out \"upward motion\" for an effect whose own description says it moves upward).");
        sb.AppendLine("- An effect name above followed by \" -- <description> (not visually exercised by this pipeline's own isolated fixture)\" is documented-behavior-only -- you know what it is FOR, not what it visually looks like. Never claim a specific visual shape/motion/direction for it; if you annotate such a layer at all, describe only its declared purpose/role, never an invented look.");
        sb.AppendLine("- An effect name above with no appended description at all has no known visual character -- never write an Annotation claiming any specific visual behavior for it. Omitting Annotation entirely is always correct and always preferred over a guess.");
        sb.AppendLine("Within that discipline, DO use the feature: write an Annotation for at least each Look's own subject layer whenever that layer's effect has a real, AI-observed description above (the first bullet's case) -- omitting Annotation should be reserved for a layer whose effect is documented-behavior-only or unknown, never skipped on a layer that already has a grounded description available.");
        sb.AppendLine();
    }

    /// <summary>
    /// Build-plan Phase 18 §7 (Phase F): shared with <see cref="SectionScriptPromptBuilder"/> so both the
    /// Director's own Look-authoring prompt and the Choreographer's rebinding prompt enrich/ground effect
    /// names identically -- the same catalog entry must read the same way regardless of which role sees it.
    /// </summary>
    internal static string EnrichEffectName(string effectName, EffectVisualCharacterCatalog? catalog)
    {
        var character = catalog?.Find(effectName);
        if (character is null)
        {
            return effectName;
        }

        var description = character.DocumentedBehavior is { Length: > 0 } documented
            ? $"{documented} (not visually exercised by this pipeline's own isolated fixture)"
            : character.Description;
        return $"{effectName} -- {description}";
    }

    private static double AverageBpm(MusicModel music) => music.BeatGrid.SelectedTempo?.Bpm ?? 0;
}
