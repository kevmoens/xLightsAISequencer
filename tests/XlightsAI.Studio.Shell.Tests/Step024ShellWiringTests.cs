using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 024: proves <see cref="ShellViewModel"/>'s <see cref="ShellViewModel.Audio"/>
/// screen and <see cref="ShellViewModel.ProbeAudioFileCommand"/> are wired end to end through a
/// real <see cref="WavAudioDecoder"/> and a real <see cref="JsonSongAssetStore"/> (not stand-ins):
/// probing a real WAV file persists a saved asset record to disk, and earlier steps' constructor
/// call sites (no <see cref="IAudioDecoder"/>/<see cref="ISongAssetStore"/> supplied) keep
/// compiling and behaving identically.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step024ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step024-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ProbeAudioFileCommand_OnSuccess_AutomaticallyBuildsWaveformCache()
    {
        // User feedback (2026-11-01): "when you load the song automatically rebuild waveform" -- probing
        // used to leave the waveform cache stale/absent until a separate manual "Build waveform" click.
        Directory.CreateDirectory(_root);
        var audioPath = Path.Combine(_root, "click-track.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 8, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000);
        await File.WriteAllBytesAsync(audioPath, Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000));

        var decoder = new WavAudioDecoder();
        var songAssetStore = new JsonSongAssetStore(Path.Combine(_root, "audio-assets"));
        var waveformPyramidStore = new XlightsAI.Studio.Audio.JsonWaveformPyramidStore(Path.Combine(_root, "waveform-cache"));
        var waveformBuilder = new XlightsAI.Studio.Audio.WaveformPyramidBuilder(decoder, waveformPyramidStore);

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: songAssetStore,
            waveformPyramidStore: waveformPyramidStore,
            waveformBuilder: waveformBuilder);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = audioPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        Assert.True(shell.Audio.HasProbedAsset);
        // No separate BuildWaveformCacheCommand click here -- probing alone must have built it.
        Assert.True(shell.Audio.HasWaveformCache);
        Assert.Null(shell.Audio.WaveformErrorMessage);
        Assert.True(File.Exists(shell.Audio.WaveformCachePath));
    }

    [Fact]
    public async Task ProbeAudioFileCommand_MissingMedia_NeverAttemptsWaveformBuild_NegativePath()
    {
        var waveformPyramidStore = new XlightsAI.Studio.Audio.JsonWaveformPyramidStore(Path.Combine(_root, "waveform-cache"));
        var waveformBuilder = new XlightsAI.Studio.Audio.WaveformPyramidBuilder(new WavAudioDecoder(), waveformPyramidStore);

        var shell = new ShellViewModel(
            new ShellNavigator(),
            () => Array.Empty<string>(),
            audioDecoder: new WavAudioDecoder(),
            songAssetStore: new JsonSongAssetStore(Path.Combine(_root, "audio-assets")),
            waveformPyramidStore: waveformPyramidStore,
            waveformBuilder: waveformBuilder);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = Path.Combine(_root, "does-not-exist.wav");
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        Assert.False(shell.Audio.HasProbedAsset);
        Assert.False(shell.Audio.HasWaveformCache);
        Assert.Null(shell.Audio.WaveformErrorMessage);
    }

    [Fact]
    public async Task Step024_Acceptance_Passes_ProbeAudioFilePersistsAssetRecord()
    {
        Directory.CreateDirectory(_root);
        var audioPath = Path.Combine(_root, "click-track.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 8, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000);
        await File.WriteAllBytesAsync(audioPath, Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000));

        var assetDirectory = Path.Combine(_root, "audio-assets");
        var decoder = new WavAudioDecoder();
        var store = new JsonSongAssetStore(assetDirectory);

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: store);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);
        Assert.True(shell.IsShowingAudio);

        shell.Audio.AudioFilePath = audioPath;
        Assert.True(shell.ProbeAudioFileCommand.CanExecute(null));
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        Assert.Null(shell.Audio.ErrorMessage);
        Assert.True(shell.Audio.HasProbedAsset);
        Assert.Equal(8000, shell.Audio.ProbedAsset!.SampleRate);
        Assert.Equal(samples.Length, shell.Audio.ProbedAsset.SampleCount);
        Assert.True(File.Exists(shell.Audio.SavedAssetPath));

        var reloaded = await store.LoadAsync(shell.Audio.SavedAssetPath!, CancellationToken.None);
        Assert.Equal(shell.Audio.ProbedAsset, reloaded);
    }

    [Fact]
    public async Task ProbeAudioFileCommand_MissingMedia_SetsErrorAndDoesNotPersistAnything_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var store = new JsonSongAssetStore(Path.Combine(_root, "audio-assets"));

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: new WavAudioDecoder(),
            songAssetStore: store);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = Path.Combine(_root, "does-not-exist.wav");
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.Audio.ErrorMessage));
        Assert.False(shell.Audio.HasProbedAsset);
        Assert.False(Directory.Exists(Path.Combine(_root, "audio-assets")));
    }

    [Fact]
    public async Task ProbeAudioFileCommand_CorruptFile_SetsErrorRatherThanThrowing_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var badPath = Path.Combine(_root, "not-audio.wav");
        await File.WriteAllTextAsync(badPath, "not a real wav file");
        var store = new JsonSongAssetStore(Path.Combine(_root, "audio-assets"));

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: new WavAudioDecoder(),
            songAssetStore: store);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = badPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.Audio.ErrorMessage));
        Assert.False(shell.Audio.HasProbedAsset);
    }

    [Fact]
    public void ShellViewModel_WithoutAudioCollaborators_StillConstructsAndStarts_BackwardCompatible()
    {
        // Earlier steps' constructor call sites must keep compiling and behaving identically: no
        // IAudioDecoder/ISongAssetStore supplied defaults to a real WavAudioDecoder + the throwing
        // NullSongAssetStore (same compatibility pattern as every other optional collaborator).
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup();

        Assert.Equal(StartupState.Ready, vm.State);
        vm.NavigateToAudioCommand.Execute(null);
        Assert.True(vm.IsShowingAudio);
        Assert.True(vm.ProbeAudioFileCommand.CanExecute(null));
    }

    [Fact]
    public async Task ProbeAudioFileCommand_WithNoStoreConfigured_ReportsErrorRatherThanThrowing_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var audioPath = Path.Combine(_root, "note.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], 0.2, 8000);
        await File.WriteAllBytesAsync(audioPath, Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000));

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());
        vm.RunStartup();
        vm.NavigateToAudioCommand.Execute(null);

        vm.Audio.AudioFilePath = audioPath;
        vm.ProbeAudioFileCommand.Execute(null);
        await vm.LastProbeAudioFileCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.Audio.ErrorMessage));
        Assert.False(vm.Audio.HasProbedAsset);
    }
}
