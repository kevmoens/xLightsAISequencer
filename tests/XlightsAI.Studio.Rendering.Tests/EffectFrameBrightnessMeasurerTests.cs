using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// Build-plan Phase 21 §2a: the metric is explicitly "averaged over every pixel in the fixture, lit or not"
/// -- never only the subset that happens to be lit. A sparse/scattered effect with a few fully-lit pixels
/// must measure low, the same as a uniformly dim effect, when most of the canvas stays dark.
/// </summary>
public sealed class EffectFrameBrightnessMeasurerTests
{
    private static readonly ModelId Model = new("FixtureLowResString");

    private static PixelPoint White(double x) => new(Model, x, 0, 255, 255, 255);

    [Fact]
    public void Measure_NoFrames_ReturnsZero()
    {
        var (mean, peak) = EffectFrameBrightnessMeasurer.Measure([], totalPixelCount: 20);

        Assert.Equal(0.0, mean);
        Assert.Equal(0.0, peak);
    }

    [Fact]
    public void Measure_EveryPixelFullyLitWhite_ReturnsOne()
    {
        var frame = new PreviewFrame(0, Enumerable.Range(0, 20).Select(i => White(i)).ToList(), []);

        var (mean, peak) = EffectFrameBrightnessMeasurer.Measure([frame], totalPixelCount: 20);

        Assert.Equal(1.0, mean, 3);
        Assert.Equal(1.0, peak, 3);
    }

    [Fact]
    public void Measure_SparseEffect_DividesByTheWholeCanvas_NotJustTheLitPixels()
    {
        // Only 1 of 20 pixels lit, fully white -- the user's own named case: a few intensely bright pixels on
        // an otherwise-dark canvas must still measure as dim overall, never as if only the lit pixel counted.
        var frame = new PreviewFrame(0, [White(0)], []);

        var (mean, peak) = EffectFrameBrightnessMeasurer.Measure([frame], totalPixelCount: 20);

        Assert.Equal(1.0 / 20.0, mean, 5);
        Assert.Equal(1.0 / 20.0, peak, 5);
    }

    [Fact]
    public void Measure_MeanBrightness_AveragesAcrossFrames_PeakTakesTheBrightestSingleFrame()
    {
        var dim = new PreviewFrame(0, [White(0)], []);
        var bright = new PreviewFrame(1, Enumerable.Range(0, 20).Select(i => White(i)).ToList(), []);

        var (mean, peak) = EffectFrameBrightnessMeasurer.Measure([dim, bright], totalPixelCount: 20);

        Assert.Equal((0.05 + 1.0) / 2.0, mean, 5);
        Assert.Equal(1.0, peak, 5);
    }

    [Fact]
    public void Measure_InvalidTotalPixelCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EffectFrameBrightnessMeasurer.Measure([], totalPixelCount: 0));
    }
}
