namespace XlightsAI.Studio.Stems;

/// <summary>
/// Minimal, real (no third-party dependency) writer for mono IEEE-float32 PCM WAV files -- the exact
/// inverse container <see cref="XlightsAI.Studio.Audio.WavAudioDecoder"/> already reads (format tag 3,
/// 32-bit). Used to persist each produced <see cref="StemAsset"/> losslessly (no quantization), so a
/// regression comparison against a known ground truth is exact, not merely "close enough" because of
/// an intermediate PCM16 round-trip.
/// </summary>
public static class StemWavWriter
{
    private const short FormatTagIeeeFloat = 3;
    private const short BitsPerSample = 32;

    public static async Task WriteMonoFloatAsync(string path, ReadOnlyMemory<float> samples, int sampleRate, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");
        }

        var dataSize = samples.Length * sizeof(float);
        var byteRate = sampleRate * sizeof(float);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);

            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write(FormatTagIeeeFloat);
            writer.Write((short)1); // mono
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)sizeof(float)); // block align
            writer.Write(BitsPerSample);

            writer.Write("data"u8);
            writer.Write(dataSize);
        }

        var buffer = new byte[Math.Min(dataSize, 1 << 20)];
        var written = 0;
        while (written < samples.Length)
        {
            ct.ThrowIfCancellationRequested();

            var countThisBlock = Math.Min(buffer.Length / sizeof(float), samples.Length - written);
            var blockSpan = samples.Span.Slice(written, countThisBlock);
            for (var i = 0; i < countThisBlock; i++)
            {
                BitConverter.TryWriteBytes(buffer.AsSpan(i * sizeof(float), sizeof(float)), blockSpan[i]);
            }

            await stream.WriteAsync(buffer.AsMemory(0, countThisBlock * sizeof(float)), ct).ConfigureAwait(false);
            written += countThisBlock;
        }
    }

    /// <summary>
    /// PHASE-06A Step 044C. New, additive sibling to <see cref="WriteMonoFloatAsync"/> (never a retrofit
    /// of it -- the same "new sibling, no shared call sites" precedent this addendum's own Step 044A
    /// already established for <c>DeterministicSyntheticAudioGenerator.EncodeStereoWav</c>): a 2-channel,
    /// interleaved IEEE-float32 PCM WAV writer, required by <see cref="SpectrogramOnnxStemSeparator"/>'s
    /// real stereo stem output. <paramref name="left"/> and <paramref name="right"/> must be the same
    /// length.
    /// </summary>
    public static async Task WriteStereoFloatAsync(
        string path, ReadOnlyMemory<float> left, ReadOnlyMemory<float> right, int sampleRate, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");
        }

        if (left.Length != right.Length)
        {
            throw new ArgumentException(
                $"Left channel length {left.Length} does not match right channel length {right.Length}.", nameof(right));
        }

        const short channels = 2;
        var frameCount = left.Length;
        var dataSize = frameCount * channels * sizeof(float);
        var blockAlign = channels * sizeof(float);
        var byteRate = sampleRate * blockAlign;

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);

            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write(FormatTagIeeeFloat);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write(BitsPerSample);

            writer.Write("data"u8);
            writer.Write(dataSize);
        }

        var buffer = new byte[2 * sizeof(float)];
        for (var i = 0; i < frameCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            BitConverter.TryWriteBytes(buffer.AsSpan(0, sizeof(float)), left.Span[i]);
            BitConverter.TryWriteBytes(buffer.AsSpan(sizeof(float), sizeof(float)), right.Span[i]);
            await stream.WriteAsync(buffer, ct).ConfigureAwait(false);
        }
    }
}
