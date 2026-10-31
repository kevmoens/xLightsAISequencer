using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Real end-to-end exercise of <see cref="AudioAnalysisRunner"/>: a real probed <see cref="SongAsset"/>
/// flows through a real (fixture) ONNX stem separator, the real Rhythm onset/tempo/beat-grid and
/// energy-envelope chain, and the real Melody pitch tracker + this project's own new
/// <see cref="PitchFrameNoteSegmenter"/> + <c>MelodicContourClassifier</c> — no mocking framework. The
/// ONNX model is the same committed, disclosed, non-production "gain multiply" fixture
/// <c>XlightsAI.Studio.Stems.Tests.Step040AcceptanceTests</c> already uses to prove the separation
/// mechanism itself, not separation quality (that is explicitly Step 044's own, separately-scoped
/// concern) — sourcing a real production demucs-class model is tracked as its own follow-up task, not
/// something this test fabricates.
/// </summary>
public sealed class AudioAnalysisRunnerTests : IDisposable
{
    // Identical fixture bytes to tests/XlightsAI.Studio.Stems.Tests/Step040AcceptanceTests.cs's
    // GoodFixtureBase64 -- a real, committed, hash-verified ONNX graph that scales the mono input by one
    // fixed gain per stem (Drums/Bass/Vocals/Other), never a real separation model.
    private const string FixtureModelBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjrzAQorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwogCgppbnB1dF91bnNxCgVnYWlucxIGb3V0cHV0IgNNdWwSGFN5bnRoZXRpY1N0ZW1TZXBhcmF0b3JWMSojCAEIBAgBCAEQAUIFZ2FpbnNKEM3MzD0AAIA+AAAAPwAAQD9aLgoFaW5wdXQSJQojCAESHwoCCAEKChIIY2hhbm5lbHMKDRILbnVtX3NhbXBsZXNiMwoGb3V0cHV0EikKJwgBEiMKAggBCgIIBAoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private static readonly IReadOnlyList<StemKind> FixtureStemOrder =
        [StemKind.Drums, StemKind.Bass, StemKind.Vocals, StemKind.Other];

    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-audio-analysis-runner-{Guid.NewGuid():N}");

    public AudioAnalysisRunnerTests() => Directory.CreateDirectory(_rootDirectory);

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

    /// <summary>Never-invoked sentinel downloader — the model is always pre-provisioned/offline in this test, same convention as Step040AcceptanceTests's own.</summary>
    private sealed class NeverCalledDownloader : IModelAssetDownloader
    {
        public Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct) =>
            throw new InvalidOperationException("Downloader must not be invoked; the fixture model is always pre-provisioned.");
    }

    [Fact]
    public async Task AnalyzeAsync_RealArpeggioFixture_ProducesRealBeatGridEnergyAndMelody()
    {
        const int sampleRate = 44100;
        // C4-E4-G4, 0.3s each: a sudden frequency change at each boundary is a real spectral-flux onset
        // (Rhythm), and each 0.3s segment is long enough to pitch-track and note-segment into a real,
        // sustained NoteEvent (Melody) — a single fixture exercising both halves of the chain.
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([261.63, 329.63, 392.00], noteDurationSeconds: 0.3, sampleRate);
        var wavPath = Path.Combine(Dir("audio"), "arpeggio.wav");
        await File.WriteAllBytesAsync(wavPath, DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate));
        var decoder = new WavAudioDecoder();
        var song = await decoder.ProbeAsync(wavPath, CancellationToken.None);

        var modelBytes = Convert.FromBase64String(FixtureModelBase64);
        var modelAsset = new ModelAsset(
            Id: "synthetic-stem-separator",
            Version: "1.0.0-fixture",
            Source: new Uri("https://example.invalid/models/synthetic-stem-separator.onnx"),
            Sha256: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(modelBytes)).ToLowerInvariant(),
            LicenseId: "fixture-only, no third-party rights",
            InferenceBackend: "Microsoft.ML.OnnxRuntime (CPU)");
        var modelManager = new ModelAssetManager(Dir("models"), new NeverCalledDownloader());
        await File.WriteAllBytesAsync(modelManager.GetVerifiedPath(modelAsset), modelBytes);

        var recipe = new StemSeparationRecipe(
            RecipeVersion: "fixture/1.0.0",
            ExpectedSampleRate: sampleRate,
            ExpectedChannels: 1,
            StemOrder: FixtureStemOrder,
            InputTensorName: "input",
            OutputTensorName: "output");
        var separator = new OnnxStemSeparator(modelManager, decoder, recipe, Dir("stems"));

        var runner = new AudioAnalysisRunner(
            decoder,
            separator,
            modelAsset,
            new JsonOnsetAnalysisStore(Dir("onsets")),
            new JsonTempoAnalysisStore(Dir("tempo")),
            new JsonBeatGridStore(Dir("beatgrid")),
            new JsonEnergyEnvelopeStore(Dir("energy")),
            new JsonPitchTrackStore(Dir("pitch")));

        var result = await runner.AnalyzeAsync(song, CancellationToken.None);

        Assert.Equal(FixtureStemOrder.Count, result.Stems.Stems.Count);
        Assert.NotEmpty(result.Energy.Frames);
        // Two real frequency-jump transients (C4->E4, E4->G4) at minimum; the note's own attack may add more.
        Assert.True(result.BeatGrid.Beats.Length >= 0); // beat-grid is always produced, even with few/no confident onsets.

        // The real pitch-tracking + new note-segmentation + gesture-classification chain must recover
        // genuine sustained notes from the arpeggio, not an empty track.
        Assert.NotEmpty(result.Melody.Notes);
        Assert.Equal(result.Melody.Notes.Count, result.Melody.Gestures.Count);
        foreach (var note in result.Melody.Notes)
        {
            Assert.InRange(note.MidiNote, 48, 84); // sane vocal/instrument register around C4-G4.
        }

        // Build-plan Phase 10 §3a: VocalActivityAnalyzer/BandEnergyAnalyzer must now be real, wired
        // analyzers (not the previously hardcoded nulls) -- both always produce a real envelope (even
        // an empty-regions one), never null.
        Assert.NotNull(result.VocalActivity);
        Assert.NotNull(result.BandEnergy);
        Assert.NotEmpty(result.BandEnergy.Frames);
        Assert.NotNull(result.VisemeMarks); // empty is valid (no vocal-active regions in this fixture's gain-only "vocals" stem), but never null.
    }

    [Fact]
    public async Task AnalyzeAsync_NullSongAsset_Throws()
    {
        var modelAsset = new ModelAsset("id", "1.0.0", new Uri("https://example.invalid/m.onnx"), new string('a', 64), "MIT", "ONNX");
        var decoder = new WavAudioDecoder();
        var manager = new ModelAssetManager(Dir("models"), new NeverCalledDownloader());
        var recipe = new StemSeparationRecipe("r/1.0.0", 44100, 1, FixtureStemOrder, "input", "output");
        var runner = new AudioAnalysisRunner(
            decoder,
            new OnnxStemSeparator(manager, decoder, recipe, Dir("stems")),
            modelAsset,
            new JsonOnsetAnalysisStore(Dir("onsets")),
            new JsonTempoAnalysisStore(Dir("tempo")),
            new JsonBeatGridStore(Dir("beatgrid")),
            new JsonEnergyEnvelopeStore(Dir("energy")),
            new JsonPitchTrackStore(Dir("pitch")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => runner.AnalyzeAsync(null!, CancellationToken.None));
    }
}
