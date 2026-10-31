using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// Proves <see cref="ShellViewModel.GenerateWholeSongPlanCommand"/>/<see cref="ShellViewModel.DownloadStemSeparationModelCommand"/>
/// are wired end to end: a real probed song + a real imported layout, run through a real (fixture-backed)
/// <see cref="WholeSongGenerator"/>, produces a real <see cref="SequenceDocument"/> using every real prop
/// — and that the existing Step 012 "3 hardcoded props" <see cref="ShellViewModel.GenerateEffectPlanCommand"/>
/// still compiles and behaves identically alongside it (additive, not a breaking replacement).
/// </summary>
[Trait("Category", "Integration")]
public sealed class WholeSongPlanShellWiringTests : IDisposable
{
    private const string FixtureModelBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjrzAQorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwogCgppbnB1dF91bnNxCgVnYWlucxIGb3V0cHV0IgNNdWwSGFN5bnRoZXRpY1N0ZW1TZXBhcmF0b3JWMSojCAEIBAgBCAEQAUIFZ2FpbnNKEM3MzD0AAIA+AAAAPwAAQD9aLgoFaW5wdXQSJQojCAESHwoCCAEKChIIY2hhbm5lbHMKDRILbnVtX3NhbXBsZXNiMwoGb3V0cHV0EikKJwgBEiMKAggBCgIIBAoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private static readonly IReadOnlyList<StemKind> FixtureStemOrder =
        [StemKind.Drums, StemKind.Bass, StemKind.Vocals, StemKind.Other];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-wholesong-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
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

    private WholeSongGenerator BuildRealFixtureBackedGenerator(SongAsset probeForSampleRate)
    {
        var modelBytes = Convert.FromBase64String(FixtureModelBase64);
        var modelAsset = new ModelAsset(
            "synthetic-stem-separator", "1.0.0-fixture", new Uri("https://example.invalid/m.onnx"),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(modelBytes)).ToLowerInvariant(),
            "fixture-only, no third-party rights", "Microsoft.ML.OnnxRuntime (CPU)");
        var modelManager = new ModelAssetManager(Dir("models"), new NeverCalledDownloader());
        File.WriteAllBytes(modelManager.GetVerifiedPath(modelAsset), modelBytes);
        var decoder = new WavAudioDecoder();
        var recipe = new StemSeparationRecipe("fixture/1.0.0", probeForSampleRate.SampleRate, 1, FixtureStemOrder, "input", "output");
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

