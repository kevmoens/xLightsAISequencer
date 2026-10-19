using System.Runtime.Versioning;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 025: proves <see cref="ShellViewModel"/>'s Open/Play/Pause/Seek audio commands are
/// wired end to end through a real <see cref="WaveOutAudioTransport"/> (the real Windows winmm
/// device on this machine, not a stand-in): after probing a real WAV file, opening/playing/pausing/
/// seeking actually drives the real device and real, device-reported position.
/// </summary>
[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class Step025ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step025-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<ShellViewModel> CreateReadyShellWithProbedAssetAsync(string root, WaveOutAudioTransport transport, int clickCount = 6, int bpm = 240)
    {
        Directory.CreateDirectory(root);
        var audioPath = Path.Combine(root, "click-track.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000);
        await File.WriteAllBytesAsync(audioPath, Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000));

        var decoder = new WavAudioDecoder();
        var store = new JsonSongAssetStore(Path.Combine(root, "audio-assets"));

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: store,
            audioTransport: transport);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = audioPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;
        Assert.True(shell.Audio.HasProbedAsset);

        return shell;
    }

    [Fact]
    public async Task Step025_Acceptance_Passes_OpenPlayPauseSeekDriveTheRealDeviceThroughShellViewModel()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithProbedAssetAsync(_root, transport);

        Assert.True(shell.OpenAudioPlaybackCommand.CanExecute(null));
        shell.OpenAudioPlaybackCommand.Execute(null);
        await shell.LastOpenAudioPlaybackCommandTask!;
        Assert.True(shell.Audio.IsPlaybackOpen);
        Assert.Null(shell.Audio.PlaybackErrorMessage);

        Assert.True(shell.PlayAudioCommand.CanExecute(null));
        shell.PlayAudioCommand.Execute(null);
        await shell.LastPlayAudioCommandTask!;
        Assert.True(shell.Audio.IsPlaying);

        var advanced = await WaitUntilAsync(() => { shell.RefreshAudioPositionCommand.Execute(null); return shell.Audio.PlaybackPositionTicks > 0; }, TimeSpan.FromSeconds(2));
        Assert.True(advanced, "Shell-wired real transport position never advanced.");

        Assert.True(shell.PauseAudioCommand.CanExecute(null));
        shell.PauseAudioCommand.Execute(null);
        await shell.LastPauseAudioCommandTask!;
        Assert.False(shell.Audio.IsPlaying);

        shell.Audio.SeekTargetTicksText = "0";
        Assert.True(shell.SeekAudioCommand.CanExecute(null));
        shell.SeekAudioCommand.Execute(null);
        await shell.LastSeekAudioCommandTask!;
        Assert.Equal(0, shell.Audio.PlaybackPositionTicks);
        Assert.Null(shell.Audio.PlaybackErrorMessage);
    }

    [Fact]
    public async Task OpenAudioPlaybackCommand_WithoutProbedAsset_IsDisabled()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>(), audioTransport: transport);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        Assert.False(shell.OpenAudioPlaybackCommand.CanExecute(null));
    }

    [Fact]
    public async Task PlayPauseSeekCommands_BeforeOpen_AreDisabled()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithProbedAssetAsync(_root, transport);

        Assert.False(shell.PlayAudioCommand.CanExecute(null));
        Assert.False(shell.PauseAudioCommand.CanExecute(null));
        Assert.False(shell.SeekAudioCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenAudioPlaybackCommand_InvalidDevice_SetsErrorRatherThanThrowing_NegativePath()
    {
        // "missing output device handled" — proven at the Shell-wiring layer too, not only the
        // Audio project's own unit tests.
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder(), deviceId: 99_999);
        var shell = await CreateReadyShellWithProbedAssetAsync(_root, transport);

        shell.OpenAudioPlaybackCommand.Execute(null);
        await shell.LastOpenAudioPlaybackCommandTask!;

        Assert.False(shell.Audio.IsPlaybackOpen);
        Assert.False(string.IsNullOrEmpty(shell.Audio.PlaybackErrorMessage));
    }

    [Fact]
    public async Task ShellViewModel_WithoutAudioTransportCollaborator_DefaultsToRealWorkingTransportOnWindows()
    {
        // docs/ARCHITECTURE-AND-GUARDRAILS.md "Platform coverage": on Windows (this machine), the
        // default collaborator must be the real WaveOutAudioTransport, not the Mac/Linux
        // UnsupportedPlatformAudioTransport stub. Proven behaviorally: opening real playback (no
        // audioTransport argument supplied) must actually succeed against the real device here,
        // never throw UnsupportedAudioPlatformException.
        Directory.CreateDirectory(_root);
        var shell = await CreateReadyShellWithProbedAssetAsyncWithDefaultTransport(_root);

        shell.OpenAudioPlaybackCommand.Execute(null);
        await shell.LastOpenAudioPlaybackCommandTask!;

        Assert.True(shell.Audio.IsPlaybackOpen);
        Assert.Null(shell.Audio.PlaybackErrorMessage);
    }

    private static async Task<ShellViewModel> CreateReadyShellWithProbedAssetAsyncWithDefaultTransport(string root)
    {
        var audioPath = Path.Combine(root, "default-transport-click-track.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 240, clickCount: 4, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000);
        await File.WriteAllBytesAsync(audioPath, Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000));

        var decoder = new WavAudioDecoder();
        var store = new JsonSongAssetStore(Path.Combine(root, "default-transport-audio-assets"));

        var navigator = new ShellNavigator();
        // Deliberately no audioTransport argument: exercises ShellViewModel's own default-selection
        // logic (CreateDefaultAudioTransport), not a test-supplied instance.
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: store);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = audioPath;
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;
        Assert.True(shell.Audio.HasProbedAsset);

        return shell;
    }

    [Fact]
    public void ShellViewModel_WithoutAudioTransportCollaborator_StillConstructsAndStarts_BackwardCompatible()
    {
        // Earlier steps' constructor call sites (Step 004-024, no audioTransport argument) must keep
        // compiling and behaving identically: omitting it defaults to a real WaveOutAudioTransport
        // (same compatibility pattern as every other optional collaborator).
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup();

        Assert.Equal(StartupState.Ready, vm.State);
        vm.NavigateToAudioCommand.Execute(null);
        Assert.True(vm.IsShowingAudio);
        Assert.False(vm.OpenAudioPlaybackCommand.CanExecute(null));
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return condition();
    }
}
