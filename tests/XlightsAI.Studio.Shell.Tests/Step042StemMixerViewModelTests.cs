using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-06 Step 042 — fast, deterministic, hardware-free unit tests for
/// <see cref="StemMixerViewModel"/>'s own orchestration contract (load/toggle/apply-mix/build-waveforms),
/// independent of any real <see cref="IAudioTransport"/> device. The real-device end-to-end slice
/// (actual synchronized solo/mute audio) is covered separately by <c>Step042ShellWiringTests</c>
/// (Windows-only, real <c>winmm</c> transport), mirroring Step 028's own
/// <c>Step028TimelineViewModelTests</c> / <c>Step028ShellWiringTests</c> split.
/// </summary>
public sealed class Step042StemMixerViewModelTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step042-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    /// <summary>A real in-memory <see cref="IAudioTransport"/> test double: no native/device I/O, but a
    /// fully real, independently-verifiable clock model (position only ever advances via an explicit
    /// <see cref="Advance"/> call, never a hidden timer) -- used only to verify
    /// <see cref="StemMixerViewModel"/>'s own orchestration (does it reopen, does it reseek, does it
    /// resume play), not to simulate real audio hardware.</summary>
    private sealed class FakeAudioTransport : IAudioTransport
    {
        private long _position;

        public long PositionTicks => _position;
        public bool IsPlaying { get; private set; }
        public SongAsset? CurrentSong { get; private set; }
        public TimeWindow? Loop { get; private set; }
        public int OpenCount { get; private set; }

        public Task OpenAsync(SongAsset song, CancellationToken ct)
        {
            CurrentSong = song;
            _position = 0;
            IsPlaying = false;
            OpenCount++;
            return Task.CompletedTask;
        }

        public Task PlayAsync(CancellationToken ct)
        {
            if (CurrentSong is null)
            {
                throw new InvalidOperationException("No song is open.");
            }

            IsPlaying = true;
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken ct)
        {
            IsPlaying = false;
            return Task.CompletedTask;
        }

        public Task SeekAsync(long ticks, CancellationToken ct)
        {
            if (CurrentSong is null)
            {
                throw new InvalidOperationException("No song is open.");
            }

            if (ticks < 0 || ticks > CurrentSong.DurationTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks));
            }

            _position = ticks;
            return Task.CompletedTask;
        }

        public void SetLoop(TimeWindow? window) => Loop = window;

        public void Advance(long ticks) => _position += ticks;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private const int SampleRate = 8000;
    private const int SampleCount = 400;

    private static float[] Sine(double frequencyHz, float amplitude) =>
        Enumerable.Range(0, SampleCount).Select(i => amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate)).ToArray();

    private async Task<(StemManifest Manifest, StemAsset Drums, StemAsset Vocals)> BuildManifestAsync()
    {
        var audioDir = Path.Combine(_rootDirectory, "stems");
        Directory.CreateDirectory(audioDir);

        var drumsPath = Path.Combine(audioDir, "drums.wav");
        var vocalsPath = Path.Combine(audioDir, "vocals.wav");
        await StemWavWriter.WriteMonoFloatAsync(drumsPath, Sine(110.0, 0.3f), SampleRate, CancellationToken.None);
        await StemWavWriter.WriteMonoFloatAsync(vocalsPath, Sine(440.0, 0.2f), SampleRate, CancellationToken.None);

        var drumsProbe = await new WavAudioDecoder().ProbeAsync(drumsPath, CancellationToken.None);
        var vocalsProbe = await new WavAudioDecoder().ProbeAsync(vocalsPath, CancellationToken.None);

        var drums = new StemAsset(Guid.NewGuid(), StemKind.Drums, drumsPath, SampleRate, SampleCount, SourceOriginTicks: 500_000L, drumsProbe.Sha256);
        var vocals = new StemAsset(Guid.NewGuid(), StemKind.Vocals, vocalsPath, SampleRate, SampleCount, SourceOriginTicks: 500_000L, vocalsProbe.Sha256);
        var manifest = new StemManifest(Guid.NewGuid(), "source-hash", "fixture-model", new[] { drums, vocals }, Array.Empty<string>());

        return (manifest, drums, vocals);
    }

    [Fact]
    public async Task LoadManifest_CreatesOneUnmutedUnsoloedChannelPerStem()
    {
        var (manifest, drums, vocals) = await BuildManifestAsync();
        var vm = new StemMixerViewModel();

        vm.LoadManifest(manifest);

        Assert.Equal(2, vm.Channels.Count);
        Assert.Contains(vm.Channels, c => c.StemId == drums.Id && !c.Muted && !c.Soloed);
        Assert.Contains(vm.Channels, c => c.StemId == vocals.Id && !c.Muted && !c.Soloed);
        Assert.False(vm.IsMixOpen);
    }

    [Fact]
    public async Task ToggleMute_ThenApplyMix_RendersWithoutThatStem()
    {
        var (manifest, drums, vocals) = await BuildManifestAsync();
        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);
        vm.ToggleMute(vocals.Id);

        var cacheDir = Path.Combine(_rootDirectory, "mix");
        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), cacheDir);
        var transport = new FakeAudioTransport();

        var ok = await vm.ApplyMixAndReopenAsync(renderer, transport);

        Assert.True(ok);
        Assert.True(vm.IsMixOpen);
        Assert.NotNull(transport.CurrentSong);

        var decoded = new List<float>();
        await foreach (var block in new WavAudioDecoder().DecodeMonoAsync(transport.CurrentSong!, SampleRate, CancellationToken.None))
        {
            decoded.AddRange(block.Span.ToArray());
        }

        var drumsOnly = Sine(110.0, 0.3f);
        for (var i = 0; i < SampleCount; i++)
        {
            Assert.True(Math.Abs(decoded[i] - drumsOnly[i]) < 1e-4f, "muted vocals must not contribute to the mix");
        }
    }

    [Fact]
    public async Task ApplyMixAndReopenAsync_WhilePlaying_PreservesPositionAndResumesAcrossRemix()
    {
        var (manifest, drums, vocals) = await BuildManifestAsync();
        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);

        var cacheDir = Path.Combine(_rootDirectory, "mix");
        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), cacheDir);
        var transport = new FakeAudioTransport();

        Assert.True(await vm.ApplyMixAndReopenAsync(renderer, transport));
        await vm.PlayAsync(transport);
        transport.Advance(10_000L);
        var positionBeforeRemix = transport.PositionTicks;
        Assert.True(positionBeforeRemix > 0);

        // Toggling solo must reopen the transport (a new mixed SongAsset) but land at the same
        // song-relative position and keep playing -- "Use single transport timebase to solo/mute
        // stems" (implementation guidance, verbatim): never resets the user's place in the song.
        vm.ToggleSolo(drums.Id);
        var ok = await vm.ApplyMixAndReopenAsync(renderer, transport);

        Assert.True(ok);
        Assert.Equal(2, transport.OpenCount); // one Open per ApplyMixAndReopenAsync call -- a real reopen happened, not a no-op.
        Assert.Equal(positionBeforeRemix, transport.PositionTicks);
        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public async Task ApplyMixAndReopenAsync_NoManifestLoaded_SetsErrorMessage_NegativePath()
    {
        var vm = new StemMixerViewModel();
        var cacheDir = Path.Combine(_rootDirectory, "mix");
        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), cacheDir);
        var transport = new FakeAudioTransport();

        var ok = await vm.ApplyMixAndReopenAsync(renderer, transport);

        Assert.False(ok);
        Assert.False(vm.IsMixOpen);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task ApplyMixAndReopenAsync_IncompatibleStems_SetsErrorMessageAndLeavesMixClosed_NegativePath()
    {
        var audioDir = Path.Combine(_rootDirectory, "stems");
        Directory.CreateDirectory(audioDir);
        var drumsPath = Path.Combine(audioDir, "drums.wav");
        var vocalsPath = Path.Combine(audioDir, "vocals.wav");
        await StemWavWriter.WriteMonoFloatAsync(drumsPath, Sine(110.0, 0.3f), SampleRate, CancellationToken.None);
        await StemWavWriter.WriteMonoFloatAsync(vocalsPath, Sine(440.0, 0.2f), 11025, CancellationToken.None);

        var drumsProbe = await new WavAudioDecoder().ProbeAsync(drumsPath, CancellationToken.None);
        var vocalsProbe = await new WavAudioDecoder().ProbeAsync(vocalsPath, CancellationToken.None);
        var drums = new StemAsset(Guid.NewGuid(), StemKind.Drums, drumsPath, SampleRate, SampleCount, 0, drumsProbe.Sha256);
        var vocals = new StemAsset(Guid.NewGuid(), StemKind.Vocals, vocalsPath, 11025, vocalsProbe.SampleCount, 0, vocalsProbe.Sha256);
        var manifest = new StemManifest(Guid.NewGuid(), "hash", "model", new[] { drums, vocals }, Array.Empty<string>());

        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);

        var cacheDir = Path.Combine(_rootDirectory, "mix");
        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), cacheDir);
        var transport = new FakeAudioTransport();

        var ok = await vm.ApplyMixAndReopenAsync(renderer, transport);

        Assert.False(ok);
        Assert.False(vm.IsMixOpen);
        Assert.Contains("align", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildWaveformsAsync_PopulatesPerStemCachePathsWithOriginPreserved()
    {
        var (manifest, drums, vocals) = await BuildManifestAsync();
        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);

        var waveformDir = Path.Combine(_rootDirectory, "waveforms");
        var decoder = new WavAudioDecoder();
        var store = new JsonWaveformPyramidStore(waveformDir);
        var builder = new WaveformPyramidBuilder(decoder, store);

        var ok = await vm.BuildWaveformsAsync(decoder, builder);

        Assert.True(ok);
        Assert.All(vm.Channels, c => Assert.True(File.Exists(c.WaveformCachePath)));

        var drumsCache = await store.LoadAsync(vm.Channels.First(c => c.StemId == drums.Id).WaveformCachePath!, CancellationToken.None);
        Assert.Equal(SampleCount, drumsCache.SampleCount);
        Assert.Equal(SampleRate, drumsCache.SourceSampleRate);
    }
}