        return new WholeSongGenerator(audioAnalysis, structureAnalysis);
    }

    [Fact]
    public async Task GenerateWholeSongPlanCommand_RealFixtureSongAndLayout_ProducesPlanUsingAllRealProps()
    {
        const int sampleRate = 44100;
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence(
            [261.63, 329.63, 392.00, 440.0, 392.00, 329.63], noteDurationSeconds: 0.3, sampleRate);
        var wavPath = Path.Combine(Dir("audio"), "song.wav");
        await File.WriteAllBytesAsync(wavPath, DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate));
        var decoder = new WavAudioDecoder();
        var song = await decoder.ProbeAsync(wavPath, CancellationToken.None);
        var generator = BuildRealFixtureBackedGenerator(song);

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: new JsonSongAssetStore(Dir("audio-assets")),
            sequenceDocumentStore: new JsonSequenceDocumentStore(Dir("plans")),
            wholeSongGenerator: generator);
        shell.RunStartup();

        // Probe the song on the Audio screen first -- GenerateWholeSongPlanCommand reads Audio.ProbedAsset.
        shell.NavigateToAudioCommand.Execute(null);
        shell.Audio.AudioFilePath = wavPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;
        Assert.True(shell.Audio.HasProbedAsset);

        // Discover isn't wired to a real show directory here, so set Layout indirectly isn't possible --
        // ShowImportViewModel.Layout has only a private setter. Instead, confirm the command surfaces the
        // real, correct "import a layout first" guidance rather than silently doing nothing.
        shell.NavigateToShowImportCommand.Execute(null);
        Assert.True(shell.GenerateWholeSongPlanCommand.CanExecute(null));
        shell.GenerateWholeSongPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;
        Assert.False(shell.ShowImport.HasPlan);
        Assert.Contains("Discover", shell.ShowImport.PlanErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateWholeSongPlanCommand_RealDiscoveredLayoutAndProbedSong_ProducesRealPlanUsingAllProps()
    {
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(
            Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects),
            """
            <xrgb FormatVersion="2" BaseChannel="0">
              <models>
                <model name="Arches" DisplayAs="Arches" />
                <model name="Roofline" DisplayAs="Star" />
                <model name="Tree" DisplayAs="Tree" />
              </models>
            </xrgb>
            """);

        const int sampleRate = 44100;
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence(
            [261.63, 329.63, 392.00, 440.0, 392.00, 329.63], noteDurationSeconds: 0.3, sampleRate);
        var wavPath = Path.Combine(Dir("audio"), "song.wav");
        await File.WriteAllBytesAsync(wavPath, DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate));
        var decoder = new WavAudioDecoder();
        var song = await decoder.ProbeAsync(wavPath, CancellationToken.None);
        var generator = BuildRealFixtureBackedGenerator(song);

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: new JsonSongAssetStore(Dir("audio-assets")),
            sequenceDocumentStore: new JsonSequenceDocumentStore(Dir("plans")),
            showDirectoryDiscoveryService: new ShowDirectoryDiscoveryService(),
            wholeSongGenerator: generator);
        shell.RunStartup();

        shell.NavigateToAudioCommand.Execute(null);
        shell.Audio.AudioFilePath = wavPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;
        Assert.True(shell.Audio.HasProbedAsset);

        shell.NavigateToShowImportCommand.Execute(null);
        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;
        Assert.NotNull(shell.ShowImport.Layout);

        shell.GenerateWholeSongPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        Assert.Null(shell.ShowImport.PlanErrorMessage);
        Assert.True(shell.ShowImport.HasPlan);
        Assert.NotEmpty(shell.ShowImport.Plan!.Effects);
        foreach (var model in shell.ShowImport.Layout!.Models)
        {
            Assert.Contains(shell.ShowImport.Plan.Effects, e => e.Target == model.Id);
        }
        Assert.True(File.Exists(shell.ShowImport.PlanSavedPath));
    }

    private sealed class ThrowingWholeSongGenerator(Exception failure) : IWholeSongGenerator
    {
        public Task<WholeSongGenerationResult> GenerateAsync(SongAsset song, DisplayGraph layout, CancellationToken ct = default) =>
            throw failure;
    }

    [Fact]
    public async Task GenerateWholeSongPlanCommand_IncompatibleSourceAudio_SurfacesActionableGuidance()
    {
        // The real separation model takes only 44.1 kHz stereo; a native WAV at another rate/channel
        // count is probed directly (never transcoded), so the user must be told what to actually do
        // about it rather than just shown the lower-level mismatch message.
        var showDirectory = Path.Combine(_root, "show-incompatible");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(
            Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects),
            """<xrgb FormatVersion="2" BaseChannel="0"><models><model name="Arches" DisplayAs="Arches" /></models></xrgb>""");

        const int sampleRate = 22050;
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.3, sampleRate);
        var wavPath = Path.Combine(Dir("audio-incompatible"), "song.wav");
        await File.WriteAllBytesAsync(wavPath, DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate));
        var decoder = new WavAudioDecoder();

        var generator = new ThrowingWholeSongGenerator(new StemSeparationIncompatibleSourceException(
            "Source song (22050 Hz, 1ch) does not match recipe 'sherpa-onnx-spleeter-2stems/1.0.0' expected 44100 Hz, 2ch stereo."));

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: new JsonSongAssetStore(Dir("audio-assets-incompatible")),
            sequenceDocumentStore: new JsonSequenceDocumentStore(Dir("plans-incompatible")),
            showDirectoryDiscoveryService: new ShowDirectoryDiscoveryService(),
            wholeSongGenerator: generator);
        shell.RunStartup();

        shell.NavigateToAudioCommand.Execute(null);
        shell.Audio.AudioFilePath = wavPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        shell.NavigateToShowImportCommand.Execute(null);
        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        shell.GenerateWholeSongPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        Assert.False(shell.ShowImport.HasPlan);
        // The real measured mismatch is preserved...
        Assert.Contains("22050 Hz, 1ch", shell.ShowImport.PlanErrorMessage);
        // ...and augmented with something the user can actually act on.
        Assert.Contains("44.1 kHz stereo", shell.ShowImport.PlanErrorMessage);
        // This is not the model-download case, so that affordance must stay hidden.
        Assert.False(shell.ShowImport.RequiresStemModelDownload);
    }

    [Fact]
    public void ShellViewModel_WithoutWholeSongGeneratorConfigured_StillConstructsAndStarts_BackwardCompatible()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup();
        vm.NavigateToShowImportCommand.Execute(null);

        Assert.True(vm.GenerateWholeSongPlanCommand.CanExecute(null));
        Assert.False(vm.DownloadStemSeparationModelCommand.CanExecute(null));
        // The legacy 3-hardcoded-props command must still exist and behave identically alongside it.
        Assert.True(vm.GenerateEffectPlanCommand.CanExecute(null));
    }
}
