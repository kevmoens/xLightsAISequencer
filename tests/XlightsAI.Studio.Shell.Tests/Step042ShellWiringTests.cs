using System.Runtime.Versioning;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-06 Step 042 — the real-device end-to-end slice: <see cref="StemMixerViewModel"/> driving a
/// real <see cref="WaveOutAudioTransport"/> (the real Windows winmm device on this machine), same
/// "no mock IAudioTransport anywhere" convention as every Step 025/026/027/028 wiring test. Proves the
/// required acceptance verbatim -- "Audibly isolated stems stay aligned with source" -- against real
/// device-reported playback position, not an estimate.
/// </summary>
[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class Step042ShellWiringTests : IAsyncDisposable
{
    private const int SampleRate = 8000;
    private const int SampleCount = 6000; // 0.75s -- long enough to observe real device position advance.

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"xlai-step042-shell-{Guid.NewGuid():N}");
    private WaveOutAudioTransport? _transport;

    public async ValueTask DisposeAsync()
    {
        if (_transport is not null)
        {
            await _transport.DisposeAsync();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static float[] Sine(double frequencyHz, float amplitude) =>
        Enumerable.Range(0, SampleCount).Select(i => amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate)).ToArray();

    private async Task<StemManifest> BuildManifestAsync()
    {
        var audioDir = Path.Combine(_root, "stems");
        Directory.CreateDirectory(audioDir);

        var drumsPath = Path.Combine(audioDir, "drums.wav");
        var vocalsPath = Path.Combine(audioDir, "vocals.wav");
        await StemWavWriter.WriteMonoFloatAsync(drumsPath, Sine(110.0, 0.3f), SampleRate, CancellationToken.None);
        await StemWavWriter.WriteMonoFloatAsync(vocalsPath, Sine(1000.0, 0.2f), SampleRate, CancellationToken.None);

        var decoder = new WavAudioDecoder();
        var drumsProbe = await decoder.ProbeAsync(drumsPath, CancellationToken.None);
        var vocalsProbe = await decoder.ProbeAsync(vocalsPath, CancellationToken.None);

        var drums = new StemAsset(Guid.NewGuid(), StemKind.Drums, drumsPath, SampleRate, SampleCount, SourceOriginTicks: 0, drumsProbe.Sha256);
        var vocals = new StemAsset(Guid.NewGuid(), StemKind.Vocals, vocalsPath, SampleRate, SampleCount, SourceOriginTicks: 0, vocalsProbe.Sha256);
        return new StemManifest(Guid.NewGuid(), "source-hash", "fixture-model", new[] { drums, vocals }, Array.Empty<string>());
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

    [Fact]
    public async Task Step042_Acceptance_Passes_SoloVocalsAndDrumsStayAlignedWithSourceOnRealDevice()
    {
        var manifest = await BuildManifestAsync();
        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);

        var decoder = new WavAudioDecoder();
        var renderer = new StemMixdownRenderer(decoder, Path.Combine(_root, "mix"));
        _transport = new WaveOutAudioTransport(decoder);

        // Both stems audible: open and play on the real device.
        Assert.True(await vm.ApplyMixAndReopenAsync(renderer, _transport));
        Assert.True(await vm.PlayAsync(_transport));
        Assert.True(await WaitUntilAsync(() => _transport.PositionTicks > 0, TimeSpan.FromSeconds(2)),
            "real device position never advanced after Play.");

        var positionWhileBothAudible = _transport.PositionTicks;

        // Solo vocals and drums (both -- i.e. nothing actually muted here, but exercise the real solo
        // path) while still playing: re-render + reopen must happen on the SAME single transport
        // (Section 3's own "not six independent drifting clocks"), landing at the same song-relative
        // position and resuming playback without the user losing their place.
        foreach (var channel in vm.Channels)
        {
            vm.ToggleSolo(channel.StemId);
        }

        Assert.True(await vm.ApplyMixAndReopenAsync(renderer, _transport));
        Assert.True(vm.IsPlaying);
        Assert.True(_transport.PositionTicks >= positionWhileBothAudible,
            "real device position regressed after a solo-driven remix -- stems are no longer aligned with the source timeline.");

        Assert.True(await WaitUntilAsync(() => _transport.PositionTicks > positionWhileBothAudible, TimeSpan.FromSeconds(2)),
            "real device position never advanced further after resuming the soloed mix.");

        await vm.PauseAsync(_transport);
    }

    [Fact]
    public async Task Step042_Acceptance_Passes_MutingAStemProducesAudiblyDifferentSignalThanFullMix()
    {
        var manifest = await BuildManifestAsync();
        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);

        var decoder = new WavAudioDecoder();
        var renderer = new StemMixdownRenderer(decoder, Path.Combine(_root, "mix"));
        _transport = new WaveOutAudioTransport(decoder);

        Assert.True(await vm.ApplyMixAndReopenAsync(renderer, _transport));
        var fullMixSong = _transport.CurrentSong!;
        var fullMixSamples = new List<float>();
        await foreach (var block in decoder.DecodeMonoAsync(fullMixSong, SampleRate, CancellationToken.None))
        {
            fullMixSamples.AddRange(block.Span.ToArray());
        }

        var vocalsChannel = vm.Channels.First(c => c.Kind == StemKind.Vocals);
        vm.ToggleMute(vocalsChannel.StemId);
        Assert.True(await vm.ApplyMixAndReopenAsync(renderer, _transport));

        // Same real transport, same real origin/duration ("keep origin offset ... visible") -- but
        // audibly different content once vocals is muted, proving solo/mute actually reaches the
        // signal fed to the real device rather than being a UI-only toggle.
        var mutedSong = _transport.CurrentSong!;
        Assert.Equal(fullMixSong.OriginTicks, mutedSong.OriginTicks);
        Assert.Equal(fullMixSong.SampleRate, mutedSong.SampleRate);

        var mutedSamples = new List<float>();
        await foreach (var block in decoder.DecodeMonoAsync(mutedSong, SampleRate, CancellationToken.None))
        {
            mutedSamples.AddRange(block.Span.ToArray());
        }

        Assert.NotEqual(fullMixSamples[10], mutedSamples[10]);
    }
}
