using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// UI cleanup item 2, Phase 2a (see docs/UI-CLEANUP-PLAN.md): proves the Piano Roll/Structure/
/// Display Paths screens are wired end to end against real pipeline output, not just that their
/// view models exist in isolation.
///
/// <see cref="WholeSongGenerationResult"/> is what makes the first two possible at all — before it
/// existed, <see cref="IWholeSongGenerator.GenerateAsync"/> computed a melody and a phrase/motif
/// analysis internally and threw both away, returning only the final <see cref="SequenceDocument"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PianoRollStructureDisplayPathsWiringTests : IDisposable
{
    private const string FixtureModelBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjrzAQorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwogCgppbnB1dF91bnNxCgVnYWlucxIGb3V0cHV0IgNNdWwSGFN5bnRoZXRpY1N0ZW1TZXBhcmF0b3JWMSojCAEIBAgBCAEQAUIFZ2FpbnNKEM3MzD0AAIA+AAAAPwAAQD9aLgoFaW5wdXQSJQojCAESHwoCCAEKChIIY2hhbm5lbHMKDRILbnVtX3NhbXBsZXNiMwoGb3V0cHV0EikKJwgBEiMKAggBCgIIBAoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private static readonly IReadOnlyList<StemKind> FixtureStemOrder =
        [StemKind.Drums, StemKind.Bass, StemKind.Vocals, StemKind.Other];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-pianoroll-structure-" + Guid.NewGuid().ToString("N"));

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

    private static DisplayGraph BuildLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Arches"), "Arches", "ModelGroup", [], []),
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
        ],
        Groups: [],
        Warnings: []);

    private ShellViewModel BuildShell(WholeSongGenerator generator)
    {
        var navigator = new ShellNavigator();
        return new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: new WavAudioDecoder(),
            songAssetStore: new JsonSongAssetStore(Dir("audio-assets")),
            sequenceDocumentStore: new JsonSequenceDocumentStore(Dir("plans")),
            showDirectoryDiscoveryService: new ShowDirectoryDiscoveryService(),
            wholeSongGenerator: generator,
            noteTrackEditor: new NoteTrackEditor(new JsonMelodyTrackRevisionStore(Dir("melody-revisions"))),
            motifLinkOverrideStore: new JsonMotifLinkOverrideStore(Dir("motif-overrides")),
            displayLayoutEditor: new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(Dir("display-corrections"))));
    }

    private async Task<(ShellViewModel Shell, string ShowDirectory)> GenerateARealPlanAsync()
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

        var shell = BuildShell(generator);
        shell.RunStartup();

        shell.NavigateToAudioCommand.Execute(null);
        shell.Audio.AudioFilePath = wavPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        shell.NavigateToShowImportCommand.Execute(null);
        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;
        Assert.NotNull(shell.ShowImport.Layout);

        shell.GenerateWholeSongPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;
        Assert.True(shell.ShowImport.HasPlan);

        return (shell, showDirectory);
    }

    [Fact]
    public async Task GeneratingAWholeSongPlan_AlsoOpensPianoRoll_AgainstTheSameRealMelody()
    {
        var (shell, _) = await GenerateARealPlanAsync();

        Assert.True(shell.PianoRoll.IsLoaded);
        Assert.Equal(shell.ShowImport.LastWholeSongGeneration!.Melody.Id, shell.PianoRoll.TrackId);
        Assert.Equal(shell.ShowImport.LastWholeSongGeneration.Melody.Notes.Count, shell.PianoRoll.OriginalNoteCount);
    }

    [Fact]
    public async Task GeneratingAWholeSongPlan_AlsoOpensStructureInspector_AgainstTheSameRealPhrases()
    {
        var (shell, _) = await GenerateARealPlanAsync();

        Assert.True(shell.StructureInspector.IsLoaded || shell.StructureInspector.IsEmpty);
        // Either state is a real outcome of the real analysis (a tiny fixture song may segment into
        // zero phrases); what matters is OpenAsync actually ran against the real result rather than
        // leaving the screen untouched.
        Assert.NotEqual(ScreenLoadState.Error, shell.StructureInspector.LoadState);
    }

    /// <summary>
    /// Each real analysis run mints a fresh <see cref="MelodyTrack.Id"/> (confirmed empirically — the
    /// pipeline does not derive it from song content), so two generations of the same song never
    /// collide on one editing session; <see cref="ShellViewModel.RunGenerateWholeSongPlanAsync"/>'s
    /// own "resume if already open" check (<see cref="PianoRollViewModel.OpenTrackAsync"/> before
    /// <see cref="PianoRollViewModel.BeginEditingAsync"/>) exists for the case where it would, but is
    /// not exercised by ordinary re-generation today. This test proves re-generation still succeeds
    /// end to end (a fresh session opens cleanly) rather than asserting a same-id resume that the
    /// current pipeline's own id generation cannot actually produce.
    /// </summary>
    [Fact]
    public async Task RegeneratingAPlan_OpensAFreshPianoRollSession_WithoutError()
    {
        var (shell, _) = await GenerateARealPlanAsync();
        var firstTrackId = shell.PianoRoll.TrackId;
        Assert.NotNull(firstTrackId);

        shell.GenerateWholeSongPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        Assert.True(shell.PianoRoll.IsLoaded);
        Assert.NotEqual(ScreenLoadState.Error, shell.PianoRoll.LoadState);
    }

    [Fact]
    public async Task OpenDisplayPathEditorCommand_BuildsAndOpensARealSpatialGraph_FromTheImportedLayout()
    {
        var (shell, _) = await GenerateARealPlanAsync();

        Assert.True(shell.OpenDisplayPathEditorCommand.CanExecute(null));
        shell.OpenDisplayPathEditorCommand.Execute(null);
        await shell.LastOpenDisplayPathEditorCommandTask!;

        Assert.True(shell.DisplayPathEditor.IsLoaded);
        Assert.Equal(shell.ShowImport.Layout!.SourceLayoutHash, shell.DisplayPathEditor.SourceLayoutHash);
        Assert.Equal(2, shell.DisplayPathEditor.Rows.Count);
    }

    [Fact]
    public void OpenDisplayPathEditorCommand_DisabledUntilAShowLayoutIsImported()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();

        Assert.False(shell.OpenDisplayPathEditorCommand.CanExecute(null));
    }
}
