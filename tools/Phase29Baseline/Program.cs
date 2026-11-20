using System.Text.Json;
using System.Text.RegularExpressions;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

if (args.Length != 5 || args[0] is not ("analyze" or "record" or "replay" or "compare" or "compare-replay" or "compare-captured"))
{
    Console.Error.WriteLine("Usage: Phase29Baseline analyze|record|replay|compare|compare-replay|compare-captured <output directory> <show directory> <effect metadata directory> <audio path>");
    return 2;
}

var compare = args[0] is "compare" or "compare-replay" or "compare-captured";
var record = args[0] is "record" or "compare";
var output = Path.GetFullPath(args[1]);
var captures = Path.Combine(output, args[0] is "compare" or "compare-captured" ? "comparison-responses" : "responses");
var run = Path.Combine(output, args[0] switch
{
    "compare" => "comparison",
    "compare-replay" => "comparison-replay",
    "compare-captured" => "comparison-captured",
    "record" => "baseline",
    _ => "replay",
});
Directory.CreateDirectory(captures);
var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(args[2], CancellationToken.None);
var musicPath = Path.Combine(output, "music.json");
var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XlightsAI.Studio");
var options = new JsonSerializerOptions { WriteIndented = true };
var music = (record || args[0] == "analyze") && !File.Exists(musicPath)
    ? await AnalyzeAsync()
    : JsonSerializer.Deserialize<MusicModel>(await File.ReadAllTextAsync(musicPath))!;
Console.WriteLine($"Analysis complete: {music.Sections.Labels.Count} sections.");
if (args[0] == "analyze")
{
    return 0;
}

// The analyser can extrapolate past the end of a trimmed clip. Keep the saved analysis
// intact and feed only in-bounds beats to both runs, as with a corrected beat grid.
var inBounds = music.BeatGrid.Beats.Where(b => b.TimeTicks >= 0 && b.TimeTicks < music.SongDurationTicks).ToArray();
if (inBounds.Length < music.BeatGrid.Beats.Length)
{
    Console.WriteLine($"Removed {music.BeatGrid.Beats.Length - inBounds.Length} out-of-song beats from the generation input.");
    music = music with { BeatGrid = music.BeatGrid with { Beats = inBounds } };
}

using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
using var executor = new HttpJsonRequestExecutor(http);
var model = Environment.GetEnvironmentVariable("PHASE29_OLLAMA_MODEL") ?? "qwen2.5-coder:7b-instruct";
IChatCompletionClient Role(AiRole role) => new CapturedClient(
    captures, role, record ? new OpenAiCompatibleChatCompletionClient(
        new RoleProviderConfig(role, true, AiProviderType.OpenAiCompatible, "http://127.0.0.1:11434", model, null,
            RequestTimeoutSeconds: 600, MaxRetries: 0, MaxResponseBytes: 131072), executor, apiKey: null) : null);
var knowledgeDir = Path.Combine(appData, "corpus-knowledge");
var (rules, exemplars, patterns, coOccurrence, complexity) =
    await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(knowledgeDir, CancellationToken.None);
var catalogs = EffectCatalogFactory.BuildFromMetadataDirectory(args[3]);
var knowledge = new DirectorKnowledgeContext(
    catalogs.Vocabulary, rules, exemplars, MultiLayerTemplateCatalogLoader.Load(knowledgeDir),
    EffectVisualCharacterCatalogLoader.Load(), patterns, coOccurrence, complexity, DirectorLessonCatalogLoader.Load(knowledgeDir));
var biblePath = Path.Combine(output, "input-show-bible.json");
if (record && Environment.GetEnvironmentVariable("PHASE29_SHOW_BIBLE") is { Length: > 0 } sourceBible)
{
    File.Copy(sourceBible, biblePath, overwrite: false);
}
var bible = File.Exists(biblePath)
    ? JsonSerializer.Deserialize<ShowBible>(await File.ReadAllTextAsync(biblePath))
        ?? throw new InvalidDataException($"No Show Bible in '{biblePath}'.")
    : null;
var pipeline = new ChoreographyGenerationPipeline(
    knowledge, StylePresetCatalog.TraditionalClassicId, Role(AiRole.Director), model, Role(AiRole.Choreographer), model, 16000);
var settings = new ChoreographyGenerationSettings(layout, catalogs.Vocabulary, catalogs.Recipes,
    new EffectRecipeRegistry(catalogs.Recipes), DefaultEnergyCeilings.ArchitectureSection10, run);
var report = await pipeline.GenerateAsync(music, LayoutSummary.Build(layout), settings, Guid.NewGuid(), bible, CancellationToken.None);
await File.WriteAllTextAsync(Path.Combine(run, "timelines.json"), JsonSerializer.Serialize(report.Timelines, options));
Console.WriteLine($"Export: {report.Export.Path}");
BaselineMeasurement.Write(run, report.Export.Path, layout, music, compare ? report.CueDecisions : null);
return 0;

