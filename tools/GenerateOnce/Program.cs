using System.Xml.Linq;
using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.GenerateOnce;

/// <summary>
/// One-off manual runner that drives a REAL end-to-end
/// <see cref="AiChoreographyWholeSongGenerator"/> run: a real song file, a real local show layout
/// (<c>C:\xLights</c>), and whichever Director/Choreographer provider is configured in the app's own
/// Settings screen -- then exports the resulting <see cref="SequenceDocument"/> to a real <c>.xsq</c>
/// file. No mocks/stubs anywhere in this path.
///
/// Mirrors <c>XlightsAI.Studio.App.Program.Main</c>'s own composition root exactly (same %AppData%
/// directories, same store/decoder/runner wiring) so this exercises the identical pipeline the Shell
/// app itself uses, not a parallel reimplementation.
/// </summary>
public static class Program
{
    private const string SongPath = @"C:\xLights\Bad Feeling-Jagwar Twin (Clean_Lyrics).mp3";
    private const string ShowDirectory = @"C:\xLights";
    private const string EffectMetadataDirectory = @"C:\Program Files\xLights\effectmetadata";

    public static async Task<int> Main()
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XlightsAI.Studio");

        Console.WriteLine($"[GenerateOnce] AppData directory: {appDataDirectory}");

        // ---- Step 1: use whatever Director/Choreographer provider config is already saved in the REAL
        // Settings-screen-backed location (the user now configures this directly in the app's Settings
        // screen) instead of forcing the Ollama bootstrap config this tool used for its first few runs --
        // overwriting it here would silently clobber a real provider (Anthropic, Azure AI Foundry, etc)
        // the user has since set up. ----
        var roleProviderConfigStore = new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers"));
        foreach (var role in new[] { AiRole.Director, AiRole.Choreographer })
        {
            var existing = await roleProviderConfigStore.LoadAsync(role, CancellationToken.None);
            Console.WriteLine($"[GenerateOnce] {role} provider config: Enabled={existing.Enabled}, Provider={existing.ProviderType}, Endpoint={existing.Endpoint}, Model={existing.ModelName}");
        }

        if (!File.Exists(SongPath))
        {
            Console.Error.WriteLine($"[GenerateOnce] FATAL: song file not found at '{SongPath}'.");
            return 1;
        }

        if (!Directory.Exists(EffectMetadataDirectory))
        {
            Console.Error.WriteLine($"[GenerateOnce] FATAL: effect metadata directory not found at '{EffectMetadataDirectory}'.");
            return 1;
        }

        // ---- Step 2/3: same composition-root wiring as XlightsAI.Studio.App.Program.Main. ----
        var downloadsDirectory = Path.Combine(appDataDirectory, "downloads");
        var releaseAssetDownloader = new HttpReleaseAssetDownloader();
        var assetInstaller = new AssetInstaller(downloadsDirectory, releaseAssetDownloader);
        var ffmpegInstaller = new FfmpegInstaller(Path.Combine(appDataDirectory, "tools", "ffmpeg"), assetInstaller);
        var audioDecoder = new TranscodingAudioDecoder(
            new WavAudioDecoder(), ffmpegInstaller, Path.Combine(appDataDirectory, "audio-transcode-cache"));
        var songAssetStore = new JsonSongAssetStore(Path.Combine(appDataDirectory, "audio-assets"));

        var stemModelAssetManager = new ModelAssetManager(
            Path.Combine(appDataDirectory, "tools", "stem-separation-model"), new HttpModelAssetDownloader());
        var stemSeparator = new SpectrogramStemSeparatorAdapter(
            new SpectrogramOnnxStemSeparator(
                stemModelAssetManager, new TarBzip2ModelArchiveExpander(), new WavStereoAudioDecoder(),
                Path.Combine(appDataDirectory, "stem-separation-output")),
            SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe);

