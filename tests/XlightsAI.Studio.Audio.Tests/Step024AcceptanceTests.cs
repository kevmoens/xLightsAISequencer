using System.Security.Cryptography;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Audio.Tests;

/// <summary>
/// PHASE-04 / Step 024 — "Import supported local audio and compute canonical audio metadata and
/// hash." Required acceptance: "Invalid/corrupt files fail clearly; duration/sample-rate verified."
/// Failure/edge case: "Silence, invalid timing boundaries and missing media must be handled
/// explicitly." Implementation guidance: "Validate extension and decoder support by probing
/// contents, not only filename; record decoder version, duration and original audio origin/priming."
/// </summary>
public sealed class Step024AcceptanceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step024-{Guid.NewGuid():N}");

    public Step024AcceptanceTests()
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

    [Fact]
    public async Task Step024_Acceptance_Passes_ProbesComputesHashAndPersistsProvenance()
    {
        // Real, deterministic, fully synthetic click-track WAV (this repository's own licensed
        // fixture generator, PHASE-01 Step 003) — 120 BPM, 8 clicks, 8000 Hz mono PCM16.
        const int sampleRate = 8000;
        const int clickCount = 8;
        const int bpm = 120;
        var samples = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate);

        var path = Path.Combine(_tempDir, "click-track.wav");
        await File.WriteAllBytesAsync(path, wavBytes);

        var decoder = new WavAudioDecoder();
        var asset = await decoder.ProbeAsync(path, CancellationToken.None);

        // Duration/sample-rate verified against the independently known recipe parameters.
        Assert.Equal(sampleRate, asset.SampleRate);
        Assert.Equal(1, asset.Channels);
        Assert.Equal(16, asset.BitsPerSample);
        Assert.Equal(samples.Length, asset.SampleCount);
        var expectedDurationSeconds = clickCount * 60.0 / bpm;
        Assert.Equal(expectedDurationSeconds, asset.DurationTicks / 10_000_000.0, precision: 3);

        // Decoder version recorded (Section 4 cache-key rule) and priming disclosed as 0 for this
        // plain-PCM container (no codec delay).
        Assert.Equal(WavAudioDecoder.Version, asset.DecoderVersion);
        Assert.Equal(0, asset.EncoderDelaySamples);
        Assert.Equal(0, asset.OriginTicks);

        // Hash matches the real on-disk bytes — the content-identity half of "hash-addressed".
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(wavBytes)).ToLowerInvariant();
        Assert.Equal(expectedSha256, asset.Sha256);

        // Persisted audio/analysis artifact with timestamp provenance: saved and reloaded verbatim.
        var store = new JsonSongAssetStore(Path.Combine(_tempDir, "song-assets"));
        var savedPath = await store.SaveAsync(asset, CancellationToken.None);
        Assert.True(File.Exists(savedPath));

        var reloaded = await store.LoadAsync(savedPath, CancellationToken.None);
        Assert.Equal(asset, reloaded);

        // Decoded PCM matches the original synthesized signal within 16-bit quantization tolerance
        // (public output: real decoded samples, not just a probed header).
        var decodedFrames = new List<float>();
        await foreach (var block in decoder.DecodeMonoAsync(asset, sampleRate, CancellationToken.None))
        {
            decodedFrames.AddRange(block.ToArray());
        }

        Assert.Equal(samples.Length, decodedFrames.Count);
        for (var i = 0; i < samples.Length; i += 97) // sampled stride, not every frame, keeps the assertion fast
        {
            Assert.Equal(samples[i], decodedFrames[i], precision: 3);
        }
    }

    [Fact]
    public async Task ProbeAsync_SilentAudio_SucceedsAsAnExplicitlyHandledEdgeCase()
    {
        // "Silence... must be handled explicitly" — a valid, non-zero-duration file whose samples
        // are all zero must probe successfully, not be mistaken for a corrupt/empty file.
        var silentSamples = new float[8000 * 2]; // 2 seconds of true digital silence at 8 kHz.
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(silentSamples, sampleRate: 8000);
        var path = Path.Combine(_tempDir, "silence.wav");
        await File.WriteAllBytesAsync(path, wavBytes);

        var asset = await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);

        Assert.Equal(silentSamples.Length, asset.SampleCount);
        Assert.Equal(8000, asset.SampleRate);
        Assert.True(asset.DurationTicks > 0);
    }

    [Fact]
    public async Task ProbeAsync_MissingMediaFile_ThrowsFileNotFoundException_NegativePath()
    {
        var missingPath = Path.Combine(_tempDir, "does-not-exist.wav");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => new WavAudioDecoder().ProbeAsync(missingPath, CancellationToken.None));
    }

    [Fact]
    public async Task ProbeAsync_NotARiffFile_FailsClearly_NegativePath()
    {
        var path = Path.Combine(_tempDir, "not-audio.wav");
        await File.WriteAllBytesAsync(path, "This is not an audio file at all, just plain text bytes."u8.ToArray());

        var ex = await Assert.ThrowsAsync<AudioProbeInvalidException>(
            () => new WavAudioDecoder().ProbeAsync(path, CancellationToken.None));

        Assert.Contains("RIFF", ex.Reason);
    }

    [Fact]
    public async Task ProbeAsync_ZeroLengthDataChunk_FailsAsInvalidTimingBoundary_NegativePath()
    {
        // "invalid timing boundaries... must be handled explicitly" — a well-formed container with
        // a zero-byte data chunk (zero duration) is rejected, distinct from true silence above.
        var path = Path.Combine(_tempDir, "zero-duration.wav");
        await File.WriteAllBytesAsync(path, BuildWavBytes(dataBytes: [], sampleRate: 8000, channels: 1, bitsPerSample: 16));

        var ex = await Assert.ThrowsAsync<AudioProbeInvalidException>(
            () => new WavAudioDecoder().ProbeAsync(path, CancellationToken.None));

        Assert.Contains("zero-length", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_DeclaredDataSizeExceedsActualFileLength_FailsAsTruncated_NegativePath()
    {
        var path = Path.Combine(_tempDir, "truncated.wav");
        var bytes = BuildWavBytes(dataBytes: new byte[16], sampleRate: 8000, channels: 1, bitsPerSample: 16);
        // Corrupt the 'data' chunk's declared size field in place: fixed offsets for this
        // hand-built file are RIFF(12) + fmt chunk header(8) + fmt body(16) + "data"(4) == the
        // 4-byte size field starts at byte 40, with the real payload starting at byte 44.
        var corrupted = bytes[..40].Concat(BitConverter.GetBytes(10_000)).Concat(bytes[44..]).ToArray();
        await File.WriteAllBytesAsync(path, corrupted);

        var ex = await Assert.ThrowsAsync<AudioProbeInvalidException>(
            () => new WavAudioDecoder().ProbeAsync(path, CancellationToken.None));

        Assert.Contains("truncated", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_UnsupportedCompressedCodec_FailsClearly_NegativePath()
    {
        var path = Path.Combine(_tempDir, "compressed.wav");
        await File.WriteAllBytesAsync(path, BuildWavBytes(dataBytes: new byte[16], sampleRate: 8000, channels: 1, bitsPerSample: 16, formatTag: 6 /* A-law, unsupported */));

        var ex = await Assert.ThrowsAsync<AudioProbeInvalidException>(
            () => new WavAudioDecoder().ProbeAsync(path, CancellationToken.None));

        Assert.Contains("unsupported", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_DataSizeNotMultipleOfFrameSize_FailsClearly_NegativePath()
    {
        var path = Path.Combine(_tempDir, "misaligned.wav");
        // 1 channel, 16-bit => 2-byte frames; 3 bytes of data can never contain a whole number of frames.
        await File.WriteAllBytesAsync(path, BuildWavBytes(dataBytes: new byte[3], sampleRate: 8000, channels: 1, bitsPerSample: 16));

        var ex = await Assert.ThrowsAsync<AudioProbeInvalidException>(
            () => new WavAudioDecoder().ProbeAsync(path, CancellationToken.None));

        Assert.Contains("multiple", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_NeverMutatesOrMovesTheSourceFile()
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.5, sampleRate: 8000);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, 8000);
        var path = Path.Combine(_tempDir, "note.wav");
        await File.WriteAllBytesAsync(path, wavBytes);
        var hashBefore = SHA256.HashData(await File.ReadAllBytesAsync(path));

        await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(hashBefore, SHA256.HashData(await File.ReadAllBytesAsync(path)));
    }

    /// <summary>Hand-builds a minimal, deliberately malformable 16-bit PCM WAV byte layout for the negative-path tests above.</summary>
    private static byte[] BuildWavBytes(byte[] dataBytes, int sampleRate, short channels, short bitsPerSample, short formatTag = 1)
    {
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataBytes.Length);
            writer.Write("WAVE"u8.ToArray());

            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write(formatTag);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            writer.Write("data"u8.ToArray());
            writer.Write(dataBytes.Length);
            writer.Write(dataBytes);
        }

        return stream.ToArray();
    }
}
