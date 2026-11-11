using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering.Tests;

public sealed class AnimatedGifEncoderTests
{
    private static byte[] SolidRgb(int width, int height, byte r, byte g, byte b)
    {
        var rgb = new byte[width * height * 3];
        for (var i = 0; i < width * height; i++)
        {
            rgb[i * 3] = r;
            rgb[(i * 3) + 1] = g;
            rgb[(i * 3) + 2] = b;
        }

        return rgb;
    }

    [Fact]
    public void Encode_TwoSolidColorFrames_ProducesValidGifSignatureAndTrailer()
    {
        var frames = new[]
        {
            new AnimatedGifEncoder.Frame(SolidRgb(4, 4, 255, 0, 0), DelayCentiseconds: 10),
            new AnimatedGifEncoder.Frame(SolidRgb(4, 4, 0, 255, 0), DelayCentiseconds: 10),
        };

        var gif = AnimatedGifEncoder.Encode(4, 4, frames);

        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
        Assert.Equal(0x3B, gif[^1]);
    }

    [Fact]
    public void Encode_TwoSolidColorFrames_RoundTripsThroughTestDecoder()
    {
        var frames = new[]
        {
            new AnimatedGifEncoder.Frame(SolidRgb(8, 6, 255, 0, 0), DelayCentiseconds: 10),
            new AnimatedGifEncoder.Frame(SolidRgb(8, 6, 0, 0, 255), DelayCentiseconds: 25),
        };

        var gif = AnimatedGifEncoder.Encode(8, 6, frames);
        var (width, height, decoded) = MinimalGifDecoderForTests.Decode(gif);

        Assert.Equal(8, width);
        Assert.Equal(6, height);
        Assert.Equal(2, decoded.Count);

        Assert.Equal(10, decoded[0].DelayCentiseconds);
        Assert.Equal(255, decoded[0].Rgb[0]);
        Assert.Equal(0, decoded[0].Rgb[1]);
        Assert.Equal(0, decoded[0].Rgb[2]);

        Assert.Equal(25, decoded[1].DelayCentiseconds);
        Assert.Equal(0, decoded[1].Rgb[0]);
        Assert.Equal(0, decoded[1].Rgb[1]);
        Assert.Equal(255, decoded[1].Rgb[2]);
    }

    [Fact]
    public void Encode_RasterizedPreviewFrameSequence_RoundTripsPointColorAtCenter()
    {
        var modelId = new ModelId("Tree");
        var previewFrames = new[]
        {
            new PreviewFrame(0, [new PixelPoint(modelId, 0.5, 0.5, 255, 128, 0)], []),
            new PreviewFrame(10_000_000, [new PixelPoint(modelId, 0.5, 0.5, 0, 200, 255)], []),
        };

        var gifFrames = previewFrames
            .Select(f => new AnimatedGifEncoder.Frame(PreviewFrameRasterizer.Rasterize(f, 32, 32, pointRadiusPixels: 1), DelayCentiseconds: 10))
            .ToArray();

        var gif = AnimatedGifEncoder.Encode(32, 32, gifFrames);
        var (width, height, decoded) = MinimalGifDecoderForTests.Decode(gif);

        Assert.Equal(32, width);
        Assert.Equal(32, height);
        Assert.Equal(2, decoded.Count);

        var centerOffset = (((height / 2) * width) + (width / 2)) * 3;
        Assert.Equal(255, decoded[0].Rgb[centerOffset]);
        Assert.Equal(128, decoded[0].Rgb[centerOffset + 1]);
        Assert.Equal(0, decoded[0].Rgb[centerOffset + 2]);

        Assert.Equal(0, decoded[1].Rgb[centerOffset]);
        Assert.Equal(200, decoded[1].Rgb[centerOffset + 1]);
        Assert.Equal(255, decoded[1].Rgb[centerOffset + 2]);
    }

    [Fact]
    public void Encode_LongSequenceExceedingInitialLzwCodeTable_StillRoundTrips()
    {
        // 300 frames, each a different solid shade, forces the dictionary through several code-size growths
        // (and, with >256 distinct colours overall, forces the 6-levels/channel palette fallback too).
        var frames = new List<AnimatedGifEncoder.Frame>();
        for (var i = 0; i < 300; i++)
        {
            var shade = (byte)(i % 256);
            frames.Add(new AnimatedGifEncoder.Frame(SolidRgb(6, 6, shade, (byte)(255 - shade), (byte)(shade / 2)), DelayCentiseconds: 4));
        }

        var gif = AnimatedGifEncoder.Encode(6, 6, frames);
        var (width, height, decoded) = MinimalGifDecoderForTests.Decode(gif);

        Assert.Equal(6, width);
        Assert.Equal(6, height);
        Assert.Equal(300, decoded.Count);

        for (var i = 0; i < frames.Count; i++)
        {
            // Palette may have been quantized (>256 distinct colours), so compare within a small tolerance
            // rather than requiring an exact byte match.
            Assert.InRange(decoded[i].Rgb[0], frames[i].Rgb[0] - 26, frames[i].Rgb[0] + 26);
        }
    }

    [Fact]
    public void Encode_EmptyFrameList_Throws()
    {
        Assert.Throws<ArgumentException>(() => AnimatedGifEncoder.Encode(4, 4, []));
    }

    [Fact]
    public void Encode_MismatchedFrameSize_Throws()
    {
        var frames = new[] { new AnimatedGifEncoder.Frame(new byte[10], 10) };
        Assert.Throws<ArgumentException>(() => AnimatedGifEncoder.Encode(4, 4, frames));
    }
}
