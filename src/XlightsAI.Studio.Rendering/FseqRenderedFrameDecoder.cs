using System.Text;
using ZstdSharp;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Default <see cref="IRenderedFrameDecoder"/>. Decodes the Falcon Player/xLights FSEQ v2 binary format:
/// a fixed 32-byte header, a compression-block index, then zstd-compressed channel-data blocks.
///
/// <para>
/// <b>Grounding (Section 1's "version-aware... not a universal assumption" discipline applied to a file
/// format instead of a CLI flag).</b> This repository's only pinned, committed reference to the FSEQ v2
/// byte layout is <c>reference/xlights-autosequencer/src/video/fseq.py</c> (the pinned upstream
/// xLightsAI's own Python FSEQ v2 reader) -- there is no FSEQ format appendix in this phase spec itself.
/// Every field offset below (<c>PSEQ</c> magic @0, data offset @4, channel count @10, frame count @14,
/// step time @18, fixed 32-byte header, 8-byte block-index entries of <c>(frameIndex, blockByteLength)</c>)
/// is taken from that file, not invented. That same reference hardcodes zstd as the only compression
/// scheme it decompresses (it never reads a compression-type byte) -- this decoder does the same, and
/// does not attempt to support FSEQ v2's publicly documented but, in our own pinned reference, unused
/// "uncompressed" or "zlib" compression types; see remarks on <see cref="Decode"/> for the explicit risk
/// this carries.
/// </para>
/// </summary>
public sealed class FseqRenderedFrameDecoder : IRenderedFrameDecoder
{
    private const string Magic = "PSEQ";
    private const int FixedHeaderSize = 32;
    private const int BlockIndexEntrySize = 8;

    /// <summary>
    /// Decodes <paramref name="fseqPath"/> into real per-frame channel bytes with timing preserved.
    ///
    /// <para>
    /// <b>Explicitly out of this step's scope: channel-to-model mapping.</b> Step 087's own phase-spec
    /// text reads "into display preview," but no prior phase (PHASE-02's own <c>DisplayModel</c>, PHASE-09's
    /// <c>DisplayNode</c>) carries a channel offset / node-count field -- that data lives in
    /// <c>xlights_networks.xml</c>'s own controller/output configuration, which no step through PHASE-13
    /// Step 086 imports. Returning a channel-indexed <see cref="DecodedRenderOutput"/> rather than a
    /// model-indexed <c>Rendering.PreviewFrame</c> avoids inventing a channel-to-model mapping Section 2
    /// forbids guessing at ("do not invent xLights effect parameters," applied here to channel layout);
    /// a future step that imports real channel-offset metadata can build a thin converter on top of this
    /// decoder without changing it.
    /// </para>
    /// </summary>
    public DecodedRenderOutput Decode(string fseqPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fseqPath);

        if (!File.Exists(fseqPath))
        {
            throw new FseqDecodeInvalidException($"No rendered .fseq file exists at '{fseqPath}'.");
        }

        var raw = File.ReadAllBytes(fseqPath);
        if (raw.Length < FixedHeaderSize)
        {
            throw new FseqDecodeInvalidException(
                $"'{fseqPath}' is only {raw.Length} byte(s) long -- too short to contain an FSEQ v2 header.");
        }

        if (Encoding.ASCII.GetString(raw, 0, 4) != Magic)
        {
            throw new FseqDecodeInvalidException(
                $"'{fseqPath}' does not start with the FSEQ v2 'PSEQ' magic bytes -- not a rendered sequence file.");
        }

        var dataOffset = BitConverter.ToUInt16(raw, 4);
        var channelCount = checked((int)BitConverter.ToUInt32(raw, 10));
        var frameCount = checked((int)BitConverter.ToUInt32(raw, 14));
        var stepMs = raw[18];

        if (channelCount <= 0 || frameCount <= 0 || stepMs == 0)
        {
            throw new FseqDecodeInvalidException(
                $"'{fseqPath}' declares an invalid channel count ({channelCount}), frame count ({frameCount}) " +
                $"or step time ({stepMs}ms); none may be zero or negative.");
        }

        if (dataOffset > raw.Length)
        {
            throw new FseqDecodeInvalidException(
                $"'{fseqPath}' declares a data offset ({dataOffset}) beyond the file's own length ({raw.Length} bytes).");
        }

        var blocks = new List<(int FrameIndex, int ByteLength)>();
        var indexCursor = FixedHeaderSize;
        while (indexCursor + BlockIndexEntrySize <= dataOffset)
        {
            var frameIndex = checked((int)BitConverter.ToUInt32(raw, indexCursor));
            var blockLength = checked((int)BitConverter.ToUInt32(raw, indexCursor + 4));
            if (blockLength == 0 || frameIndex > frameCount)
            {
                break;
            }

            blocks.Add((frameIndex, blockLength));
            indexCursor += BlockIndexEntrySize;
        }

        var channelsByFrame = new byte[frameCount][];
        for (var i = 0; i < frameCount; i++)
        {
            channelsByFrame[i] = new byte[channelCount];
        }

        using var decompressor = new Decompressor();
        var readCursor = (int)dataOffset;
        foreach (var (frameIndex, blockLength) in blocks)
        {
            if (readCursor + blockLength > raw.Length)
            {
                throw new FseqDecodeInvalidException(
                    $"'{fseqPath}' declares a compressed block (starting at frame {frameIndex}, {blockLength} " +
                    $"byte(s)) that extends past the end of the file.");
            }

            byte[] decompressed;
            try
            {
                decompressed = decompressor.Unwrap(raw.AsSpan(readCursor, blockLength)).ToArray();
            }
            catch (Exception ex) when (ex is not FseqDecodeInvalidException)
            {
                throw new FseqDecodeInvalidException(
                    $"'{fseqPath}' contains a block (starting at frame {frameIndex}) that failed zstd decompression.", ex);
            }

            readCursor += blockLength;

            var framesInBlock = decompressed.Length / channelCount;
            for (var i = 0; i < framesInBlock; i++)
            {
                var targetFrame = frameIndex + i;
                if (targetFrame >= frameCount)
                {
                    break;
                }

                Array.Copy(decompressed, i * channelCount, channelsByFrame[targetFrame], 0, channelCount);
            }
        }

        var frames = new DecodedRenderFrame[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = new DecodedRenderFrame((long)i * stepMs * TimeSpan.TicksPerMillisecond, channelsByFrame[i]);
        }

        return new DecodedRenderOutput(channelCount, stepMs, frames);
    }
}
