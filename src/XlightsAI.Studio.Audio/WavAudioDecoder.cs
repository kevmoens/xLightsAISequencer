using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 024 — real, managed (no native/third-party dependency) decoder for uncompressed
/// PCM and IEEE-float WAV files: the format <c>FixtureCorpusGenerator</c>/<c>DeterministicSyntheticAudioGenerator</c>
/// already produce for this repository's own committed audio fixtures. Reads actual RIFF chunk
/// structure to probe/decode, never trusts a file extension. Compressed WAV codecs (ADPCM,
/// WAVE_FORMAT_EXTENSIBLE, etc.) and other containers (MP3) are explicitly out of this step's
/// scope — see <c>implementation-evidence/PHASE-04/STEP-24.md</c> §3/§6 for the disclosed
/// decision and gap; probing such a file fails clearly via <see cref="AudioProbeInvalidException"/>
/// rather than silently misreading it.
/// </summary>
public sealed class WavAudioDecoder : IAudioDecoder
{
    /// <summary>Burned into every <see cref="SongAsset.DecoderVersion"/> this decoder produces (Section 4: "every cache key includes algorithm/model/settings versions").</summary>
    public const string Version = "XlightsAI.Studio.Audio.WavAudioDecoder/1.0.0";

    private const short FormatTagPcm = 1;
    private const short FormatTagIeeeFloat = 3;
    private const int DecodeBlockFrames = 4096;