        var audioAnalysisRunner = new AudioAnalysisRunner(
            audioDecoder, stemSeparator, SherpaOnnxSpleeterModelAsset.Archive,
            new JsonOnsetAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "onsets")),
            new JsonTempoAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "tempo")),
            new JsonBeatGridStore(Path.Combine(appDataDirectory, "analysis-cache", "beatgrid")),
            new JsonEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "energy")),
            new JsonPitchTrackStore(Path.Combine(appDataDirectory, "analysis-cache", "pitch")),
            new JsonStemActivityResultStore(Path.Combine(appDataDirectory, "analysis-cache", "vocal-activity")),
            new JsonBandEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "band-energy")));

        var structureAnalysisRunner = new StructureAnalysisRunner(
            new JsonBarFingerprintStore(Path.Combine(appDataDirectory, "analysis-cache", "fingerprints")),
            new JsonSectionBoundaryStore(Path.Combine(appDataDirectory, "analysis-cache", "boundaries")),
            new JsonSectionLabelStore(Path.Combine(appDataDirectory, "analysis-cache", "labels")),
            new JsonSectionLabelOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "label-overrides")),
            new JsonPhraseSegmentationStore(Path.Combine(appDataDirectory, "analysis-cache", "phrases")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "phrase-overrides")),
            new JsonMotifClusteringStore(Path.Combine(appDataDirectory, "analysis-cache", "motifs")));

        var credentialStore = new DpapiCredentialStore(Path.Combine(appDataDirectory, "credentials"));

        // BUG FOUND DURING THIS REAL RUN: XlightsAI.Studio.App.Program.Main wires AiRoleClientResolver
        // with a plain `new HttpClient()`, whose default Timeout is 100 seconds. That HttpClient-level
        // timeout fires independently of (and tighter than) RoleProviderConfig.RequestTimeoutSeconds
        // (set to 300 above) -- HttpClient.Timeout governs the whole request regardless of any
        // CancellationToken passed to SendAsync. Against a real local Ollama model under real load, a
        // single chat-completion call legitimately took longer than 100s and every attempt was killed
        // by this HttpClient-level timeout, not the configured per-call timeout. Worked around here
        // (tool-only change, library code intentionally left untouched per this task's scope) by
        // giving the HttpClient itself an effectively unbounded timeout and relying solely on
        // RoleProviderConfig.RequestTimeoutSeconds / the executor's own per-attempt TimeSpan for the
        // real timeout behavior -- which is what the config value is supposed to control in the first
        // place. The production Program.cs has this same latent bug and should be fixed there too.
        var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var aiRoleClientResolver = new AiRoleClientResolver(roleProviderConfigStore, credentialStore, httpClient);

        var projectDirectory = Path.Combine(appDataDirectory, "ai-choreography", "generate-once");
        // Confirmed against a real rendered video: the default Traditional/Classic style (low density,
        // long held effects) produced a slow uniform color wash for this song, which is an energetic
        // alt/trap-leaning track much closer to the High-Energy/EDM preset's own target.
        // Build-plan Phase 10 §3f: same %AppData%/corpus-knowledge directory the real app's
        // Program.cs now wires RunCorpusMiningCommand/AiChoreographyWholeSongGenerator to agree on.
        var corpusKnowledgeDirectory = Path.Combine(appDataDirectory, "corpus-knowledge");
        var generator = new AiChoreographyWholeSongGenerator(
            audioAnalysisRunner, structureAnalysisRunner, aiRoleClientResolver,
            EffectMetadataDirectory, projectDirectory, knowledgeDirectory: corpusKnowledgeDirectory,
            style: XlightsAI.Studio.Director.StylePresetCatalog.HighEnergyEdmId);

        // ---- Step 2.1: import the real show layout. ----
        var showLayoutImporter = new ShowLayoutImporter();
        Console.WriteLine($"[GenerateOnce] Importing show layout from '{ShowDirectory}'...");
        var layout = await showLayoutImporter.ImportReadOnlyAsync(ShowDirectory, CancellationToken.None);
        Console.WriteLine($"[GenerateOnce] Layout imported: {layout.Models.Count} models.");

        // ---- Step 2.2: probe the real song file. ----
        Console.WriteLine($"[GenerateOnce] Probing song '{SongPath}'...");
        var song = await audioDecoder.ProbeAsync(SongPath, CancellationToken.None);
        await songAssetStore.SaveAsync(song, CancellationToken.None);
        Console.WriteLine($"[GenerateOnce] Song probed: Id={song.Id}, Duration={TimeSpan.FromTicks(song.DurationTicks)}, SampleRate={song.SampleRate}.");

        // ---- Step 6: generate. A generous but bounded timeout (60 minutes) so a hung local model
        // cannot run forever, but no artificially short cancellation cuts off a real multi-minute
        // multi-call LLM pipeline. 60 minutes proved too short once maxOutputTokens was raised to 16000
        // for the thinking-model reasoning-budget fix above: a real attempt was still legitimately
        // progressing (no empty-content failure) when that ceiling cut it off. 240 minutes leaves room
        // for up to 3 Show Bible retry attempts plus ~8-20 SectionScript calls, still bounded. ----
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(240));
        Console.WriteLine("[GenerateOnce] Starting AiChoreographyWholeSongGenerator.GenerateAsync (calls the configured AI provider many times; may take a while)...");
        var startedAt = DateTimeOffset.UtcNow;

        WholeSongGenerationResult result;
        try
        {
            result = await generator.GenerateAsync(song, layout, cts.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GenerateOnce] FATAL: generation failed after {DateTimeOffset.UtcNow - startedAt}: {ex}");
            return 1;
        }

        var elapsed = DateTimeOffset.UtcNow - startedAt;
        Console.WriteLine($"[GenerateOnce] Generation completed in {elapsed}.");

        // ---- Step 7: export to a real .xsq, using the SAME recipe/vocabulary catalog the generator
        // itself built from the real effectmetadata directory, so every recipe id the generator used
        // resolves to a real xLights effect name/settings shape during export. ----
        var catalogs = EffectCatalogFactory.BuildFromMetadataDirectory(EffectMetadataDirectory);
        var exporter = new XsqExporter(catalogs.Recipes, catalogs.Vocabulary);

        var outputDirectory = Path.Combine(ShowDirectory, "generated");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "Bad Feeling - Jagwar Twin.xsq");

        var policy = File.Exists(outputPath) ? XsqExportPolicy.OverwriteWithBackup : XsqExportPolicy.RejectIfExists;

        var exportResult = await exporter.ExportAsync(layout, result.Document, outputPath, "2026.03", policy, CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("==================== GenerateOnce report ====================");
        Console.WriteLine($"Final .xsq path: {exportResult.Path}");
        if (exportResult.BackupPath is not null)
        {
            Console.WriteLine($"(Pre-existing file backed up to: {exportResult.BackupPath})");
        }

        var effects = result.Document.Effects;
        var recipesById = catalogs.Recipes.Recipes.ToDictionary(r => r.RecipeId, r => r.XlightsEffectName, StringComparer.Ordinal);
        var distinctEffectNames = effects
            .Select(e => recipesById.TryGetValue(e.RecipeId, out var name) ? name : $"(unknown recipe: {e.RecipeId})")
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"Total EffectInstances generated: {effects.Count}");
        Console.WriteLine($"Distinct effect names ({distinctEffectNames.Count}): {string.Join(", ", distinctEffectNames)}");

        if (distinctEffectNames.Count <= 1)
        {
            Console.WriteLine("WARNING: only one (or zero) distinct effect name was produced -- this looks like the old flat \"On\" bug, not real LLM-driven choreography.");
        }

        // Build-plan Phase 13 §5 acceptance criteria: distinct-effect-name count/dominant-share (2026-10-30
        // baseline: 9 distinct names, top-2 combined ~37%) and max concurrent EffectLayers on any one prop
        // (baseline: 3 on the richest prop, 0-2 on secondary ones) -- both reported here directly from the
        // in-memory compiled effects so every real GenerateOnce run self-reports this phase's own metrics.
        var namesByEffect = effects.Select(e => recipesById.TryGetValue(e.RecipeId, out var name) ? name : null).Where(n => n is not null).ToList();
        if (namesByEffect.Count > 0)
        {
            var topEffect = namesByEffect.GroupBy(n => n, StringComparer.Ordinal).OrderByDescending(g => g.Count()).First();
            Console.WriteLine($"Most-used effect: '{topEffect.Key}' at {topEffect.Count() / (double)namesByEffect.Count:P1} of all {namesByEffect.Count} name-resolved effects (2026-10-30 baseline: top 2 effects combined ~37%, 9 distinct names total).");
        }

        var maxConcurrentLayersByModel = effects
            .GroupBy(e => e.Target)
            .Select(g => (Model: g.Key, MaxConcurrent: MaxConcurrentLayers(g.ToList())))
            .OrderByDescending(x => x.MaxConcurrent)
            .ToList();
        if (maxConcurrentLayersByModel.Count > 0)
        {
            var richest = maxConcurrentLayersByModel[0];
            Console.WriteLine($"Max concurrent EffectLayers on any one prop: {richest.MaxConcurrent} (on '{richest.Model.Value}') -- 2026-10-30 baseline: 3 on the richest prop, 0-2 on typical secondary props.");
        }

        var xsqText = await File.ReadAllTextAsync(exportResult.Path);
        var doc = XDocument.Parse(xsqText);
        var mediaFileElement = doc.Descendants("mediaFile").FirstOrDefault();
        var mediaFileValue = mediaFileElement?.Value;
        Console.WriteLine($"<mediaFile> in exported .xsq: {(string.IsNullOrEmpty(mediaFileValue) ? "(EMPTY/MISSING)" : mediaFileValue)}");
        Console.WriteLine($"<mediaFile> matches real song path: {string.Equals(mediaFileValue, SongPath, StringComparison.OrdinalIgnoreCase)}");
        Console.WriteLine($"Wall-clock generation time: {elapsed}");

        // Build-plan Phase 12 §5 acceptance criteria: the same bucketed-start-time method
        // docs/AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md used to measure the pre-Phase-12 ~1.2x
        // flat baseline (vs the real reference's own ~4.7x) -- every effect's own start time bucketed into
        // 10-second windows across the whole song, reported here directly from the in-memory
        // EffectInstance list (no .xsq re-parse needed, since this tool already holds the real compiled
        // result) so every real GenerateOnce run self-reports this phase's own acceptance metric.
        const long ticksPerSecond = 10_000_000L;
        const long bucketSeconds = 10L;
        var bucketTicks = bucketSeconds * ticksPerSecond;
        var bucketCounts = effects
            .GroupBy(e => e.StartTicks / bucketTicks)
            .ToDictionary(g => g.Key, g => g.Count());
        if (bucketCounts.Count > 0)
        {
            var minCount = bucketCounts.Values.Min();
            var maxCount = bucketCounts.Values.Max();
            var spread = minCount > 0 ? maxCount / (double)minCount : double.PositiveInfinity;
            Console.WriteLine();
            Console.WriteLine($"Change-density spread (starts/{bucketSeconds}s across {bucketCounts.Count} bucket(s)): min={minCount}, max={maxCount}, spread={(double.IsPositiveInfinity(spread) ? "inf (min bucket is 0)" : $"{spread:0.00}x")}.");
            Console.WriteLine($"  (2026-10-30 baseline: reference 4.7x, pre-Phase-12 AI-generated 1.2x -- this run's spread should measurably exceed 1.2x.)");
        }

        var longestEffectTicks = effects.Count > 0 ? effects.Max(e => e.EndTicks - e.StartTicks) : 0;
        Console.WriteLine($"Longest single compiled effect: {TimeSpan.FromTicks(longestEffectTicks)} ({(longestEffectTicks / (double)ticksPerSecond):0.0}s) -- pre-Phase-12 baseline measured under 7s anywhere in the song.");
        Console.WriteLine("===============================================================");

        return 0;
    }

    /// <summary>Build-plan Phase 13 §5: the most EffectInstances active at any single instant for one prop -- a sweep-line over start/end events, since overlapping half-open [Start,End) windows is exactly what "concurrent EffectLayers" means in a real exported .xsq.</summary>
    private static int MaxConcurrentLayers(IReadOnlyList<EffectInstance> effectsForOneModel)
    {
        var events = effectsForOneModel
            .SelectMany(e => new[] { (Tick: e.StartTicks, Delta: 1), (Tick: e.EndTicks, Delta: -1) })
            .OrderBy(e => e.Tick)
            .ThenBy(e => e.Delta); // process every end-event at a tick before any start-event at that same tick -- a half-open [Start,End) window is not "active" at its own EndTicks.

        var current = 0;
        var max = 0;
        foreach (var (_, delta) in events)
        {
            current += delta;
            max = Math.Max(max, current);
        }

        return max;
    }
}
