using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Audio.Tests;

/// <summary>
/// PHASE-06A Step 044A — "Add real stereo audio decode support." Required acceptance (verbatim): "A
/// real stereo WAV fixture (left channel = one known frequency tone, right channel = a different
/// known frequency tone) is decoded via IStereoAudioDecoder; each decoded channel's own
/// independently-measured frequency... matches its own known tone within tolerance... proving
/// channels are read independently, never swapped or downmixed." Failure/edge case (verbatim): "A
/// mono (1-channel) or &gt;2-channel source passed to DecodeStereoAsync throws
/// StereoAudioDecodeChannelMismatchException before any sample data is read; a truncated/corrupt
/// stereo file is rejected the same way WavAudioDecoder.ProbeAsync already rejects a truncated mono
/// file (reused validation, not reinvented)."
/// </summary>
public sealed class Step044AAcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step044a-{Guid.NewGuid():N}");

    public Step044AAcceptanceTests() => Directory.CreateDirectory(_rootDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    /// <summary>Zero-crossing-rate frequency estimate -- simple and robust enough for a pure synthetic tone, independent of any STFT/FFT machinery this addendum also introduces elsewhere.</summary>
    private static double EstimateFrequencyHz(float[] samples, int sampleRate)
    {
        var crossings = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            if (samples[i - 1] < 0 && samples[i] >= 0)
            {
                crossings++;
            }
        }

        var durationSeconds = samples.Length / (double)sampleRate;
        return crossings / durationSeconds;
    }

    [Fact]
    public async Task Step044A_Acceptance_Passes_ChannelsDecodeIndependentlyAtTheirOwnKnownFrequency()
    {
        var left = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.25, SampleRate);
        var right = DeterministicSyntheticAudioGenerator.BuildNoteSequence([880.0], noteDurationSeconds: 0.25, SampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeStereoWav(left, right, SampleRate);

        var path = Path.Combine(_rootDirectory, "stereo-440-880.wav");
        await File.WriteAllBytesAsync(path, wavBytes);

        var monoDecoder = new WavAudioDecoder();
        var asset = await monoDecoder.ProbeAsync(path, CancellationToken.None);
        Assert.Equal(2, asset.Channels);

        var stereoDecoder = new WavStereoAudioDecoder();
        var decodedLeft = new List<float>();
        var decodedRight = new List<float>();
        await foreach (var block in stereoDecoder.DecodeStereoAsync(asset, CancellationToken.None))
        {
            decodedLeft.AddRange(block.Left.Span.ToArray());
            decodedRight.AddRange(block.Right.Span.ToArray());
        }

        Assert.Equal(left.Length, decodedLeft.Count);
        Assert.Equal(right.Length, decodedRight.Count);

        var leftFreq = EstimateFrequencyHz(decodedLeft.ToArray(), SampleRate);
        var rightFreq = EstimateFrequencyHz(decodedRight.ToArray(), SampleRate);

        // 16-bit PCM quantization + zero-crossing estimation tolerance -- generous but far tighter
        // than the 440 Hz gap between the two known tones, so a channel swap/downmix would fail this
        // assertion unambiguously.
        Assert.InRange(leftFreq, 420.0, 460.0);
        Assert.InRange(rightFreq, 850.0, 910.0);
    }

    [Fact]
    public async Task DecodeStereoAsync_MonoSource_ThrowsStereoAudioDecodeChannelMismatchException_BeforeAnySampleRead_NegativePath()
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.1, SampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, SampleRate);
        var path = Path.Combine(_rootDirectory, "mono.wav");
        await File.WriteAllBytesAsync(path, wavBytes);

        var monoDecoder = new WavAudioDecoder();
        var asset = await monoDecoder.ProbeAsync(path, CancellationToken.None);
        Assert.Equal(1, asset.Channels);

        var stereoDecoder = new WavStereoAudioDecoder();
        var ex = await Assert.ThrowsAsync<StereoAudioDecodeChannelMismatchException>(async () =>
        {
            await foreach (var _ in stereoDecoder.DecodeStereoAsync(asset, CancellationToken.None))
            {
            }
        });

        Assert.Equal(1, ex.ActualChannels);
    }

    [Fact]
    public async Task ProbeAsync_TruncatedStereoFile_ThrowsAudioProbeInvalidException_ReusedValidationNegativePath()
    {
        var left = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.25, SampleRate);
        var right = DeterministicSyntheticAudioGenerator.BuildNoteSequence([880.0], noteDurationSeconds: 0.25, SampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeStereoWav(left, right, SampleRate);

        // Truncate the file body (keep the 44-byte header intact, chop off most of the declared
        // "data" chunk) -- the same shape of corruption WavAudioDecoder.ProbeAsync already rejects
        // for a mono file, proven here against a stereo one instead of reinventing a new check.
        var truncated = wavBytes[..(wavBytes.Length / 2)];
        var path = Path.Combine(_rootDirectory, "truncated-stereo.wav");
        await File.WriteAllBytesAsync(path, truncated);

        var decoder = new WavAudioDecoder();
        await Assert.ThrowsAsync<AudioProbeInvalidException>(() => decoder.ProbeAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task DecodeStereoAsync_ThreeChannelSource_ThrowsStereoAudioDecodeChannelMismatchException_NegativePath()
    {
        // Three-channel WAV built by hand (no generator helper exists for >2 channels, nor should
        // one -- this is a deliberately malformed-for-this-decoder input, not a real fixture shape
        // this repository otherwise needs).
        const int channels = 3;
        const short bitsPerSample = 16;
        var frameCount = 100;
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var dataSize = frameCount * blockAlign;

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * channels * (bitsPerSample / 8));
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write("data"u8.ToArray());
            writer.Write(dataSize);
            for (var i = 0; i < frameCount * channels; i++)
            {
                writer.Write((short)0);
            }
        }

        var path = Path.Combine(_rootDirectory, "three-channel.wav");
        await File.WriteAllBytesAsync(path, stream.ToArray());

        var monoDecoder = new WavAudioDecoder();
        var asset = await monoDecoder.ProbeAsync(path, CancellationToken.None);
        Assert.Equal(3, asset.Channels);

        var stereoDecoder = new WavStereoAudioDecoder();
        var ex = await Assert.ThrowsAsync<StereoAudioDecodeChannelMismatchException>(async () =>
        {
            await foreach (var _ in stereoDecoder.DecodeStereoAsync(asset, CancellationToken.None))
            {
            }
        });

        Assert.Equal(3, ex.ActualChannels);
    }
}
