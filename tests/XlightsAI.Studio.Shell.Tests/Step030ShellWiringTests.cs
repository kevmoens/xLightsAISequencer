using System.Runtime.Versioning;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 030 ("Add frame/beat/off-grid snapping and timing-diagnostics panel"): proves the
/// real vertical slice — <see cref="ShellViewModel.SetTimelineSnapFrameCommand"/> arms
/// <see cref="TimelineViewModel.SnapPolicy"/>, <see cref="ShellViewModel.ScrubTimelinePlayheadCommand"/>
/// applies it to the real scrub target *before* seeking the real device, and the real device's own
/// resulting position is recorded as sync/drift telemetry — against a real
/// <see cref="WaveOutAudioTransport"/> (the real Windows winmm device on this machine), same "no mock
/// IAudioTransport anywhere" convention as every prior Step 025-029 wiring test. Required acceptance:
/// "Time-to-frame conversion and boundary rounding golden tests" (the pure half, in
/// <c>XlightsAI.Studio.Editing.Tests.Step030AcceptanceTests</c>) plus this step's own real-device scrub
/// slice.
/// </summary>
[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class Step030ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step030-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<ShellViewModel> CreateReadyShellWithOpenPlaybackAndTimelineAsync(string root, WaveOutAudioTransport transport, int clickCount = 8, int bpm = 240)
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
        shell.Timeline.TotalDurationTicks = shell.Audio.ProbedAsset!.DurationTicks;
        shell.LoadTimelineCommand.Execute(null);
        Assert.True(shell.Timeline.IsLoaded);

        return shell;
    }

    [RequiresAudioDeviceFact]
    public async Task Step030_Acceptance_Passes_FrameSnapAppliesToScrubAndRecordsRealDriftTelemetry()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithOpenPlaybackAndTimelineAsync(_root, transport);

        Assert.True(shell.SetTimelineSnapFrameCommand.CanExecute(null));
        shell.SetTimelineSnapFrameCommand.Execute(null);
        Assert.Equal(SnapPolicy.Frame, shell.Timeline.SnapPolicy);

        // 1,260,000 ticks is deliberately off the 250,000-tick xLights frame grid; the nearest frame
        // boundary is 1,250,000 (1,260,000 / 250,000 == 5.04, rounds to frame 5).
        shell.Timeline.ScrubTargetTicksText = "1260000";
        shell.ScrubTimelinePlayheadCommand.Execute(null);
        await shell.LastScrubTimelinePlayheadCommandTask!;

        Assert.Null(shell.Timeline.ScrubErrorMessage);
        // The real transport was seeked to the *snapped* tick, never the raw requested one — proven by
        // reading the real device's own position back, not a mocked value.
        Assert.Equal(1_250_000, shell.Timeline.LastScrubRequestedTicks);
        Assert.NotNull(shell.Timeline.LastScrubActualTicks);
        Assert.NotNull(shell.Timeline.LastScrubDriftTicks);
        // Real sample-rate quantization (8,000 Hz) can shift the actual position by at most ~1 sample's
        // worth of ticks; this is a real device reading, so assert a small, real tolerance rather than
        // an exact match.
        Assert.True(Math.Abs(shell.Timeline.LastScrubDriftTicks!.Value) <= 2_000,
            $"Drift {shell.Timeline.LastScrubDriftTicks} exceeded the real device's own sample-rate quantization tolerance.");
        Assert.Equal(5, shell.Timeline.PlayheadFrameIndex);
    }

    [RequiresAudioDeviceFact]
    public async Task ScrubCommand_FrameSnapMovesTargetOutOfScope_RejectsWithoutPartialMutation_NegativePath()
    {
        await using var transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var shell = await CreateReadyShellWithOpenPlaybackAndTimelineAsync(_root, transport);
        shell.SetTimelineSnapFrameCommand.Execute(null);

        var totalDuration = shell.Timeline.TotalDurationTicks;
        // Beyond the loaded timeline's own bounds by two full frames — far enough that even the
        // largest possible frame-snap adjustment (at most half a frame) cannot round it back into
        // range. The snap policy is applied first (per ShellViewModel's own ordering), then the
        // already-snapped value is validated and rejected; never a partial mutation either way.
        shell.Timeline.ScrubTargetTicksText = (totalDuration + (2 * XLightsFrameGrid.FrameIntervalTicks)).ToString();

        var requestedBefore = shell.Timeline.LastScrubRequestedTicks;
        shell.ScrubTimelinePlayheadCommand.Execute(null);
        await shell.LastScrubTimelinePlayheadCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.Timeline.ScrubErrorMessage));
        Assert.Equal(requestedBefore, shell.Timeline.LastScrubRequestedTicks); // Telemetry untouched by a rejected scrub.
    }

    [Fact]
    public void SetTimelineSnapFrameCommand_NoTimelineLoaded_IsDisabled_StaleNegativePath()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToTimelineCommand.Execute(null);

        Assert.False(shell.SetTimelineSnapFrameCommand.CanExecute(null));
        Assert.False(shell.SetTimelineSnapNoneCommand.CanExecute(null));
    }

    [Fact]
    public void SetTimelineSnapNoneCommand_TimelineLoaded_SetsPolicyBackToNone()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = 50_000_000;
        shell.LoadTimelineCommand.Execute(null);

        shell.SetTimelineSnapFrameCommand.Execute(null);
        Assert.Equal(SnapPolicy.Frame, shell.Timeline.SnapPolicy);

        Assert.True(shell.SetTimelineSnapNoneCommand.CanExecute(null));
        shell.SetTimelineSnapNoneCommand.Execute(null);
        Assert.Equal(SnapPolicy.None, shell.Timeline.SnapPolicy);
    }
}
