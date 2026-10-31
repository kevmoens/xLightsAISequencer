using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering.Tests;

public sealed class PreviewFrameRasterizerTests
{
    [Fact]
    public void RasterizeToPng_EmptyFrame_ProducesValidPngSignature()
    {
        var frame = new PreviewFrame(0, [], []);

        var png = PreviewFrameRasterizer.RasterizeToPng(frame, width: 16, height: 16);

        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.True(png.Length > 8);
    }

    [Fact]
    public void RasterizeToPng_IlluminatedPoint_RoundTripsThroughDecoder()
    {
        var frame = new PreviewFrame(0, [new PixelPoint(new ModelId("Tree"), 0.5, 0.5, 255, 0, 0)], []);

        var png = PreviewFrameRasterizer.RasterizeToPng(frame, width: 32, height: 32, pointRadiusPixels: 1);

        var (width, height, rgb) = MinimalPngDecoderForTests.Decode(png);
        Assert.Equal(32, width);
        Assert.Equal(32, height);

        var centerOffset = (((height / 2) * width) + (width / 2)) * 3;
        Assert.Equal(255, rgb[centerOffset]);
        Assert.Equal(0, rgb[centerOffset + 1]);
        Assert.Equal(0, rgb[centerOffset + 2]);
    }

    [Fact]
    public void RasterizeToPng_InvalidDimensions_Throws()
    {
        var frame = new PreviewFrame(0, [], []);
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewFrameRasterizer.RasterizeToPng(frame, width: 0, height: 10));
    }
}