async Task<MusicModel> AnalyzeAsync()
{
    var cache = Path.Combine(appData, "analysis-cache");
    var decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), new LocalPathFfmpegInstaller(), Path.Combine(output, "wav"));
    var stemAssets = new ModelAssetManager(Path.Combine(appData, "tools", "stem-separation-model"), new InstalledModelsOnly());
    var stemSeparator = new SpectrogramStemSeparatorAdapter(
        new SpectrogramOnnxStemSeparator(stemAssets, new TarBzip2ModelArchiveExpander(), new WavStereoAudioDecoder(),
            Path.Combine(appData, "stem-separation-output")),
        SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe);
    var audioRunner = new AudioAnalysisRunner(
        decoder, stemSeparator, SherpaOnnxSpleeterModelAsset.Archive,
        new JsonOnsetAnalysisStore(Path.Combine(cache, "onsets")),
        new JsonTempoAnalysisStore(Path.Combine(cache, "tempo")),
        new JsonBeatGridStore(Path.Combine(cache, "beatgrid")),
        new JsonEnergyEnvelopeStore(Path.Combine(cache, "energy")),
        new JsonPitchTrackStore(Path.Combine(cache, "pitch")),
        new JsonStemActivityResultStore(Path.Combine(cache, "vocal-activity")),
        new JsonBandEnergyEnvelopeStore(Path.Combine(cache, "band-energy")));
    var structureRunner = new StructureAnalysisRunner(
        new JsonBarFingerprintStore(Path.Combine(cache, "fingerprints")),
        new JsonSectionBoundaryStore(Path.Combine(cache, "boundaries")),
        new JsonSectionLabelStore(Path.Combine(cache, "labels")),
        new JsonSectionLabelOverrideStore(Path.Combine(cache, "label-overrides")),
        new JsonPhraseSegmentationStore(Path.Combine(cache, "phrases")),
        new JsonPhraseBoundaryOverrideStore(Path.Combine(cache, "phrase-overrides")),
        new JsonMotifClusteringStore(Path.Combine(cache, "motifs")));
    var song = await decoder.ProbeAsync(args[4], CancellationToken.None);
    Console.WriteLine($"Analyzing {Path.GetFileName(args[4])}, duration {TimeSpan.FromTicks(song.DurationTicks)}.");
    var audio = await audioRunner.AnalyzeAsync(song, CancellationToken.None);
    var structure = await structureRunner.AnalyzeAsync(song.Id, song.Sha256, audio.BeatGrid, audio.Energy, audio.Melody, CancellationToken.None);
    var analyzed = new MusicModel(
        song.Id, song.DurationTicks, audio.BeatGrid, structure.Labels, structure.Phrases,
        audio.BandEnergy, audio.VocalActivity, audio.VisemeMarks, audio.Melody, structure.Motifs,
        TransientHits: audio.Onsets is { } onsets ? TransientHitDetector.Detect(onsets, audio.BandEnergy) : null,
        EnergyTransitions: audio.BandEnergy is { } energy ? EnergyTransitionDetector.Detect(energy) : null,
        Energy: audio.Energy,
        KeyChanges: KeyDetector.DetectKeyChanges(structure.FingerprintSet),
        LeadInstrumentWindows: audio.BandEnergy is { } lead ? LeadInstrumentWindowDetector.Detect(lead) : null);
    await File.WriteAllTextAsync(musicPath, JsonSerializer.Serialize(analyzed, options));
    return analyzed;
}

sealed class InstalledModelsOnly : IModelAssetDownloader
{
    public Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct) =>
        throw new InvalidOperationException("The baseline only uses already-installed stem models.");
}

sealed class CapturedClient(string directory, AiRole role, IChatCompletionClient? live) : IChatCompletionClient
{
    private sealed record Rejection(AiCallRejectionReason Reason, string Message);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _calls = new();

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var section = Regex.Match(request.UserPrompt, @"(?m)^SectionId: ([a-fA-F0-9-]+)").Groups[1].Value;
        var key = section.Length == 0 ? role.ToString() : $"{role}-{section}";
        var index = _calls.AddOrUpdate(key, 1, (_, attempts) => attempts + 1);
        var path = Path.Combine(directory, $"{key}-{index:D3}.json");
        var rejectionPath = Path.ChangeExtension(path, ".rejected.json");
        Console.WriteLine($"{role} call {index} ({(live is null ? "replay" : "record")}).");
        if (live is null)
        {
            if (File.Exists(rejectionPath))
            {
                var rejection = JsonSerializer.Deserialize<Rejection>(await File.ReadAllTextAsync(rejectionPath, ct))
                    ?? throw new InvalidDataException($"No model rejection in '{rejectionPath}'.");
                throw new AiCallRejectedException(rejection.Reason, rejection.Message);
            }

            if (!File.Exists(path))
            {
                throw new AiCallRejectedException(AiCallRejectionReason.ProviderUnavailable, "No captured reply for this retry.");
            }
            return JsonSerializer.Deserialize<ChatCompletionResult>(await File.ReadAllTextAsync(path, ct))
                ?? throw new InvalidDataException($"No model response in '{path}'.");
        }

        if (File.Exists(path) || File.Exists(rejectionPath))
        {
            throw new InvalidOperationException($"Refusing to overwrite recorded response '{path}'.");
        }

        try
        {
            var result = await live.CompleteAsync(request, ct);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(result with { Diagnostics = null }), ct);
            return result;
        }
        catch (AiCallRejectedException ex)
        {
            await File.WriteAllTextAsync(rejectionPath, JsonSerializer.Serialize(new Rejection(ex.Reason, ex.Message)), ct);
            throw;
        }
    }
}
