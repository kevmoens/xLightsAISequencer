namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Build-plan Phase 18 §4 (Phase C): builds the real GIF global colour table (max 256 entries, as the GIF89a
/// format itself requires) <see cref="AnimatedGifEncoder"/> needs, from the actual colours a fixture's own
/// rasterized frames use. Every fixture rasterized by <see cref="PreviewFrameRasterizer"/> is a flat-colour
/// point-plot on a black background, so the exact-colour path below is expected to be the one that always
/// actually runs; the 6-levels-per-channel (216-colour) fallback exists purely as a correctness guarantee for
/// a hypothetical sequence with more than 256 distinct colours, never silently dropping colour fidelity
/// without a reason to.
/// </summary>
public sealed class GifPalette
{
    private readonly int _levelsPerChannel;
    private readonly Dictionary<int, byte> _quantizedColorToIndex;

    internal GifPalette(IReadOnlyList<(byte R, byte G, byte B)> colors, int bitsPerPixel, int levelsPerChannel)
    {
        Colors = colors;
        BitsPerPixel = bitsPerPixel;
        _levelsPerChannel = levelsPerChannel;
        _quantizedColorToIndex = new Dictionary<int, byte>(colors.Count);
        for (var i = 0; i < colors.Count; i++)
        {
            _quantizedColorToIndex[GifColorQuantizer.Pack(colors[i])] = (byte)i;
        }
    }

    public IReadOnlyList<(byte R, byte G, byte B)> Colors { get; }

    public int BitsPerPixel { get; }

    /// <summary>Maps a raw row-major RGB buffer (3 bytes/pixel) to one palette index per pixel, via the same quantization level this palette was built at.</summary>
    public byte[] ToIndices(byte[] rgb)
    {
        var pixelCount = rgb.Length / 3;
        var indices = new byte[pixelCount];
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 3;
            var quantized = GifColorQuantizer.Quantize(rgb[offset], rgb[offset + 1], rgb[offset + 2], _levelsPerChannel);
            indices[i] = _quantizedColorToIndex[GifColorQuantizer.Pack(quantized)];
        }

        return indices;
    }
}

internal static class GifColorQuantizer
{
    public static GifPalette BuildPalette(IReadOnlyList<AnimatedGifEncoder.Frame> frames, int maxColors)
    {
        var exact = CollectDistinctColors(frames, levelsPerChannel: 256);
        if (exact.Count <= maxColors)
        {
            var exactColors = exact.Select(Unpack).OrderBy(Pack).ToList();
            return new GifPalette(exactColors, Math.Max(2, CeilLog2(exactColors.Count)), levelsPerChannel: 256);
        }

        // 6 levels/channel = 216 possible colours, always <= a 256-colour palette -- guaranteed to fit.
        var quantized = CollectDistinctColors(frames, levelsPerChannel: 6);
        var quantizedColors = quantized.Select(Unpack).OrderBy(Pack).ToList();
        return new GifPalette(quantizedColors, Math.Max(2, CeilLog2(quantizedColors.Count)), levelsPerChannel: 6);
    }

    internal static (byte R, byte G, byte B) Quantize(byte r, byte g, byte b, int levelsPerChannel) =>
        levelsPerChannel >= 256
            ? (r, g, b)
            : (QuantizeChannel(r, levelsPerChannel), QuantizeChannel(g, levelsPerChannel), QuantizeChannel(b, levelsPerChannel));

    internal static int Pack((byte R, byte G, byte B) color) => (color.R << 16) | (color.G << 8) | color.B;

    private static (byte R, byte G, byte B) Unpack(int packed) =>
        ((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);

    private static byte QuantizeChannel(byte value, int levels)
    {
        var step = 255.0 / (levels - 1);
        var levelIndex = (int)Math.Round(value / step);
        return (byte)Math.Round(levelIndex * step);
    }

    private static HashSet<int> CollectDistinctColors(IReadOnlyList<AnimatedGifEncoder.Frame> frames, int levelsPerChannel)
    {
        var distinct = new HashSet<int>();
        foreach (var frame in frames)
        {
            var rgb = frame.Rgb;
            for (var i = 0; i < rgb.Length; i += 3)
            {
                var quantized = Quantize(rgb[i], rgb[i + 1], rgb[i + 2], levelsPerChannel);
                distinct.Add(Pack(quantized));
            }
        }

        return distinct;
    }

    private static int CeilLog2(int value)
    {
        var bits = 0;
        var capacity = 1;
        while (capacity < value)
        {
            capacity <<= 1;
            bits++;
        }

        return bits;
    }
}
