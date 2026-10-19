using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 026: proves <see cref="ShellViewModel"/>'s "Build Waveform Cache" command is wired
/// end to end through a real <see cref="WaveformPyramidBuilder"/>/<see cref="JsonWaveformPyramidStore"/>
/// (no fakes) after probing a real WAV file.
/// </summary>
public sealed class Step026ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step026-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<ShellViewModel> CreateReadyShellWithProbedAssetAsync(string root, int clickCount = 6, int bpm = 120)
    {
        Directory.CreateDirectory(root);
        var audioPath = Path.Combine(root, "click-track.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000);
        await File.WriteAllBytesAsync(audioPath, Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000));

        var decoder = new WavAudioDecoder();
        var songAssetStore = new JsonSongAssetStore(Path.Combine(root, "audio-assets"));
        var waveformPyramidStore = new JsonWaveformPyramidStore(Path.Combine(root, "waveform-cache"));
        var waveformBuilder = new WaveformPyramidBuilder(decoder, waveformPyramidStore);

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

        return shell;
    }

    [Fact]
    public async Task Step026_Acceptance_Passes_BuildWaveformCacheReopensWithoutRecomputeThroughShellViewModel()
    {
        var shell = await CreateReadyShellWithProbedAssetAsync(_root);

        Assert.True(shell.BuildWaveformCacheCommand.CanExecute(null));
        shell.BuildWaveformCacheCommand.Execute(null);
        await shell.LastBuildWaveformCacheCommandTask!;

        Assert.True(shell.Audio.HasWaveformCache);
        Assert.Null(shell.Audio.WaveformErrorMessage);
        Assert.True(File.Exists(shell.Audio.WaveformCachePath));
        Assert.Equal(5, shell.Audio.WaveformResolutionLevelCount);
        Assert.Equal(shell.Audio.ProbedAsset!.Sha256, shell.Audio.WaveformContentHash);

        var firstPath = shell.Audio.WaveformCachePath;
        var firstWriteTimeUtc = File.GetLastWriteTimeUtc(firstPath!);

        // "Cache reopens without recompute" through the full Shell-wired vertical slice too, not
        // only the Audio project's own unit tests: rebuilding must not rewrite the file.
        shell.BuildWaveformCacheCommand.Execute(null);
        await shell.LastBuildWaveformCacheCommandTask!;

        Assert.Equal(firstPath, shell.Audio.WaveformCachePath);
        Assert.Equal(firstWriteTimeUtc, File.GetLastWriteTimeUtc(firstPath!));
    }

    [Fact]
    public void BuildWaveformCacheCommand_WithoutProbedAsset_IsDisabled()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        Assert.False(shell.BuildWaveformCacheCommand.CanExecute(null));
    }

    [Fact]
    public async Task BuildWaveformCacheCommand_NoWaveformCollaboratorsConfigured_SetsErrorRatherThanThrowing_NegativePath()
    {
        // ShellViewModel's default WaveformPyramidBuilder falls back to NullWaveformPyramidStore
        // when no real store directory is supplied anywhere (same "needs real configuration"
        // reasoning as the song-asset store) — the command must surface that as a clean error, not
        // an unhandled exception.
        var shell = await CreateReadyShellWithProbedAssetAsyncWithoutWaveformStore(_root);

        shell.BuildWaveformCacheCommand.Execute(null);
        await shell.LastBuildWaveformCacheCommandTask!;

        Assert.False(shell.Audio.HasWaveformCache);
        Assert.False(string.IsNullOrEmpty(shell.Audio.WaveformErrorMessage));
    }

    private static async Task<ShellViewModel> CreateReadyShellWithProbedAssetAsyncWithoutWaveformStore(string root)
    {
        Directory.CreateDirectory(root);
        var audioPath = Path.Combine(root, "no-waveform-store-click-track.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 4, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000);
        await File.WriteAllBytesAsync(audioPath, Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000));

        var decoder = new WavAudioDecoder();
        var songAssetStore = new JsonSongAssetStore(Path.Combine(root, "no-waveform-store-audio-assets"));

        var navigator = new ShellNavigator();
        // Deliberately no waveformPyramidStore/waveformBuilder argument: exercises ShellViewModel's
        // own default (NullWaveformPyramidStore-backed WaveformPyramidBuilder), not a test-supplied
        // real store.
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: songAssetStore);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = audioPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;
        Assert.True(shell.Audio.HasProbedAsset);

        return shell;
    }

    [Fact]
    public void ShellViewModel_WithoutWaveformCollaborators_StillConstructsAndStarts_BackwardCompatible()
    {
        // Earlier steps' constructor call sites (Step 004-025, no waveform arguments) must keep
        // compiling and behaving identically.
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup();

        Assert.Equal(StartupState.Ready, vm.State);
        vm.NavigateToAudioCommand.Execute(null);
        Assert.True(vm.IsShowingAudio);
        Assert.False(vm.BuildWaveformCacheCommand.CanExecute(null));
    }
}
