using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// <see cref="WholeSongGenerator"/> is the one facade wrapping M1-M4; this test proves it genuinely wires
/// a real probed <see cref="SongAsset"/> and a real <see cref="DisplayGraph"/> all the way through to a
/// real, exportable <see cref="SequenceDocument"/> — the same cheap fixture ONNX separator
/// <see cref="AudioAnalysisRunnerTests"/> uses (never the real, large, network-downloaded production
/// model), since this test's job is proving the facade's own sequencing, not re-proving any individual
/// stage's own algorithm (each already has its own dedicated tests).
/// </summary>
public sealed class WholeSongGeneratorTests : IDisposable
{
    private const string FixtureModelBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjrzAQorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwogCgppbnB1dF91bnNxCgVnYWlucxIGb3V0cHV0IgNNdWwSGFN5bnRoZXRpY1N0ZW1TZXBhcmF0b3JWMSojCAEIBAgBCAEQAUIFZ2FpbnNKEM3MzD0AAIA+AAAAPwAAQD9aLgoFaW5wdXQSJQojCAESHwoCCAEKChIIY2hhbm5lbHMKDRILbnVtX3NhbXBsZXNiMwoGb3V0cHV0EikKJwgBEiMKAggBCgIIBAoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private static readonly IReadOnlyList<StemKind> FixtureStemOrder =
        [StemKind.Drums, StemKind.Bass, StemKind.Vocals, StemKind.Other];

    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-whole-song-generator-{Guid.NewGuid():N}");

    public WholeSongGeneratorTests() => Directory.CreateDirectory(_rootDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_rootDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class NeverCalledDownloader : IModelAssetDownloader
    {
        public Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct) =>
            throw new InvalidOperationException("Downloader must not be invoked; the fixture model is always pre-provisioned.");
    }

    private static DisplayGraph BuildLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Arches"), "Arches", "ModelGroup", [], []),
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], []),
        ],
        Groups: [],
        Warnings: []);

    [Fact]
    public async Task GenerateAsync_RealProbedSongAndLayout_ProducesRealDocumentUsingAllProps()
    {
        const int sampleRate = 44100;
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([261.63, 329.63, 392.00, 440.0, 392.00, 329.63], noteDurationSeconds: 0.3, sampleRate);
        var wavPath = Path.Combine(Dir("audio"), "song.wav");
        await File.WriteAllBytesAsync(wavPath, DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate));
        var decoder = new WavAudioDecoder();
        var song = await decoder.ProbeAsync(wavPath, CancellationToken.None);

        var modelBytes = Convert.FromBase64String(FixtureModelBase64);
        var modelAsset = new ModelAsset(
            "synthetic-stem-separator", "1.0.0-fixture", new Uri("https://example.invalid/m.onnx"),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(modelBytes)).ToLowerInvariant(),
            "fixture-only, no third-party rights", "Microsoft.ML.OnnxRuntime (CPU)");
        var modelManager = new ModelAssetManager(Dir("models"), new NeverCalledDownloader());
        await File.WriteAllBytesAsync(modelManager.GetVerifiedPath(modelAsset), modelBytes);
        var recipe = new StemSeparationRecipe("fixture/1.0.0", sampleRate, 1, FixtureStemOrder, "input", "output");
        var separator = new OnnxStemSeparator(modelManager, decoder, recipe, Dir("stems"));

        var audioAnalysis = new AudioAnalysisRunner(
            decoder, separator, modelAsset,
            new JsonOnsetAnalysisStore(Dir("onsets")), new JsonTempoAnalysisStore(Dir("tempo")),
            new JsonBeatGridStore(Dir("beatgrid")), new JsonEnergyEnvelopeStore(Dir("energy")),
            new JsonPitchTrackStore(Dir("pitch")));
        var structureAnalysis = new StructureAnalysisRunner(
            new JsonBarFingerprintStore(Dir("fingerprints")), new JsonSectionBoundaryStore(Dir("boundaries")),
            new JsonSectionLabelStore(Dir("labels")), new JsonSectionLabelOverrideStore(Dir("label-overrides")),
            new JsonPhraseSegmentationStore(Dir("phrases")), new JsonPhraseBoundaryOverrideStore(Dir("phrase-overrides")),
            new JsonMotifClusteringStore(Dir("motifs")));

        var generator = new WholeSongGenerator(audioAnalysis, structureAnalysis);
        var layout = BuildLayout();

        var result = await generator.GenerateAsync(song, layout, CancellationToken.None);

        Assert.NotEmpty(result.Document.Effects);
        foreach (var model in layout.Models)
        {
            Assert.Contains(result.Document.Effects, e => e.Target == model.Id);
        }

        // The intermediate analysis the pipeline computes along the way is returned, not discarded —
        // see WholeSongGenerationResult's own doc comment for why (PianoRoll/StructureInspector).
        Assert.Equal(song.Id, result.SongId);
        Assert.NotNull(result.Melody);
        Assert.NotNull(result.Phrases);
        Assert.NotNull(result.Motifs);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new WholeSongGenerator(null!, null!));
    }
}
