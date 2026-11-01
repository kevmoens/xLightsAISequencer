using System.Runtime.Versioning;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 028 ("Use one master media time clock for playback and playhead"): proves the full
/// real vertical slice — opening/playing/pausing/seeking on the Audio screen pushes the exact same
/// <see cref="MasterPlaybackClock"/> reading into <see cref="TimelineViewModel.PlayheadTicks"/>, and
/// the Timeline screen's own "Scrub Playhead" action seeks that one real transport too — against a
/// real <see cref="WaveOutAudioTransport"/> (the real Windows winmm device on this machine), same
/// "no mock IAudioTransport anywhere" convention as every prior Step 025/026/027 wiring test.
/// </summary>
[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class Step028ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step028-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<ShellViewModel> CreateReadyShellWithOpenPlaybackAndTimelineAsync(string root, WaveOutAudioTransport transport, int clickCount = 6, int bpm = 240)
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

        shell.OpenAudioPlaybackCommand.Execute(null);
        await shell.LastOpenAudioPlaybackCommandTask!;
        Assert.True(shell.Audio.IsPlaybackOpen);

        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = shell.Audio.ProbedAsset!.DurationTicks + 1_000_000;
        shell.LoadTimelineCommand.Execute(null);
        Assert.True(shell.Timeline.IsLoaded);

        return shell;
    }

    [RequiresAudioDeviceFact]
    public async Task Step028_Acceptance_Passes_AudioScreenActionsPushTheSamePlayheadIntoTimeline()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithOpenPlaybackAndTimelineAsync(_root, transport);

        shell.Audio.SeekTargetTicksText = (shell.Audio.ProbedAsset!.DurationTicks / 2).ToString();
        shell.SeekAudioCommand.Execute(null);
        await shell.LastSeekAudioCommandTask!;

        // One authoritative playhead: the Timeline's own PlayheadTicks must reflect exactly the same
        // seek the Audio screen just performed, never a separate/stale value.
        Assert.True(shell.Timeline.HasPlayhead);
        Assert.Equal(shell.Audio.PlaybackPositionTicks, shell.Timeline.PlayheadTicks);
    }

    [RequiresAudioDeviceFact]
    public async Task Step028_Acceptance_Passes_ScrubPlayheadSeeksTheSharedTransport_RepeatedScrubsDoNotDrift()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithOpenPlaybackAndTimelineAsync(_root, transport);

        var target = shell.Audio.ProbedAsset!.DurationTicks / 3;
        long? firstObserved = null;
        for (var i = 0; i < 5; i++)
        {
            shell.Timeline.ScrubTargetTicksText = target.ToString();
            Assert.True(shell.ScrubTimelinePlayheadCommand.CanExecute(null));
            shell.ScrubTimelinePlayheadCommand.Execute(null);
            await shell.LastScrubTimelinePlayheadCommandTask!;

            Assert.Null(shell.Timeline.ScrubErrorMessage);
            firstObserved ??= shell.Timeline.PlayheadTicks;
            Assert.Equal(firstObserved.Value, shell.Timeline.PlayheadTicks);
            // The real transport the Audio screen itself would display is the exact same value.
            Assert.Equal(shell.Timeline.PlayheadTicks, transport.PositionTicks);
        }
    }

    [RequiresAudioDeviceFact]
    public async Task ScrubPlayheadCommand_OutOfScopeTarget_SetsErrorWithoutMovingPlayhead_NegativePath()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithOpenPlaybackAndTimelineAsync(_root, transport);

        var validTarget = shell.Audio.ProbedAsset!.DurationTicks / 2;
        shell.Timeline.ScrubTargetTicksText = validTarget.ToString();
        shell.ScrubTimelinePlayheadCommand.Execute(null);
        await shell.LastScrubTimelinePlayheadCommandTask!;
        var playheadBefore = shell.Timeline.PlayheadTicks;
        Assert.Null(shell.Timeline.ScrubErrorMessage);

        // Beyond the loaded timeline's own content bounds (TotalDurationTicks + 1,000,000).
        shell.Timeline.ScrubTargetTicksText = (shell.Timeline.TotalDurationTicks + 10_000_000).ToString();
        shell.ScrubTimelinePlayheadCommand.Execute(null);
        await shell.LastScrubTimelinePlayheadCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.Timeline.ScrubErrorMessage));
        Assert.Equal(playheadBefore, shell.Timeline.PlayheadTicks);
        Assert.Equal(playheadBefore, transport.PositionTicks);
    }

    [RequiresAudioDeviceFact]
    public async Task ScrubPlayheadCommand_UnparsableText_SetsErrorWithoutMovingPlayhead_NegativePath()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithOpenPlaybackAndTimelineAsync(_root, transport);

        var playheadBefore = shell.Timeline.PlayheadTicks;
        shell.Timeline.ScrubTargetTicksText = "not-a-number";
        shell.ScrubTimelinePlayheadCommand.Execute(null);
        await shell.LastScrubTimelinePlayheadCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.Timeline.ScrubErrorMessage));
        Assert.Equal(playheadBefore, shell.Timeline.PlayheadTicks);
    }

    [Fact]
    public void ScrubPlayheadCommand_NoPlaybackSessionOpen_SetsStaleErrorWithoutMovingPlayhead_NegativePath()
    {
        // No OpenAudioPlaybackCommand ever executed: the master clock has no song open at all — the
        // "stale/missing playback session" case — even though a timeline is loaded.
        Directory.CreateDirectory(_root);
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = 50_000_000;
        shell.LoadTimelineCommand.Execute(null);
        Assert.True(shell.Timeline.IsLoaded);

        shell.Timeline.ScrubTargetTicksText = "0";
        shell.ScrubTimelinePlayheadCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(shell.Timeline.ScrubErrorMessage));
        Assert.Equal(0, shell.Timeline.PlayheadTicks);
        Assert.False(shell.Timeline.HasPlayhead);
    }

    [Fact]
    public void ScrubTimelinePlayheadCommand_BeforeTimelineLoaded_IsDisabled()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToTimelineCommand.Execute(null);

        Assert.False(shell.Timeline.IsLoaded);
        Assert.False(shell.ScrubTimelinePlayheadCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadTimeline_WithoutAnyPlaybackOpened_PlayheadIsInactiveAtZero()
    {
        Directory.CreateDirectory(_root);
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = 50_000_000;
        shell.LoadTimelineCommand.Execute(null);

        Assert.True(shell.Timeline.IsLoaded);
        Assert.False(shell.Timeline.HasPlayhead);
        Assert.Equal(0, shell.Timeline.PlayheadTicks);
        Assert.False(shell.Timeline.IsPlayheadVisible);
        await Task.CompletedTask;
    }
}
