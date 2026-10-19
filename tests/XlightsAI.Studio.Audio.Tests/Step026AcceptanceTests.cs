using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Audio.Tests;

/// <summary>
/// PHASE-04 / Step 026 — "Generate zoom-independent multiresolution waveform peaks cache." Required
/// acceptance: "Cache reopens without recompute; envelope sample checks." Failure/edge case:
/// "Silence, invalid timing boundaries and missing media must be handled explicitly." Implementation
/// guidance: "Generate min/max/RMS envelopes for multiple sample block sizes and content hash; write
/// cache only after complete decode and verified dimensions."
/// </summary>
public sealed class Step026AcceptanceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step026-{Guid.NewGuid():N}");

    public Step026AcceptanceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private async Task<SongAsset> ProbeClickTrackAsync(string fileName, int bpm, int clickCount, int sampleRate = 8000)
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate);
        var path = Path.Combine(_tempDir, fileName);
        await File.WriteAllBytesAsync(path, wavBytes);

        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    [Fact]
    public async Task Step026_Acceptance_Passes_BuildsMultiResolutionPeaksAndReopensWithoutRecompute()
    {
        // ~6 seconds at 8 kHz => 48000 samples: long enough that every pyramid level (up to the
        // coarsest 16384-sample block) has more than one envelope.
        var asset = await ProbeClickTrackAsync("click-track.wav", bpm: 60, clickCount: 6);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonWaveformPyramidStore(Path.Combine(_tempDir, "waveform-cache"));
        var builder = new WaveformPyramidBuilder(decoder, store);

        var path1 = await builder.BuildPyramidAsync(asset, CancellationToken.None);
        Assert.True(File.Exists(path1));
        Assert.Equal(1, decoder.DecodeCallCount);

        var cache = await store.LoadAsync(path1, CancellationToken.None);

        // "Generate min/max/RMS envelopes for multiple sample block sizes" — every declared
        // resolution level is present.
        Assert.Equal(5, cache.Resolutions.Length);
        Assert.Equal([64, 256, 1024, 4096, 16384], cache.Resolutions.Select(r => r.BlockSizeSamples).ToArray());

        // "Content hash" cache key recorded (Section 4).
        Assert.Equal(asset.Sha256, cache.ContentHash);
        Assert.Equal(asset.DecoderVersion, cache.SourceDecoderVersion);
        Assert.Equal(WaveformPyramidBuilder.Version, cache.BuilderVersion);
        Assert.Equal(asset.SampleRate, cache.SourceSampleRate);
        Assert.Equal(asset.SampleCount, cache.SampleCount);

        // Envelope sample checks: every level's envelopes are contiguous, half-open, and exactly
        // cover [0, SampleCount) — real dimension verification, not just "some data was written".
        foreach (var level in cache.Resolutions)
        {
            Assert.True(level.Envelopes.Length > 0);
            long expectedStart = 0;
            foreach (var envelope in level.Envelopes)
            {
                Assert.Equal(expectedStart, envelope.StartSample);
                Assert.True(envelope.EndSample > envelope.StartSample);
                Assert.True(envelope.Max >= envelope.Min);
                expectedStart = envelope.EndSample;
            }

            Assert.Equal(asset.SampleCount, expectedStart);
        }

        // The finest level has more envelopes than the coarsest one (genuinely multi-resolution,
        // not five identical copies of the same summary).
        Assert.True(cache.Resolutions[0].Envelopes.Length > cache.Resolutions[^1].Envelopes.Length);

        // A 1 kHz tone's peak amplitude should show up close to the synthesized signal's own +/-1.0
        // range somewhere in the finest level (real decoded content, not placeholder zeros).
        Assert.Contains(cache.Resolutions[0].Envelopes, e => e.Max > 0.5f);
        Assert.Contains(cache.Resolutions[0].Envelopes, e => e.Min < -0.5f);

        // "Cache reopens without recompute": calling again for the exact same (unchanged) asset must
        // return the same path without a second real decode.
        var path2 = await builder.BuildPyramidAsync(asset, CancellationToken.None);
        Assert.Equal(path1, path2);
        Assert.Equal(1, decoder.DecodeCallCount); // still 1 — no recompute happened.

        // Record equality on an array-typed property (Resolutions) is reference equality, not
        // structural, so compare the fields/shape that actually matter instead of the whole record.
        var reopened = await store.LoadAsync(path2, CancellationToken.None);
        Assert.Equal(cache.SongId, reopened.SongId);
        Assert.Equal(cache.ContentHash, reopened.ContentHash);
        Assert.Equal(cache.Resolutions.Length, reopened.Resolutions.Length);
        Assert.Equal(cache.Resolutions[0].Envelopes.Length, reopened.Resolutions[0].Envelopes.Length);
    }

    [Fact]
    public async Task BuildPyramidAsync_SilentAudio_ComputesZeroEnvelopesWithoutThrowing_HandlesSilenceExplicitly()
    {
        var silentSamples = new float[8000 * 2]; // 2 seconds of true digital silence at 8 kHz.
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(silentSamples, sampleRate: 8000);
        var path = Path.Combine(_tempDir, "silence.wav");
        await File.WriteAllBytesAsync(path, wavBytes);
        var asset = await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);

        var builder = new WaveformPyramidBuilder(new WavAudioDecoder(), new JsonWaveformPyramidStore(Path.Combine(_tempDir, "waveform-cache")));
        var cachePath = await builder.BuildPyramidAsync(asset, CancellationToken.None);

        var cache = await new JsonWaveformPyramidStore(Path.Combine(_tempDir, "waveform-cache")).LoadAsync(cachePath, CancellationToken.None);

        Assert.All(cache.Resolutions, level => Assert.All(level.Envelopes, e =>
        {
            Assert.Equal(0f, e.Min);
            Assert.Equal(0f, e.Max);
            Assert.Equal(0f, e.Rms);
        }));
    }

    [Fact]
    public async Task BuildPyramidAsync_MissingMediaFile_PropagatesFileNotFoundException_NegativePath()
    {
        var asset = await ProbeClickTrackAsync("moved.wav", bpm: 120, clickCount: 4);
        File.Delete(Path.Combine(_tempDir, "moved.wav"));

        var store = new JsonWaveformPyramidStore(Path.Combine(_tempDir, "waveform-cache"));
        var builder = new WaveformPyramidBuilder(new WavAudioDecoder(), store);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => builder.BuildPyramidAsync(asset, CancellationToken.None));

        Assert.False(File.Exists(store.GetPath(asset.Id)));
    }

    [Fact]
    public async Task BuildPyramidAsync_DeclaredSampleCountMismatch_ThrowsWaveformPyramidInvalidException_NegativePath()
    {
        // "invalid timing boundaries... must be handled explicitly" — a SongAsset whose declared
        // SampleCount does not match what the real file actually decodes to (e.g. a stale/edited
        // record) must fail clearly rather than silently cache a mismatched pyramid.
        var asset = await ProbeClickTrackAsync("mismatch.wav", bpm: 120, clickCount: 4);
        var doctored = asset with { SampleCount = asset.SampleCount + 1000 };

        var store = new JsonWaveformPyramidStore(Path.Combine(_tempDir, "waveform-cache"));
        var builder = new WaveformPyramidBuilder(new WavAudioDecoder(), store);

        var ex = await Assert.ThrowsAsync<WaveformPyramidInvalidException>(
            () => builder.BuildPyramidAsync(doctored, CancellationToken.None));

        Assert.Contains("sample count", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(store.GetPath(doctored.Id)));
    }

    [Fact]
    public async Task BuildPyramidAsync_ContentHashChanges_InvalidatesCacheAndRebuilds()
    {
        // Phase-wide test matrix row 7: "Waveform cache invalidates when content hash changes."
        var asset = await ProbeClickTrackAsync("hash-change.wav", bpm: 120, clickCount: 4);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonWaveformPyramidStore(Path.Combine(_tempDir, "waveform-cache"));
        var builder = new WaveformPyramidBuilder(decoder, store);

        var path1 = await builder.BuildPyramidAsync(asset, CancellationToken.None);
        Assert.Equal(1, decoder.DecodeCallCount);

        // Same SongId, different recorded content hash — simulates a re-probe of edited audio under
        // the same identity, which must invalidate (not silently reuse) the old pyramid.
        var changed = asset with { Sha256 = "0000000000000000000000000000000000000000000000000000000000000" };
        var path2 = await builder.BuildPyramidAsync(changed, CancellationToken.None);

        Assert.Equal(path1, path2); // same deterministic path (same SongId)...
        Assert.Equal(2, decoder.DecodeCallCount); // ...but a real second decode/rebuild happened.

        var rebuilt = await store.LoadAsync(path2, CancellationToken.None);
        Assert.Equal(changed.Sha256, rebuilt.ContentHash);
    }

    /// <summary>Wraps a real <see cref="IAudioDecoder"/>, counting <see cref="DecodeMonoAsync"/> calls so a test can prove "cache reopens without recompute" against real decode behavior, not a hand-rolled fake.</summary>
    private sealed class CountingAudioDecoder(IAudioDecoder inner) : IAudioDecoder
    {
        public int DecodeCallCount { get; private set; }

        public Task<SongAsset> ProbeAsync(string path, CancellationToken ct) => inner.ProbeAsync(path, ct);

        public IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(SongAsset song, int targetSampleRate, CancellationToken ct)
        {
            DecodeCallCount++;
            return inner.DecodeMonoAsync(song, targetSampleRate, ct);
        }
    }
}
