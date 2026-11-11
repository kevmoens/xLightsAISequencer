namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Build-plan Phase 18 §4 (Phase C): "encode the rasterized frame sequence into a real video container...
/// or animated GIF per effect/tier -- new, disclosed third-party dependency choice to make explicitly here."
/// After presenting the real tradeoff (hand-rolled encoder vs. a managed imaging package vs. shelling out to a
/// real ffmpeg binary) the user chose the zero-new-dependency option: extend the same hand-rolled,
/// dependency-free approach <see cref="MinimalPngEncoder"/> already uses for stills. A hand-rolled GIF89a
/// encoder (LZW compression per Appendix F of the GIF89a spec, a global colour table, a Netscape2.0 looping
/// extension, and a per-frame Graphic Control Extension carrying a real, measured delay) is entirely tractable
/// here because every fixture frame is a flat-colour point-plot on a black background -- never a photographic
/// gradient -- so the distinct-colour count per sequence stays small in practice, and the 256-colour-palette
/// fallback quantizer below exists only to make a correctness guarantee, not because it is expected to engage.
/// </summary>
public static class AnimatedGifEncoder
{
    /// <param name="Rgb">Row-major, 3 bytes/pixel, no padding -- the same raw buffer shape <see cref="PreviewFrameRasterizer.Rasterize"/> already returns.</param>
    /// <param name="DelayCentiseconds">How long a viewer holds this frame before advancing, in GIF's native 1/100s unit. Clamped to at least 1 by <see cref="Encode"/> -- GIF treats 0 as "as fast as possible," which most viewers special-case unpredictably.</param>
    public sealed record Frame(byte[] Rgb, int DelayCentiseconds);

    private const byte TrailerByte = 0x3B;
    private const int MaxPaletteColors = 256;

    /// <summary>
    /// Encodes <paramref name="frames"/> (all the same <paramref name="width"/>x<paramref name="height"/>) as a
    /// real, playable, looping GIF89a animation.
    /// </summary>
    public static byte[] Encode(int width, int height, IReadOnlyList<Frame> frames)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
        }

        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required to encode a GIF.", nameof(frames));
        }

        var expectedLength = width * height * 3;
        foreach (var frame in frames)
        {
            if (frame.Rgb.Length != expectedLength)
            {
                throw new ArgumentException(
                    $"Expected {expectedLength} RGB bytes for a {width}x{height} frame, got {frame.Rgb.Length}.", nameof(frames));
            }
        }

        var palette = GifColorQuantizer.BuildPalette(frames, MaxPaletteColors);

        using var output = new MemoryStream();
        WriteHeaderAndLogicalScreenDescriptor(output, width, height, palette);
        WriteNetscapeLoopingExtension(output);

        foreach (var frame in frames)
        {
            var indices = palette.ToIndices(frame.Rgb);
            WriteGraphicControlExtension(output, Math.Max(1, frame.DelayCentiseconds));
            WriteImageDescriptorAndData(output, width, height, indices, palette.BitsPerPixel);
        }

        output.WriteByte(TrailerByte);
        return output.ToArray();
    }

    private static void WriteHeaderAndLogicalScreenDescriptor(Stream output, int width, int height, GifPalette palette)
    {
        output.Write("GIF89a"u8);

        WriteUInt16LittleEndian(output, (ushort)width);
        WriteUInt16LittleEndian(output, (ushort)height);

        // Global colour table flag (1) | colour resolution (3, unused here -- set to bitsPerPixel-1) |
        // sort flag (0) | size of global colour table (3, encodes 2^(N+1) entries).
        var bitsPerPixel = palette.BitsPerPixel;
        byte packed = 0b1000_0000;
        packed |= (byte)((bitsPerPixel - 1) << 4);
        packed |= (byte)(bitsPerPixel - 1);
        output.WriteByte(packed);

        output.WriteByte(0); // background colour index
        output.WriteByte(0); // pixel aspect ratio (unused/square)

        var paletteCapacity = 1 << bitsPerPixel;
        for (var i = 0; i < paletteCapacity; i++)
        {
            if (i < palette.Colors.Count)
            {
                output.WriteByte(palette.Colors[i].R);
                output.WriteByte(palette.Colors[i].G);
                output.WriteByte(palette.Colors[i].B);
            }
            else
            {
                output.WriteByte(0);
                output.WriteByte(0);
                output.WriteByte(0);
            }
        }
    }

    /// <summary>Application Extension (Netscape2.0) with a loop count of 0 -- "loop forever," the de facto standard every GIF viewer honours.</summary>
    private static void WriteNetscapeLoopingExtension(Stream output)
    {
        output.WriteByte(0x21); // extension introducer
        output.WriteByte(0xFF); // application extension label
        output.WriteByte(11); // block size
        output.Write("NETSCAPE2.0"u8);
        output.WriteByte(3); // sub-block size
        output.WriteByte(1); // sub-block id
        WriteUInt16LittleEndian(output, 0); // loop count: 0 = forever
        output.WriteByte(0); // block terminator
    }

    private static void WriteGraphicControlExtension(Stream output, int delayCentiseconds)
    {
        output.WriteByte(0x21); // extension introducer
        output.WriteByte(0xF9); // graphic control label
        output.WriteByte(4); // block size
        output.WriteByte(0); // disposal method 0 (unspecified), no transparency
        WriteUInt16LittleEndian(output, (ushort)Math.Clamp(delayCentiseconds, 1, ushort.MaxValue));
        output.WriteByte(0); // transparent colour index (unused)
        output.WriteByte(0); // block terminator
    }

    private static void WriteImageDescriptorAndData(Stream output, int width, int height, byte[] indices, int bitsPerPixel)
    {
        output.WriteByte(0x2C); // image separator
        WriteUInt16LittleEndian(output, 0); // left
        WriteUInt16LittleEndian(output, 0); // top
        WriteUInt16LittleEndian(output, (ushort)width);
        WriteUInt16LittleEndian(output, (ushort)height);
        output.WriteByte(0); // no local colour table, no interlace, no sort

        var lzwMinCodeSize = (byte)bitsPerPixel;
        output.WriteByte(lzwMinCodeSize);

        var compressed = GifLzwEncoder.Compress(indices, lzwMinCodeSize);
        WriteSubBlocks(output, compressed);
        output.WriteByte(0); // block terminator
    }

    private static void WriteSubBlocks(Stream output, byte[] data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var chunkSize = Math.Min(255, data.Length - offset);
            output.WriteByte((byte)chunkSize);
            output.Write(data, offset, chunkSize);
            offset += chunkSize;
        }
    }

    private static void WriteUInt16LittleEndian(Stream output, ushort value)
    {
        output.WriteByte((byte)(value & 0xFF));
        output.WriteByte((byte)(value >> 8));
    }
}
