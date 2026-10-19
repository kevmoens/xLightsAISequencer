using System.Security.Cryptography;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06 Step 042 — "Add waveform tracks, solo/mute and synchronized stem playback." Required
/// acceptance (verbatim): "Audibly isolated stems stay aligned with source." Implementation guidance
/// (verbatim): "Use single transport timebase to solo/mute stems; keep origin offset and resample
/// alignment visible; cache separate waveform peaks." Failure/edge case (shared phase-wide boilerplate,
/// verbatim): "Missing or incompatible model, cancellation and offline mode must yield a recoverable
/// state" -- reinterpreted here as this step's own mixdown boundary (incompatible/stale stems,
/// cancellation).
///
/// Exercises <see cref="StemMixdownRenderer"/> (the real file-backed mixdown engine) directly against
/// real on-disk WAV stem files written by the already-real <see cref="StemWavWriter"/>; no ONNX
/// separation is needed for this step's own scope (mixing already-produced stems), so no model fixture
/// is reused here, unlike Steps 038-041.
/// </summary>
public sealed class Step042AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private const int SampleCount = 400;
    private const long OriginTicks = 123_456L;

    private readonly string _rootDirectory;
    private readonly string _audioDirectory;
    private readonly string _cacheDirectory;

    public Step042AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step042-{Guid.NewGuid():N}");
        _audioDirectory = Path.Combine(_rootDirectory, "stems");
        _cacheDirectory = Path.Combine(_rootDirectory, "mixdown");
        Directory.CreateDirectory(_audioDirectory);
        Directory.CreateDirectory(_cacheDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static float[] DeterministicSineWave(int sampleCount, double frequencyHz, float amplitude) =>
        Enumerable.Range(0, sampleCount)
            .Select(i => amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate))
            .ToArray();

    private async Task<StemAsset> WriteStemAsync(StemKind kind, float[] samples, int sampleRate = SampleRate, long originTicks = OriginTicks)
    {
        var path = Path.Combine(_audioDirectory, $"{kind}-{Guid.NewGuid():N}.wav");
        await StemWavWriter.WriteMonoFloatAsync(path, samples, sampleRate, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StemAsset(Guid.NewGuid(), kind, path, sampleRate, samples.Length, originTicks, sha256);
    }

    private static StemManifest Manifest(params StemAsset[] stems) =>
        new(Guid.NewGuid(), OriginalSongHash: "source-hash", ModelId: "fixture-model", Stems: stems, Warnings: Array.Empty<string>());

    private static async Task<float[]> DecodeAsync(SongAsset song)
    {
        var result = new List<float>();
        await foreach (var block in new WavAudioDecoder().DecodeMonoAsync(song, song.SampleRate, CancellationToken.None))
        {
            result.AddRange(block.Span.ToArray());
        }

        return result.ToArray();
    }

    [Fact]
    public async Task Step042_Acceptance_Passes_MixdownSumsActiveStemsAndStaysAligned()
    {
        var drumSamples = DeterministicSineWave(SampleCount, 110.0, 0.3f);
        var vocalSamples = DeterministicSineWave(SampleCount, 440.0, 0.2f);

        var drums = await WriteStemAsync(StemKind.Drums, drumSamples);
        var vocals = await WriteStemAsync(StemKind.Vocals, vocalSamples);
        var manifest = Manifest(drums, vocals);

        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), _cacheDirectory);
        var channels = new[]
        {
            new StemChannelState(drums.Id, Muted: false, Soloed: false),
            new StemChannelState(vocals.Id, Muted: false, Soloed: false),
        };

        var mixed = await renderer.RenderMixAsync(manifest, channels, CancellationToken.None);

        // "keep origin offset and resample alignment visible" -- the mix must declare the exact same
        // origin/sample-rate/sample-count as the source stems, not a re-zeroed/truncated approximation.
        Assert.Equal(SampleRate, mixed.SampleRate);
        Assert.Equal(SampleCount, mixed.SampleCount);
        Assert.Equal(OriginTicks, mixed.OriginTicks);

        var decoded = await DecodeAsync(mixed);
        Assert.Equal(SampleCount, decoded.Length);
        for (var i = 0; i < SampleCount; i++)
        {
            var expected = Math.Clamp(drumSamples[i] + vocalSamples[i], -1f, 1f);
            Assert.True(Math.Abs(decoded[i] - expected) < 1e-4f, $"sample {i}: expected ~{expected}, got {decoded[i]}");
        }
    }

    [Fact]
    public async Task SoloOverridesOtherStems_OnlySoloedStemIsAudible()
    {
        var drumSamples = DeterministicSineWave(SampleCount, 110.0, 0.3f);
        var vocalSamples = DeterministicSineWave(SampleCount, 440.0, 0.2f);

        var drums = await WriteStemAsync(StemKind.Drums, drumSamples);
        var vocals = await WriteStemAsync(StemKind.Vocals, vocalSamples);
        var manifest = Manifest(drums, vocals);

        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), _cacheDirectory);
        var channels = new[]
        {
            new StemChannelState(drums.Id, Muted: false, Soloed: true),
            new StemChannelState(vocals.Id, Muted: false, Soloed: false),
        };

        var mixed = await renderer.RenderMixAsync(manifest, channels, CancellationToken.None);
        var decoded = await DecodeAsync(mixed);

        for (var i = 0; i < SampleCount; i++)
        {
            Assert.True(Math.Abs(decoded[i] - drumSamples[i]) < 1e-4f, $"sample {i}: soloed drums expected ~{drumSamples[i]}, got {decoded[i]}");
        }
    }

    [Fact]
    public async Task MuteWinsOverSolo_MutedAndSoloedChannelIsSilent_AndAllMutedYieldsFiniteSilence_NegativePath()
    {
        var drumSamples = DeterministicSineWave(SampleCount, 110.0, 0.3f);
        var vocalSamples = DeterministicSineWave(SampleCount, 440.0, 0.2f);

        var drums = await WriteStemAsync(StemKind.Drums, drumSamples);
        var vocals = await WriteStemAsync(StemKind.Vocals, vocalSamples);
        var manifest = Manifest(drums, vocals);

        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), _cacheDirectory);

        // Drums is both muted AND soloed: mute must win for its own channel. Vocals is neither muted
        // nor soloed, but since at least one channel (drums) is soloed, every non-soloed channel is
        // silenced too -- so the whole mix is silent, exercising the "no active stems" path without
        // ever throwing.
        var channels = new[]
        {
            new StemChannelState(drums.Id, Muted: true, Soloed: true),
            new StemChannelState(vocals.Id, Muted: false, Soloed: false),
        };

        var mixed = await renderer.RenderMixAsync(manifest, channels, CancellationToken.None);
        var decoded = await DecodeAsync(mixed);

        Assert.Equal(SampleCount, decoded.Length);
        foreach (var sample in decoded)
        {
            Assert.True(float.IsFinite(sample), "a fully-muted mix must never produce non-finite output");
            Assert.Equal(0f, sample);
        }
    }

    [Fact]
    public async Task RenderMixAsync_SecondCallSameSelection_ReusesCacheEvenAfterStemFilesAreDeleted()
    {
        var drumSamples = DeterministicSineWave(SampleCount, 110.0, 0.3f);
        var vocalSamples = DeterministicSineWave(SampleCount, 440.0, 0.2f);

        var drums = await WriteStemAsync(StemKind.Drums, drumSamples);
        var vocals = await WriteStemAsync(StemKind.Vocals, vocalSamples);
        var manifest = Manifest(drums, vocals);

        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), _cacheDirectory);
        var channels = new[]
        {
            new StemChannelState(drums.Id, Muted: false, Soloed: false),
            new StemChannelState(vocals.Id, Muted: false, Soloed: false),
        };

        var first = await renderer.RenderMixAsync(manifest, channels, CancellationToken.None);

        // Delete the real stem files; if the second call were to recompute (rather than reuse the
        // real on-disk cache), it would throw FileNotFoundException.
        File.Delete(drums.FilePath);
        File.Delete(vocals.FilePath);

        var second = await renderer.RenderMixAsync(manifest, channels, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.FilePath, second.FilePath);
        Assert.Equal(first.Sha256, second.Sha256);
    }

    [Fact]
    public async Task IncompatibleStemSampleRates_ThrowsStemMixdownIncompatibleStemsException_NegativePath()
    {
        var drums = await WriteStemAsync(StemKind.Drums, DeterministicSineWave(SampleCount, 110.0, 0.3f), sampleRate: 8000);
        var vocals = await WriteStemAsync(StemKind.Vocals, DeterministicSineWave(200, 440.0, 0.2f), sampleRate: 11025);
        var manifest = Manifest(drums, vocals);

        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), _cacheDirectory);
        var channels = new[]
        {
            new StemChannelState(drums.Id, Muted: false, Soloed: false),
            new StemChannelState(vocals.Id, Muted: false, Soloed: false),
        };

        await Assert.ThrowsAsync<StemMixdownIncompatibleStemsException>(
            () => renderer.RenderMixAsync(manifest, channels, CancellationToken.None));

        var cacheDir = Path.Combine(_cacheDirectory, ".mixdown-cache");
        Assert.False(Directory.Exists(cacheDir) && Directory.GetFiles(cacheDir).Length > 0,
            "no mixdown file must be written when the stems are declared incompatible");
    }

    [Fact]
    public async Task StemFileModifiedAfterManifest_ThrowsStemMixdownIncompatibleStemsException_NegativePath()
    {
        var drums = await WriteStemAsync(StemKind.Drums, DeterministicSineWave(SampleCount, 110.0, 0.3f));
        var vocals = await WriteStemAsync(StemKind.Vocals, DeterministicSineWave(SampleCount, 440.0, 0.2f));
        var manifest = Manifest(drums, vocals);

        // Overwrite the vocals stem's real file content without updating its declared manifest
        // hash -- a stale/corrupt stem, the exact scenario StemSongAssetFactory's cross-check exists
        // to catch.
        await StemWavWriter.WriteMonoFloatAsync(vocals.FilePath, DeterministicSineWave(SampleCount, 880.0, 0.9f), SampleRate, CancellationToken.None);

        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), _cacheDirectory);
        var channels = new[]
        {
            new StemChannelState(drums.Id, Muted: false, Soloed: false),
            new StemChannelState(vocals.Id, Muted: false, Soloed: false),
        };

        await Assert.ThrowsAsync<StemMixdownIncompatibleStemsException>(
            () => renderer.RenderMixAsync(manifest, channels, CancellationToken.None));
    }

    [Fact]
    public async Task RenderMixAsync_AlreadyCancelled_ThrowsOperationCanceledException_NoPartialFile_NegativePath()
    {
        var drums = await WriteStemAsync(StemKind.Drums, DeterministicSineWave(SampleCount, 110.0, 0.3f));
        var manifest = Manifest(drums);

        var renderer = new StemMixdownRenderer(new WavAudioDecoder(), _cacheDirectory);
        var channels = new[] { new StemChannelState(drums.Id, Muted: false, Soloed: false) };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => renderer.RenderMixAsync(manifest, channels, cts.Token));

        var cacheDir = Path.Combine(_cacheDirectory, ".mixdown-cache");
        Assert.False(Directory.Exists(cacheDir) && Directory.GetFiles(cacheDir).Length > 0,
            "no mixdown file must be written when the call is already cancelled");
    }

    [Fact]
    public void StemMixRules_MissingChannelDefaultsToUnmutedUnsoloed()
    {
        var drums = new StemAsset(Guid.NewGuid(), StemKind.Drums, "drums.wav", SampleRate, SampleCount, OriginTicks, "hash-a");
        var vocals = new StemAsset(Guid.NewGuid(), StemKind.Vocals, "vocals.wav", SampleRate, SampleCount, OriginTicks, "hash-b");
        var manifest = Manifest(drums, vocals);

        // Only drums has an explicit channel state; vocals has none at all.
        var active = StemMixRules.ResolveActiveStemIds(manifest, new[] { new StemChannelState(drums.Id, Muted: false, Soloed: false) });

        Assert.Equal(2, active.Count);
        Assert.Contains(drums.Id, active);
        Assert.Contains(vocals.Id, active);
    }
}
