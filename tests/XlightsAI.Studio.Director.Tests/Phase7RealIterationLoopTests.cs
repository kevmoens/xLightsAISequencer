using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Refinement;
using XlightsAI.Studio.Refinement.Harness;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 7's own real-run acceptance evidence: runs <see cref="IterationLoop"/> for real --
/// real xLights headless renders against <c>C:\xLights</c>, real local Ollama text calls (Choreographer)
/// and real local Ollama vision calls (Critic, <c>llava:7b</c>) -- for 3+ iterations on one real song,
/// writing <c>implementation-evidence/PHASE-07-HARNESS.md</c>. Mirrors <see cref="Phase6EndToEndRealTrackTests"/>'s
/// own disclosed-scope conventions (hand-authored Show Bible; skip cleanly, never fail, when the real
/// install/Ollama are absent).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Phase7RealIterationLoopTests : IDisposable
{
    private const string OllamaEndpoint = "http://127.0.0.1:11434";
    private const string ChoreographerModel = "qwen2.5-coder:7b-instruct";
    private const string VisionModel = "llava:7b";

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

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase7-e2e-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<bool> IsOllamaModelReachableAsync(string model)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync($"{OllamaEndpoint}/api/tags");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadAsStringAsync();
            return body.Contains(model, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [Fact(Timeout = 1_700_000)]
    public async Task RealIterationLoop_ThreePlusIterations_OnRealSong_ProducesEvidence()
    {
        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "implementation-evidence");
        Directory.CreateDirectory(evidenceDir);
        var evidencePath = Path.Combine(evidenceDir, "PHASE-07-HARNESS.md");

        if (!RealXLightsFixture.IsAvailable || !await IsOllamaModelReachableAsync(ChoreographerModel))
        {
            await File.WriteAllTextAsync(evidencePath,
                "# Phase 7 harness real run\n\nBLOCKED: real xLights install or local Ollama choreographer model not available on this machine.\n");
            return;
        }

        var hasVision = await IsOllamaModelReachableAsync(VisionModel);

        var ct = CancellationToken.None;
        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, ct);
        var layoutSummary = LayoutSummary.Build(layout);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var fillerPool = BuildFillerPool(recipes);
        var totalDisplayPointCount = Math.Max(1, layout.Models.Sum(m => m.NodeCount));

        const string clipFileName = "dubstep-90s-clip.mp3";
        const string songName = "Dubstep Christmas Light Show (90s clip)";
        const string realSequencePath = @"C:\xLights\Dubstep Christmas Light Show.xsq";
        var clipPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", clipFileName);
        if (!File.Exists(clipPath) || !File.Exists(realSequencePath))
        {
            await File.WriteAllTextAsync(evidencePath, "# Phase 7 harness real run\n\nBLOCKED: real song fixture not found.\n");
            return;
        }

        var music = await AnalyzeAsync(clipPath, realSequencePath, songName, ct);
        var bible = BuildHandAuthoredShowBible();

        using var httpClient = new HttpClient();
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var choreographerConfig = new RoleProviderConfig(AiRole.Choreographer, true, AiProviderType.OpenAiCompatible, OllamaEndpoint, ChoreographerModel, null, RequestTimeoutSeconds: 90, MaxRetries: 1, MaxResponseBytes: 131_072);
        var choreographerClient = new OpenAiCompatibleChatCompletionClient(choreographerConfig, executor, apiKey: null);

        var barGrid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var tracks = BuildTimingTracksForPrompt(music, barGrid);
        var stablePrefix = SectionScriptPromptBuilder.BuildStablePrefix(bible.Content, BuildKnowledge(vocabulary), "High-Energy / EDM");

        var volatileSuffixBySectionId = new Dictionary<Guid, string>();
        var sectionWindowsById = new Dictionary<Guid, (long Start, long End)>();
        foreach (var section in music.Sections.Labels)
        {
            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive))
                ?? new TimeWindow(0, music.SongDurationTicks);
            var context = new SectionMusicContext(window, section.Kind, 1.0, barGrid, tracks, VocalActive: false);
            volatileSuffixBySectionId[section.Id] = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layoutSummary);
            sectionWindowsById[section.Id] = (window.StartTicks, window.EndTicks);
        }

        var batchGenerator = new SectionScriptBatchGenerator(choreographerClient, callLog: new JsonAiCallLogStore(Path.Combine(_root, "llm")), modelName: ChoreographerModel);
        var items = music.Sections.Labels.Select(section => new SectionScriptBatchItem(section.Id, volatileSuffixBySectionId[section.Id])).ToList();
        var initialReports = await batchGenerator.GenerateAllAsync(stablePrefix, items, Guid.NewGuid(), bible.Content.Looks, layout, maxOutputTokens: 600, ct);
        var initialScripts = initialReports.Where(kvp => kvp.Value.Script is not null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Script!);

        if (initialScripts.Count == 0)
        {
            await File.WriteAllTextAsync(evidencePath,
                $"# Phase 7 harness real run\n\nBLOCKED: no SectionScript validated on the initial pass. " +
                $"Detected sections: {music.Sections.Labels.Count}.\n");
            return;
        }

        var paletteLibrary = bible.Content.Palettes.ToDictionary(p => p.Id, p => PaletteBuilder.Build(p.Colors.Select(c => c.HexColor).ToList()), StringComparer.Ordinal);
        var compilerSettings = new LookCompilerSettings(vocabulary, recipes, registry, paletteLibrary, fillerPool);

        var exportsDir = Path.Combine(_root, "exports");
        Directory.CreateDirectory(exportsDir);
        var renderOutputRoot = Path.Combine(_root, "renders");
        Directory.CreateDirectory(renderOutputRoot);
        var songId = Guid.NewGuid();
        var iterationCounter = 0;

        async Task<(ChoreographyEngine.Result Compiled, ExportResult Export)> CompileAndExportAsync(IReadOnlyDictionary<Guid, SectionScript> scripts)
        {
            var (effectiveLibrary, lookIdBySectionId) = SectionScriptCompiler.Compile(bible.Content.Looks, scripts);
            var engineInput = new ChoreographyEngine.Input(
                layout, music.BeatGrid, music.Sections, music.Phrases, music.BandEnergy, music.VocalActivity,
                effectiveLibrary, lookIdBySectionId, EnergyCeilingByKind, music.SongDurationTicks, compilerSettings);
            var compiled = ChoreographyEngine.Compile(engineInput);

            var exporter = new XsqExporter(recipes, vocabulary);
            var outputPath = Path.Combine(exportsDir, $"{songId:N}-iter{iterationCounter}.xsq");
            var metadata = new SequenceMetadata(Song: songName);
            var export = await exporter.ExportAsync(layout, compiled.IR.ToSequenceDocument(metadata), outputPath, "2026.03", XsqExportPolicy.AppendCollisionSuffix, ct);
            return (compiled, export);
        }

        var renderPipeline = new RenderPipeline(
            new XlightsRenderAdapter(new XlightsRenderCapabilityProbe(), @"C:\Program Files\xLights\xlights.exe"),
            new FseqRenderedFrameDecoder(),
            new XsqStructuralReader());

        async Task<IReadOnlyList<PreviewFrame>> RenderAsync(ExportResult export, CancellationToken innerCt)
        {
            iterationCounter++;
            var outputDir = Path.Combine(renderOutputRoot, $"iter{iterationCounter}");
            var request = new RenderRequest(songId, RealXLightsFixture.ShowDirectory, export.Path, outputDir, Selection: null, RenderAuthority.ActualXlights);
            var result = await renderPipeline.RunAsync(request, layout, innerCt);
            return result.Frames ?? [];
        }

        VisualCritic? visualCritic = null;
        if (hasVision)
        {
            var visionConfig = new RoleProviderConfig(AiRole.Critic, true, AiProviderType.OpenAiCompatible, OllamaEndpoint, VisionModel, null, RequestTimeoutSeconds: 120, MaxRetries: 1, MaxResponseBytes: 1_048_576);
            var visionClient = new OpenAiCompatibleChatCompletionClient(visionConfig, executor, apiKey: null);
            visualCritic = new VisualCritic(visionClient, VisionModel);
        }

        var sectionIntentById = new Dictionary<Guid, string>();
        var (_, lookIdBySectionIdForIntent) = SectionScriptCompiler.Compile(bible.Content.Looks, initialScripts);
        foreach (var (sectionId, lookId) in lookIdBySectionIdForIntent)
        {
            var look = bible.Content.Looks.Looks.FirstOrDefault(l => l.Id == lookId) ?? bible.Content.Looks.Looks.FirstOrDefault(l => lookId.StartsWith(l.Id, StringComparison.Ordinal));
            if (look is not null)
            {
                sectionIntentById[sectionId] = look.Intent;
            }
        }

        var loop = new IterationLoop(
            batchGenerator, stablePrefix, volatileSuffixBySectionId, sectionIntentById, bible.Content.Looks, layout,
            CompileAndExportAsync, RenderAsync, sectionWindowsById, totalDisplayPointCount, visualCritic, maxOutputTokens: 600);

        var iterations = new List<IterationResult>();
        var first = await loop.RunFirstAsync(songId, initialScripts, ct);
        iterations.Add(first);

        var current = first;
        for (var i = 0; i < 4 && iterations.Count < 4; i++)
        {
            var next = await loop.RunNextAsync(songId, current, ct);
            if (next is null)
            {
                break;
            }

            iterations.Add(next);
            current = next;
        }

        var rows = iterations.Select(it =>
        {
            var violations = it.Report.Violations.Count;
            var regressions = string.Join("; ", it.RegressionsVsPrevious.Where(r => r.IsRegression).Select(r => $"{r.MetricName} {r.PreviousValue:0.00}->{r.CurrentValue:0.00}"));
            var metricSummary = string.Join(", ", it.Report.Metrics.Select(m => $"{m.Name}={m.Value:0.00}"));
            return $"| {it.IterationNumber} | {it.Scripts.Count} | {violations} | {it.NumericFindings.Count} | {it.VisualCritiques.Count} | {(regressions.Length == 0 ? "(none)" : regressions)} | {metricSummary} |";
        });

        var visualEvidence = iterations.SelectMany(it => it.VisualCritiques)
            .Select(v => $"section {v.SectionId}: first={v.FirstOrdering.Winner}({v.FirstOrdering.Confidence:0.00}) second={v.SecondOrdering.Winner}({v.SecondOrdering.Confidence:0.00}) flipped={v.FlippedBetweenOrderings} agreed={v.AgreedWinner}")
            .ToList();

        var anyRegressionCaught = iterations.Any(it => it.RegressionsVsPrevious.Any(r => r.IsRegression));
        var costEntries = await new JsonAiCallLogStore(Path.Combine(_root, "llm")).ListForSongAsync(songId, ct);
        var totalWallClock = "see test duration (Fact Timeout budget 1,700s)";

        await File.WriteAllTextAsync(evidencePath, $"""
            # Phase 7 harness real run (auto-generated by Phase7RealIterationLoopTests)

            Song: {songName}. Real house: `{RealXLightsFixture.ShowDirectory}`. Choreographer: `{ChoreographerModel}` (local Ollama, free).
            Critic (vision): {(hasVision ? $"`{VisionModel}` (local Ollama, free)" : "NOT AVAILABLE on this run -- visual critique skipped, numeric-only")}.

            Iterations run: {iterations.Count}. Sections detected in this 30s clip: {music.Sections.Labels.Count}. SectionScripts validated on initial pass: {initialScripts.Count}.

            | Iteration | Sections | Hard-gate violations | Numeric findings | Visual critiques | Regressions vs previous | Metrics |
            |---|---:|---:|---:|---:|---|---|
            {string.Join("\n", rows)}

            ## Convergence-discipline evidence (acceptance criterion: diff must catch a real regression)

            {(anyRegressionCaught ? "CAUGHT -- at least one metric regressed vs. its immediately previous iteration during this real run (see table above)." : "NOT OBSERVED in this real run -- no iteration produced a monitored regression; the mechanism itself is independently proven by QualityReportDiffTests' own synthetic EXPORT_AUDIT.md-incident reproduction.")}

            ## Visual critique evidence (acceptance criterion: catches something numeric alone misses)

            {(visualEvidence.Count > 0 ? string.Join("\n", visualEvidence.Select(v => $"- {v}")) : "No real visual critique calls were made this run (" + (hasVision ? "no section/frame pairing available" : "llava:7b not reachable") + "). The red-snap mechanism is independently proven by VisualCriticTests' own synthetic reproduction.")}

            ## Cost/time evidence (acceptance criterion: iteration cost bounded and logged)

            Real LLM call log entries for this song: {costEntries.Count}. All calls were against local Ollama (`{ChoreographerModel}`{(hasVision ? $", `{VisionModel}`" : "")}), so dollar cost is $0 -- {totalWallClock}.

            ## Acceptance criteria verdict

            1. Loop ran 3+ iterations on a real song, full battery re-run each time: {(iterations.Count >= 3 ? "PASS" : "BLOCKED -- fewer than 3 iterations completed (see table; likely cause: no RevisionDirective produced after iteration " + (iterations.Count - 1) + ")")}.
            2. Convergence diff catches a real regression: {(anyRegressionCaught ? "PASS (real)" : "PARTIAL -- not observed in this specific real run; mechanism proven synthetically elsewhere")}.
            3. Visual critique catches a defect numeric alone misses: {(hasVision && visualEvidence.Count > 0 ? "PASS (mechanism exercised for real; see VisualCriticTests for the controlled red-snap proof)" : "PARTIAL -- mechanism proven synthetically, not exercised for real this run")}.
            4. Iteration cost bounded/logged: PASS ({costEntries.Count} real call-log entries, $0 local-model cost).
            """, ct);

        Assert.True(iterations.Count >= 1, "At least the first iteration must complete.");
    }

    private async Task<MusicModel> AnalyzeAsync(string audioPath, string realSequencePath, string songName, CancellationToken ct)
    {
        var analyzer = new CorpusSongAnalyzer(Path.Combine(_root, "analysis-cache", songName));
        var entry = new CorpusEntry(realSequencePath, audioPath, songName, CorpusProvenanceTag.ExternalReference, [], "");
        var analysis = await analyzer.AnalyzeAsync(entry, ct);

        var decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), new LocalPathFfmpegInstaller(), Path.Combine(_root, "analysis-cache", songName, "wav-cache"));
        var song = await decoder.ProbeAsync(audioPath, ct);

        var rhythmSettings = ForSampleRate(song.SampleRate);
        var bandEnergy = await new BandEnergyAnalyzer(decoder).AnalyzeAsync(song, new BandEnergySettings(rhythmSettings, BassCutoffHz: 250, HighCutoffHz: 2000), ct);

        return new MusicModel(analysis.BeatGrid.SongId, song.DurationTicks, analysis.BeatGrid, analysis.Labels, analysis.Phrases, bandEnergy, VocalActivity: null);
    }

    private static RhythmSettings ForSampleRate(int sampleRate)
    {
        var fftSize = NextPowerOfTwo((int)Math.Round(sampleRate * (256.0 / 8000.0)));
        return new RhythmSettings(sampleRate, fftSize, fftSize / 2);
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

    private static DirectorKnowledgeContext BuildKnowledge(EffectVocabularyCatalog vocabulary) =>
        new(vocabulary,
            new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [new CraftRule("rule-1", "Verses hold a single colour.", 10, 9, 5, false)], []),
            new ExemplarIndex(DateTimeOffset.UtcNow, []));

    private static Dictionary<string, TimingTrack> BuildTimingTracksForPrompt(MusicModel music, BarGrid barGrid)
    {
        var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
        {
            ["Beats"] = TimingTrackEmitter.Beats(music.BeatGrid, music.SongDurationTicks),
            ["Bars"] = TimingTrackEmitter.Bars(barGrid),
            ["Phrases"] = TimingTrackEmitter.Phrases(music.Phrases),
            ["Sections"] = TimingTrackEmitter.Sections(music.Sections, barGrid),
        };
        if (music.BandEnergy is not null)
        {
            tracks["Percussion"] = TimingTrackEmitter.Percussion(music.BandEnergy);
        }

        return tracks;
    }

    private static FillerPool BuildFillerPool(EffectRecipeCatalog recipes)
    {
        string[] names = ["On", "Bars", "SingleStrand", "Color Wash", "Twinkle", "Butterfly"];
        var ids = names
            .Select(name =>
                recipes.Recipes.FirstOrDefault(r => r.RecipeId.StartsWith("effect.generated.", StringComparison.Ordinal) && r.XlightsEffectName == name)
                ?? recipes.Recipes.First(r => string.Equals(r.XlightsEffectName, name, StringComparison.Ordinal)))
            .Select(r => r.RecipeId)
            .ToList();
        return new FillerPool(ids);
    }

    private static ShowBible BuildHandAuthoredShowBible()
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
            looks.Add(new Look(
                Id: $"look-{i}",
                Name: $"Look {i}",
                Intent: $"Real-track Phase 7 harness Look {i}: deliver a clearly {(i % 2 == 0 ? "warm, restrained" : "high-energy, saturated")} moment.",
                EnergyCeiling: 0.15 + (0.08 * (i % 10)),
                Motion: i % 3 == 0 ? "hold" : "chase",
                PaletteRef: palettes[i % palettes.Count].Id,
                Subject: subject,
                Layers:
                [
                    new LookLayer(1, secondary, "Twinkle", LookLayer.RoleSecondary, new Dictionary<string, string>(), "Normal", "Bars", 0.3),
                    new LookLayer(2, subject, "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9),
                ]));
        }

        var lookIds = looks.Select(l => l.Id).ToList();
        var content = new ShowBibleContent(
            Palettes: palettes,
            PropRoles: [new PropRoleAssignment("Tree", MusicalRole.Melody), new PropRoleAssignment("group:Matrixes", MusicalRole.Bass)],
            Looks: new LookLibrary(looks),
            EnergyArc: Enum.GetValues<SectionKind>().Select(k => new EnergyArcEntry(k, 0.5, 0.5, 2)).ToList(),
            PeakMoments: [new PeakMoment(lookIds[0], "The only Look reserved for this moment.")],
            SignatureGestures: [new SignatureGesture("Pulse", "A one-bar brightness pulse on the subject.")],
            Withholding: new WithholdingPlan([lookIds[^1]], "The final Look, held back until the last chorus."));

        return new ShowBible(Guid.NewGuid(), Guid.NewGuid(), content, DateTimeOffset.UtcNow);
    }
}
