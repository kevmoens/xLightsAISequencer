using System.Runtime.CompilerServices;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-06A Step 044A — real, managed stereo decoder for the same uncompressed PCM/IEEE-float WAV
/// files <see cref="WavAudioDecoder"/> already supports. Reuses <see cref="WavAudioDecoder.ReadHeaderAsync"/>/
/// <see cref="WavAudioDecoder.ReadSample"/> (widened to <c>internal</c> for exactly this reuse, §3 of
/// the addendum spec) rather than re-implementing RIFF/WAVE chunk parsing a second time — only the
/// sample-deinterleaving step differs: per-channel output instead of <see cref="WavAudioDecoder.DecodeMonoAsync"/>'s
/// averaged-mono downmix.
/// </summary>
public sealed class WavStereoAudioDecoder : IStereoAudioDecoder
{
    private const int DecodeBlockFrames = 4096;
    private const int RequiredChannels = 2;

    public async IAsyncEnumerable<StereoSampleBlock> DecodeStereoAsync(
        SongAsset song, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        if (song.Channels != RequiredChannels)
        {
            throw new StereoAudioDecodeChannelMismatchException(song.FilePath, song.Channels);
        }

        await using var stream = File.OpenRead(song.FilePath);
        var header = await WavAudioDecoder.ReadHeaderAsync(stream, song.FilePath, ct).ConfigureAwait(false);

        if (header.Channels != RequiredChannels)
        {
            // Defensive: the SongAsset's own Channels should already match the file it was probed
            // from, but this is the one place a stale/mismatched SongAsset (e.g. a file replaced on
            // disk after probing) would otherwise silently misread channel-interleaved bytes.
            throw new StereoAudioDecodeChannelMismatchException(song.FilePath, header.Channels);
        }

        stream.Seek(header.DataOffset, SeekOrigin.Begin);
        var bytesPerSample = header.BitsPerSample / 8;
        var frameBytes = bytesPerSample * header.Channels;
        var readBuffer = new byte[frameBytes * DecodeBlockFrames];
        var leftBuffer = new float[DecodeBlockFrames];
        var rightBuffer = new float[DecodeBlockFrames];

        var remainingFrames = header.FrameCount;
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
                var leftOffset = frame * frameBytes;
                var rightOffset = leftOffset + bytesPerSample;
                leftBuffer[frame] = (float)WavAudioDecoder.ReadSample(readBuffer, leftOffset, header.BitsPerSample, header.AudioFormat);
                rightBuffer[frame] = (float)WavAudioDecoder.ReadSample(readBuffer, rightOffset, header.BitsPerSample, header.AudioFormat);
            }

            remainingFrames -= framesThisBlock;
            yield return new StereoSampleBlock(leftBuffer.AsMemory(0, framesThisBlock), rightBuffer.AsMemory(0, framesThisBlock));
        }
    }
}
