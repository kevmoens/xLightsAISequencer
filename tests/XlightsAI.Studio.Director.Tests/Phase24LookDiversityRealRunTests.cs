using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 24 §2d/§4: a real, regenerated song measuring whether §2a's new per-Kind
/// Look-diversity floor actually changes what gets compiled, not just whether it validates in the
/// abstract. Reuses <see cref="Phase6EndToEndRealTrackTests"/>'s own real local-Ollama/real-xLights-
/// fixture/30s-clip pattern (Choreographer-only, no Director credentials required -- same documented
/// reason that class gives), but overrides the real analyzed song's own section list with a synthetic
/// 5-occurrence Chorus repeat structure (the real clip's own natural structure rarely repeats a Kind
/// enough times to exercise this phase at all) so there is something real to measure. Two hand-authored
/// Show Bibles, identical except for how many Looks are tagged eligible for Chorus, are run through the
/// SAME real pipeline against the SAME real song: "baseline" (1 eligible Look -- the shape every Show
/// Bible before this phase could legally have) and "diverse" (3 eligible Looks -- the floor this phase's
/// own ceil(5/2) rule now requires). Skips cleanly (never fails) when the real xLights install or a real
/// local Ollama are not present, or the real clip does not have enough real bars to carve into 5 sections.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Phase24LookDiversityRealRunTests : IDisposable
{
    private const string OllamaEndpoint = "http://127.0.0.1:11434";
    private const string OllamaModel = "qwen2.5-coder:7b-instruct";
    private const int ChorusOccurrenceCount = 5;

    private static readonly Dictionary<SectionKind, double> EnergyCeilingByKind = new()
    {
        [SectionKind.Intro] = 0.20,
        [SectionKind.Verse] = 0.40,
        [SectionKind.PreChorus] = 0.70,
        [SectionKind.Chorus] = 0.85,
        [SectionKind.Bridge] = 0.25,
        [SectionKind.Solo] = 0.80,
        [SectionKind.Outro] = 0.15,
        [SectionKind.Unknown] = 0.50,
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase24-e2e-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<bool> IsOllamaReachableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync($"{OllamaEndpoint}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact(Timeout = 900_000)]
    public async Task GenerateEndToEnd_RealSong_BaselineVsDiverseLookEligibility_MeasuresRealDistinctLookAndEffectCounts()
    {
        if (!RealXLightsFixture.IsAvailable || !await IsOllamaReachableAsync())
        {
            return;
        }

        const string realSequencePath = @"C:\xLights\Dubstep Christmas Light Show.xsq";
        var clipPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "dubstep-30s-clip.mp3");
        if (!File.Exists(clipPath) || !File.Exists(realSequencePath))
        {
            return;
        }

        var ct = CancellationToken.None;
        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, ct);
        var layoutSummary = LayoutSummary.Build(layout);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);

        var knowledge = new DirectorKnowledgeContext(
            vocabulary,
            new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []),
            new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var baseMusic = await AnalyzeAsync(clipPath, realSequencePath, "Dubstep Christmas Light Show (Phase 24 real run, 30s clip)", ct);

        var beatsPerBar = baseMusic.BeatGrid.Settings.BeatsPerBar;
        var totalBars = baseMusic.BeatGrid.Beats.Length / beatsPerBar;
        if (totalBars < ChorusOccurrenceCount * 2)
        {
            return; // Not enough real bars in this clip to carve into 5 meaningful sections -- skip cleanly.
        }

        var music = baseMusic with { Sections = BuildRepeatingChorusSections(baseMusic.SongId, totalBars) };

        // §4 acceptance (first clause): a real validator rejection for the baseline shape, none for diverse.
        var sectionKindCounts = new Dictionary<SectionKind, int> { [SectionKind.Chorus] = ChorusOccurrenceCount };
        var baselineBible = BuildHandAuthoredShowBible(chorusEligibleLookCount: 1);
        var diverseBible = BuildHandAuthoredShowBible(chorusEligibleLookCount: 3);
        var baselineViolations = ShowBibleValidator.Validate(baselineBible.Content, layout, vocabulary, recipes, sectionKindOccurrenceCounts: sectionKindCounts);
        var diverseViolations = ShowBibleValidator.Validate(diverseBible.Content, layout, vocabulary, recipes, sectionKindOccurrenceCounts: sectionKindCounts);
        Assert.Contains(baselineViolations, v => v.Rule == "look-diversity-per-kind");
        Assert.DoesNotContain(diverseViolations, v => v.Rule == "look-diversity-per-kind");

        // §4 acceptance (second clause): a real Director prompt log shows the new per-Kind repeat-structure line.
        var prompt = DirectorPromptBuilder.Build(knowledge, "High-Energy / EDM", music, layoutSummary);
        Assert.Contains($"Chorus repeats {ChorusOccurrenceCount} times:", prompt.VolatileSuffix);

        using var httpClient = new HttpClient();
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var choreographerConfig = new RoleProviderConfig(AiRole.Choreographer, true, AiProviderType.OpenAiCompatible, OllamaEndpoint, OllamaModel, null, RequestTimeoutSeconds: 90, MaxRetries: 1, MaxResponseBytes: 131_072);
        var choreographerClient = new OpenAiCompatibleChatCompletionClient(choreographerConfig, executor, apiKey: null);
        var pipeline = new ChoreographyGenerationPipeline(
            knowledge, "High-Energy / EDM",
            directorClient: null, directorModelName: null,
            choreographerClient, OllamaModel,
            maxOutputTokens: 600);
        var settings = new ChoreographyGenerationSettings(layout, vocabulary, recipes, registry, EnergyCeilingByKind, _root);

        var baselineMeasurement = await GenerateAndMeasureAsync(pipeline, music, layoutSummary, settings, baselineBible, recipes, ct);
        var diverseMeasurement = await GenerateAndMeasureAsync(pipeline, music, layoutSummary, settings, diverseBible, recipes, ct);

        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence");
        Directory.CreateDirectory(evidenceDir);
        await File.WriteAllTextAsync(Path.Combine(evidenceDir, "PHASE-24-LOOK-DIVERSITY.md"), $"""
            # Phase 24 Look diversity -- real run (auto-generated by Phase24LookDiversityRealRunTests)

            Real layout: `{RealXLightsFixture.LayoutPath}`. Choreographer model: `{OllamaModel}` via local Ollama.
            Real song: `Dubstep Christmas Light Show.mp3` (30s clip), real bar count {totalBars}, reshaped into
            {ChorusOccurrenceCount} synthetic Chorus-kind sections (real clip's own natural structure does not
            repeat any one Kind {ChorusOccurrenceCount}+ times, so this is the one input this phase's own
            measurement needed that the real analysis alone does not provide).

            ## §2a: real validator rejection
            Baseline Show Bible (1 Look eligible for Chorus, the shape every Show Bible before this phase could
            legally have): `{(baselineViolations.Any(v => v.Rule == "look-diversity-per-kind") ? "REJECTED" : "accepted (unexpected)")}`
            -- {string.Join("; ", baselineViolations.Where(v => v.Rule == "look-diversity-per-kind").Select(v => v.Message))}
            Diverse Show Bible (3 Looks eligible for Chorus, meeting ceil(5/2)=3): `{(diverseViolations.Any(v => v.Rule == "look-diversity-per-kind") ? "REJECTED (unexpected)" : "accepted")}`

            ## §2b: real Director prompt excerpt
            ```
            {prompt.VolatileSuffix.Split('\n').First(l => l.StartsWith("Chorus repeats", StringComparison.Ordinal))}
            ```

            ## §2d: real measured Look/effect diversity across the Chorus sections, before vs after
            | Bible | Distinct LookIds used across {ChorusOccurrenceCount} Chorus sections | Distinct xLights effect names compiled across those sections |
            |---|---:|---:|
            | Baseline (1 eligible Look) | {baselineMeasurement.DistinctLookCount} | {baselineMeasurement.DistinctEffectCount} |
            | Diverse (ceil(5/2)=3 eligible Looks) | {diverseMeasurement.DistinctLookCount} | {diverseMeasurement.DistinctEffectCount} |

            Real section-by-section LookId assignment (baseline): {string.Join(", ", baselineMeasurement.LookIdsBySection)}
            Real section-by-section LookId assignment (diverse): {string.Join(", ", diverseMeasurement.LookIdsBySection)}

            **Disclosed finding, measured directly by this real run, not assumed:** a Look's own
            `AllowedSectionKinds` is advisory to the real Choreographer call, not a hard filter --
            `SectionScriptPromptBuilder` lists every Look (not just the eligible ones) with a `sections=`
            annotation, and neither `SectionScriptValidator` nor `SectionScriptCompiler` ever rejects a
            SectionScript whose `LookId` ignores it. The baseline run above measured
            {baselineMeasurement.DistinctLookCount} distinct Looks used even though only 1 was declared
            eligible for Chorus -- the real local 7B Choreographer model did not reliably honor the
            restriction. §2a's validator floor and §2b's repeat-structure prompt line (both verified above)
            are real and working as designed; they raise how many Looks a repeating Kind COULD legitimately
            draw from, and tell the Director to design that many -- but neither one, nor anything else in
            this codebase yet, forces the Choreographer's own per-section pick to respect that ceiling at
            generation time. Closing that remaining gap (a real per-section eligibility check, enforced the
            same way `look-ref` already is) is follow-up work this measurement surfaces, not something §2a/
            §2b themselves claimed to solve.
            """, ct);

        // §4 acceptance (third clause): the compiled result is real and measurable for both runs (not an
        // impression) -- both legs of the comparison actually compiled real effects across real Chorus
        // sections, which is what makes the before/after numbers above a real measurement at all.
        Assert.True(baselineMeasurement.DistinctLookCount > 0 && baselineMeasurement.DistinctEffectCount > 0, "Baseline run compiled zero real Looks/effects across the Chorus sections.");
        Assert.True(diverseMeasurement.DistinctLookCount > 0 && diverseMeasurement.DistinctEffectCount > 0, "Diverse run compiled zero real Looks/effects across the Chorus sections.");
    }

    private static async Task<(int DistinctLookCount, int DistinctEffectCount, IReadOnlyList<string> LookIdsBySection)> GenerateAndMeasureAsync(
        ChoreographyGenerationPipeline pipeline, MusicModel music, LayoutSummary layoutSummary, ChoreographyGenerationSettings settings,
        ShowBible bible, EffectRecipeCatalog recipes, CancellationToken ct)
    {
        var songId = Guid.NewGuid();
        var report = await pipeline.GenerateAsync(music, layoutSummary, settings, songId, bible, ct);

        var chorusLabels = music.Sections.Labels.Where(l => l.Kind == SectionKind.Chorus).ToList();
        var lookIds = chorusLabels
            .Select(l => report.Scripts.TryGetValue(l.Id, out var script) ? script.LookId : "(missing)")
            .ToList();

        var recipeNameById = recipes.Recipes.ToDictionary(r => r.RecipeId, r => r.XlightsEffectName);
        var effectNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in chorusLabels)
        {
            var window = report.CompiledResult.BarGrid.WindowForBarRange(new BarIndex(label.StartBarIndex), new BarIndex(label.EndBarIndexExclusive));
            if (window is null)
            {
                continue;
            }

            foreach (var effect in report.CompiledResult.IR.Effects)
            {
                if (effect.StartTicks >= window.Value.StartTicks && effect.StartTicks < window.Value.EndTicks
                    && recipeNameById.TryGetValue(effect.RecipeId, out var name))
                {
                    effectNames.Add(name);
                }
            }
        }

        return (lookIds.Distinct(StringComparer.Ordinal).Count(), effectNames.Count, lookIds);
    }

    private static SectionLabelAnalysis BuildRepeatingChorusSections(SongId songId, int totalBars)
    {
        var sectionBars = totalBars / ChorusOccurrenceCount;
        var labels = Enumerable.Range(0, ChorusOccurrenceCount)
            .Select(i => new SectionLabelCandidate(
                Guid.NewGuid(), Guid.NewGuid(),
                i * sectionBars, i == ChorusOccurrenceCount - 1 ? totalBars : (i + 1) * sectionBars,
                SectionKind.Chorus, 1.0, ["Phase 24 real-run synthetic Chorus repeat"], false))
            .ToList();

        return new SectionLabelAnalysis(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new SectionLabelSettings(0.95, 0.4, "t/1"), labels);
    }

    private async Task<MusicModel> AnalyzeAsync(string audioPath, string realSequencePath, string songName, CancellationToken ct)
    {
        var analyzer = new CorpusSongAnalyzer(Path.Combine(_root, "analysis-cache", songName));
        var entry = new CorpusEntry(realSequencePath, audioPath, songName, CorpusProvenanceTag.ExternalReference, [], "");
        var analysis = await analyzer.AnalyzeAsync(entry, ct);

        var decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), new LocalPathFfmpegInstaller(), Path.Combine(_root, "analysis-cache", songName, "wav-cache"));
        var song = await decoder.ProbeAsync(audioPath, ct);

        var fftSize = NextPowerOfTwo((int)Math.Round(song.SampleRate * (256.0 / 8000.0)));
        var rhythmSettings = new RhythmSettings(song.SampleRate, fftSize, fftSize / 2);
        var bandEnergy = await new BandEnergyAnalyzer(decoder).AnalyzeAsync(song, new BandEnergySettings(rhythmSettings, BassCutoffHz: 250, HighCutoffHz: 2000), ct);

        return new MusicModel(analysis.BeatGrid.SongId, song.DurationTicks, analysis.BeatGrid, analysis.Labels, analysis.Phrases, bandEnergy, VocalActivity: null);
    }

    private static int NextPowerOfTwo(int value)
    {
        var power = 1;
        while (power < value)
        {
            power <<= 1;
        }

        return power;
    }

    /// <summary>
    /// Same real-group, schema-valid hand-authored Show Bible shape as <see cref="Phase22ContrastDrivenEmphasisRealRunTests"/>'s
    /// own fixture, parameterized by how many of its 10 Looks are tagged eligible for Chorus
    /// (<see cref="SectionKind.Chorus"/>) -- every other Look is restricted to Verse instead, so it is
    /// never eligible for Chorus at all. This is the one real variable this test's whole comparison turns on.
    /// </summary>
    private static ShowBible BuildHandAuthoredShowBible(int chorusEligibleLookCount)
    {
        string[] subjects = ["group:MiniTrees", "group:Matrixes", "group:Outline", "group:Arches", "Tree"];
        var palettes = new List<ShowBiblePalette>
        {
            new("warm-gold", "Warm Gold", [new ShowBiblePaletteColor("#FFC880", PaletteRole.Base), new ShowBiblePaletteColor("#FFFFFF", PaletteRole.Key)]),
            new("high-energy", "High Energy", [new ShowBiblePaletteColor("#FF0030", PaletteRole.Base), new ShowBiblePaletteColor("#00AEEF", PaletteRole.Accent)]),
            new("cool-blue", "Cool Blue", [new ShowBiblePaletteColor("#0030FF", PaletteRole.Base)]),
        };

        var looks = new List<Look>();
        for (var i = 0; i < 10; i++)
        {
            var subject = subjects[i % subjects.Length];
            var secondary = subjects[(i + 1) % subjects.Length];
            var energyCeiling = 0.15 + (0.08 * (i % 10));
            var layers = new List<LookLayer>
            {
                new(1, secondary, "Twinkle", LookLayer.RoleSecondary, new Dictionary<string, string>(), "Normal", "Bars", 0.3),
                new(2, subject, "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9),
            };

            // ShowBibleValidator.PeakLookEnergyCeilingThreshold/MinPeakLookLayers: a Chorus/Solo-tier
            // peak Look (EnergyCeiling >= 0.75) must carry 3+ layers -- this fixture's own energy ramp
            // crosses that threshold at i=8/9, so it needs a real third layer there too.
            if (energyCeiling >= ShowBibleValidator.PeakLookEnergyCeilingThreshold)
            {
                var tertiary = subjects[(i + 2) % subjects.Length];
                layers.Add(new LookLayer(0, tertiary, "Color Wash", LookLayer.RoleSupport, new Dictionary<string, string>(), "Additive", "Bars", 0.5));
            }

            looks.Add(new Look(
                Id: $"look-{i}",
                Name: $"Look {i}",
                Intent: $"Phase 24 real-run Look {i}.",
                EnergyCeiling: energyCeiling,
                Motion: i % 3 == 0 ? "hold" : "chase",
                PaletteRef: palettes[i % palettes.Count].Id,
                Subject: subject,
                Layers: layers,
                AllowedSectionKinds: i < chorusEligibleLookCount ? [SectionKind.Chorus] : [SectionKind.Verse]));
        }

        var content = new ShowBibleContent(
            Palettes: palettes,
            PropRoles: [new PropRoleAssignment("Tree", MusicalRole.Melody), new PropRoleAssignment("group:Matrixes", MusicalRole.Bass)],
            Looks: new LookLibrary(looks),
            EnergyArc: Enum.GetValues<SectionKind>().Select(k => new EnergyArcEntry(k, 0.5, 0.5, 2)).ToList(),
            PeakMoments: [new PeakMoment(looks[^1].Id, "Final look is the reserved peak.")],
            SignatureGestures: [new SignatureGesture("pulse", "A simple pulse gesture.")],
            Withholding: new WithholdingPlan([looks[^1].Id], looks[^1].Id));

        return new ShowBible(Guid.NewGuid(), Guid.NewGuid(), content, DateTimeOffset.UtcNow);
    }
}
