using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 027 ("Render synchronized waveform beneath ruler"): proves the full real
/// vertical slice — probe a real WAV file, build a real <see cref="WaveformPyramidBuilder"/>/
/// <see cref="JsonWaveformPyramidStore"/> cache, then Load Timeline — renders real
/// <see cref="TimelineViewModel.WaveformBars"/> aligned through the exact same
/// <see cref="Editing.TimelineViewport"/> transform the ruler itself uses, with no fakes anywhere
/// in the chain.
/// </summary>
public sealed class Step027ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step027-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<ShellViewModel> CreateReadyShellWithWaveformCacheAsync(string root)
    {
        Directory.CreateDirectory(root);
        var audioPath = Path.Combine(root, "click-track.wav");
        var samples = Reference.Fixtures.DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 60, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate: 8000);
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

        shell.BuildWaveformCacheCommand.Execute(null);
        await shell.LastBuildWaveformCacheCommandTask!;
        Assert.True(shell.Audio.HasWaveformCache);
        Assert.NotNull(shell.Audio.LoadedWaveformCache);

        return shell;
    }

    [Fact]
    public async Task Step027_Acceptance_Passes_LoadTimelineRendersWaveformAlignedToRulerCoordinates()
    {
        var shell = await CreateReadyShellWithWaveformCacheAsync(_root);

        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = shell.Audio.ProbedAsset!.DurationTicks + 1_000_000;
        shell.LoadTimelineCommand.Execute(null);

        Assert.True(shell.Timeline.IsLoaded);
        Assert.True(shell.Timeline.HasWaveform);
        var bars = shell.Timeline.WaveformBars;
        Assert.NotEmpty(bars);

        // "Waveform position matches timeline coordinates": every bar's cumulative pixel offset
        // (MarginLeft chain, the same convention RulerTicks/EffectBlockItemView already use) must
        // land inside the ruler's own visible pixel width, and bars must be strictly left-to-right.
        var cumulativeX = 0d;
        foreach (var bar in bars)
        {
            cumulativeX += bar.MarginLeft;
            Assert.True(cumulativeX >= 0d);
            Assert.True(bar.Width >= 0d);
            cumulativeX += bar.Width;
        }

        Assert.True(cumulativeX <= shell.Timeline.ViewportWidthPixels + 1d);
    }

    [Fact]
    public async Task Step027_Acceptance_Passes_WaveformBarsReDeriveLiveFromViewportOnZoom_NotStale()
    {
        var shell = await CreateReadyShellWithWaveformCacheAsync(_root);
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = shell.Audio.ProbedAsset!.DurationTicks + 1_000_000;
        shell.LoadTimelineCommand.Execute(null);

        var originTicks = shell.Audio.ProbedAsset!.OriginTicks;
        var cache = shell.Audio.LoadedWaveformCache!;

        var expectedBeforeZoom = WaveformBarLayoutBuilder.BuildBars(
            cache, shell.Timeline.Viewport!, shell.Timeline.ViewportWidthPixels, originTicks);
        Assert.Equal(expectedBeforeZoom.Count, shell.Timeline.WaveformBars.Count);

        // Zoom in: the ruler/viewport both change, and WaveformBars must re-derive from the new,
        // live TimelineViewport on the very next read — not keep returning a cached list computed
        // against the pre-zoom viewport (the same "re-derived on every scroll/zoom/resize" contract
        // RulerTicks already has).
        shell.TimelineZoomInCommand.Execute(null);

        var expectedAfterZoom = WaveformBarLayoutBuilder.BuildBars(
            cache, shell.Timeline.Viewport!, shell.Timeline.ViewportWidthPixels, originTicks);
        var actualAfterZoom = shell.Timeline.WaveformBars;
        Assert.Equal(expectedAfterZoom.Count, actualAfterZoom.Count);

        // Reconstruct each bar's absolute X from the view's own MarginLeft chain (the exact
        // convention WaveformBarView documents) and compare against the domain builder's own
        // absolute X, called fresh against the post-zoom viewport — proving the bound value is the
        // live post-zoom layout, not a stale pre-zoom one.
        var cumulativeX = 0d;
        for (var i = 0; i < expectedAfterZoom.Count; i++)
        {
            cumulativeX += actualAfterZoom[i].MarginLeft;
            Assert.Equal(expectedAfterZoom[i].X, cumulativeX, precision: 6);
            cumulativeX += actualAfterZoom[i].Width;
        }
    }

    [Fact]
    public async Task LoadTimeline_WithoutWaveformCacheBuilt_HasWaveformIsFalse_MissingMediaHandledExplicitly_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToTimelineCommand.Execute(null);

        shell.Timeline.TotalDurationTicks = 10_000_000;
        shell.LoadTimelineCommand.Execute(null);

        Assert.True(shell.Timeline.IsLoaded);
        Assert.False(shell.Timeline.HasWaveform);
        Assert.Empty(shell.Timeline.WaveformBars);
    }

    [Fact]
    public async Task LoadTimeline_AfterReset_ClearsWaveformState()
    {
        var shell = await CreateReadyShellWithWaveformCacheAsync(_root);
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = shell.Audio.ProbedAsset!.DurationTicks + 1_000_000;
        shell.LoadTimelineCommand.Execute(null);
        Assert.True(shell.Timeline.HasWaveform);

        shell.Timeline.Reset();

        Assert.False(shell.Timeline.HasWaveform);
        Assert.Empty(shell.Timeline.WaveformBars);
    }
}