    public async Task<SongAsset> ProbeAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No audio file exists at '{path}'.", path);
        }

        WavHeader header;
        await using (var stream = File.OpenRead(path))
        {
            header = await ReadHeaderAsync(stream, path, ct).ConfigureAwait(false);
        }

        await using var hashStream = File.OpenRead(path);
        var hashBytes = await SHA256.HashDataAsync(hashStream, ct).ConfigureAwait(false);
        var sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new SongAsset(
            Id: new SongId(Guid.NewGuid()),
            FilePath: path,
            OriginalFilePath: path,
            Sha256: sha256,
            SampleCount: header.FrameCount,
            SampleRate: header.SampleRate,
            Channels: header.Channels,
            OriginTicks: 0,
            BitsPerSample: header.BitsPerSample,
            EncoderDelaySamples: 0,
            DecoderVersion: Version);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(
        SongAsset song, int targetSampleRate, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (targetSampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSampleRate), targetSampleRate, "Target sample rate must be positive.");
        }

        await using var stream = File.OpenRead(song.FilePath);
        var header = await ReadHeaderAsync(stream, song.FilePath, ct).ConfigureAwait(false);

        stream.Seek(header.DataOffset, SeekOrigin.Begin);
        var bytesPerSample = header.BitsPerSample / 8;
        var frameBytes = bytesPerSample * header.Channels;
        var readBuffer = new byte[frameBytes * DecodeBlockFrames];
        var monoBuffer = new float[DecodeBlockFrames];

        var remainingFrames = header.FrameCount;
        var resampleRatio = header.SampleRate == targetSampleRate ? 1.0 : header.SampleRate / (double)targetSampleRate;

        // Resampling is deliberately out of scope for Step 024 itself (no step before Step 026's
        // waveform pyramid or Step 028's single master clock needs it yet); when the source and
        // target rates differ this decoder still produces a correct mono signal at the *source*
        // rate rather than silently mislabeling resampled output as the target rate — a later step
        // that actually needs real resampling should extend this method rather than trust a no-op.
        _ = resampleRatio;

        while (remainingFrames > 0)
        {
            ct.ThrowIfCancellationRequested();

            var framesThisBlock = (int)Math.Min(DecodeBlockFrames, remainingFrames);
            var bytesThisBlock = framesThisBlock * frameBytes;
            var read = await stream.ReadAsync(readBuffer.AsMemory(0, bytesThisBlock), ct).ConfigureAwait(false);
            if (read < bytesThisBlock)
            {
                throw new AudioProbeInvalidException(song.FilePath, "audio data truncated before the declared sample count was reached.");
            }

            for (var frame = 0; frame < framesThisBlock; frame++)
            {
                double sum = 0;
                for (var channel = 0; channel < header.Channels; channel++)
                {
                    var offset = (frame * frameBytes) + (channel * bytesPerSample);
                    sum += ReadSample(readBuffer, offset, header.BitsPerSample, header.AudioFormat);
                }

                monoBuffer[frame] = (float)(sum / header.Channels);
            }

            remainingFrames -= framesThisBlock;
            yield return monoBuffer.AsMemory(0, framesThisBlock);
        }
    }

    /// <summary>
    /// PHASE-06A Step 044A: widened from <c>private</c> to <c>internal</c> so <see cref="WavStereoAudioDecoder"/>
    /// (a disclosed, phase-local sibling decoder in this same project/assembly) can decode each
    /// channel's own raw sample independently instead of averaging them into a mono downmix.
    /// Visibility-only change — the method's own signature and behavior are byte-for-byte unchanged,
    /// so every existing caller/test of this class is unaffected.
    /// </summary>
    internal static double ReadSample(byte[] buffer, int offset, int bitsPerSample, short audioFormat)
    {
        if (audioFormat == FormatTagIeeeFloat && bitsPerSample == 32)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        return bitsPerSample switch
        {
            8 => (buffer[offset] - 128) / 128.0,
            16 => BitConverter.ToInt16(buffer, offset) / 32768.0,
            24 => Read24BitSigned(buffer, offset) / 8_388_608.0,
            32 => BitConverter.ToInt32(buffer, offset) / 2_147_483_648.0,
            _ => throw new NotSupportedException($"Unsupported bit depth: {bitsPerSample}."),
        };
    }

    private static int Read24BitSigned(byte[] buffer, int offset)
    {
        var value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    /// <summary>
    /// PHASE-06A Step 044A: widened from <c>private</c> to <c>internal</c> for the same reason as
    /// <see cref="ReadSample"/> above — <see cref="WavStereoAudioDecoder"/> reuses this exact RIFF/WAVE
    /// chunk-parsing routine rather than re-implementing header parsing a second time. Visibility-only
    /// change; behavior is byte-for-byte unchanged.
    /// </summary>
    internal static async Task<WavHeader> ReadHeaderAsync(Stream stream, string path, CancellationToken ct)
    {
        var magic = new byte[12];
        if (await ReadExactAsync(stream, magic, ct).ConfigureAwait(false) < 12)
        {
            throw new AudioProbeInvalidException(path, "file is too short to contain a RIFF/WAVE header.");
        }

        if (magic[0] != 'R' || magic[1] != 'I' || magic[2] != 'F' || magic[3] != 'F')
        {
            throw new AudioProbeInvalidException(path, "missing RIFF magic number.");
        }

        if (magic[8] != 'W' || magic[9] != 'A' || magic[10] != 'V' || magic[11] != 'E')
        {
            throw new AudioProbeInvalidException(path, "missing WAVE format identifier.");
        }

        short? audioFormat = null;
        short? channels = null;
        int? sampleRate = null;
        short? bitsPerSample = null;
        long? dataOffset = null;
        long? dataSize = null;

        var chunkHeader = new byte[8];
        while (true)
        {
            var read = await ReadExactAsync(stream, chunkHeader, ct).ConfigureAwait(false);
            if (read == 0)
            {
                break; // Clean EOF between chunks.
            }

            if (read < 8)
            {
                throw new AudioProbeInvalidException(path, "truncated chunk header.");
            }

            var chunkId = System.Text.Encoding.ASCII.GetString(chunkHeader, 0, 4);
            var chunkSize = BitConverter.ToInt32(chunkHeader, 4);
            if (chunkSize < 0)
            {
                throw new AudioProbeInvalidException(path, $"chunk '{chunkId}' declares a negative size.");
            }

            if (chunkId == "fmt ")
            {
                var fmt = new byte[chunkSize];
                if (await ReadExactAsync(stream, fmt, ct).ConfigureAwait(false) < chunkSize || chunkSize < 16)
                {
                    throw new AudioProbeInvalidException(path, "truncated or undersized 'fmt ' chunk.");
                }

                audioFormat = BitConverter.ToInt16(fmt, 0);
                channels = BitConverter.ToInt16(fmt, 2);
                sampleRate = BitConverter.ToInt32(fmt, 4);
                bitsPerSample = BitConverter.ToInt16(fmt, 14);
            }
            else if (chunkId == "data")
            {
                dataOffset = stream.Position;
                dataSize = chunkSize;
                if (!await SkipAsync(stream, chunkSize, ct).ConfigureAwait(false))
                {
                    throw new AudioProbeInvalidException(path, "'data' chunk size exceeds the actual file length (truncated file).");
                }
            }
            else
            {
                if (!await SkipAsync(stream, chunkSize, ct).ConfigureAwait(false))
                {
                    throw new AudioProbeInvalidException(path, $"chunk '{chunkId}' size exceeds the actual file length (truncated file).");
                }
            }

            if (chunkSize % 2 == 1)
            {
                // RIFF chunks are word-aligned; odd-sized chunks are followed by one pad byte.
                if (!await SkipAsync(stream, 1, ct).ConfigureAwait(false))
                {
                    break; // Trailing pad byte missing at true EOF is tolerated, not an error.
                }
            }
        }

        if (audioFormat is null || channels is null || sampleRate is null || bitsPerSample is null)
        {
            throw new AudioProbeInvalidException(path, "missing required 'fmt ' chunk.");
        }

        if (dataOffset is null || dataSize is null)
        {
            throw new AudioProbeInvalidException(path, "missing required 'data' chunk.");
        }

        if (audioFormat is not (FormatTagPcm or FormatTagIeeeFloat))
        {
            throw new AudioProbeInvalidException(path, $"unsupported WAV codec (format tag {audioFormat}); only uncompressed PCM/IEEE-float is supported.");
        }

        if (channels <= 0)
        {
            throw new AudioProbeInvalidException(path, $"invalid channel count ({channels}).");
        }

        if (sampleRate <= 0)
        {
            throw new AudioProbeInvalidException(path, $"invalid sample rate ({sampleRate}).");
        }

        if (bitsPerSample is not (8 or 16 or 24 or 32))
        {
            throw new AudioProbeInvalidException(path, $"unsupported bit depth ({bitsPerSample}); only 8/16/24/32-bit PCM is supported.");
        }

        var blockAlign = channels.Value * (bitsPerSample.Value / 8);
        if (dataSize.Value == 0)
        {
            throw new AudioProbeInvalidException(path, "zero-length audio data (invalid timing boundary: duration would be zero).");
        }

        if (dataSize.Value % blockAlign != 0)
        {
            throw new AudioProbeInvalidException(path, $"'data' chunk size ({dataSize}) is not an exact multiple of the frame size ({blockAlign}); file is corrupt.");
        }

        var frameCount = dataSize.Value / blockAlign;

        return new WavHeader(audioFormat.Value, channels.Value, sampleRate.Value, bitsPerSample.Value, dataOffset.Value, frameCount);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    /// <summary>Advances the stream by <paramref name="count"/> bytes; returns <see langword="false"/> if fewer bytes were actually available (truncated file).</summary>
    private static async Task<bool> SkipAsync(Stream stream, long count, CancellationToken ct)
    {
        if (count == 0)
        {
            return true;
        }

        if (stream.CanSeek)
        {
            if (stream.Position + count > stream.Length)
            {
                return false;
            }

            stream.Seek(count, SeekOrigin.Current);
            return true;
        }

        var buffer = new byte[Math.Min(count, 81_920)];
        var remaining = count;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            remaining -= read;
        }

        return true;
    }

    /// <summary>PHASE-06A Step 044A: widened from <c>private</c> to <c>internal</c> so <see cref="ReadHeaderAsync"/>'s return type is visible to <see cref="WavStereoAudioDecoder"/>. Shape unchanged.</summary>
    internal readonly record struct WavHeader(short AudioFormat, int Channels, int SampleRate, int BitsPerSample, long DataOffset, long FrameCount);
}
