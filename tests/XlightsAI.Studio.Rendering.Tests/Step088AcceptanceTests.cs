using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// PHASE-13 Step 088 ("Compute display-level metrics for brightness, coverage, spatial motion and
/// density"). Required acceptance, verbatim: "Designed render samples produce sensible metric order."
/// Required failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not
/// corrupt the project."
///
/// <para>
/// Every fixture here is a hand-designed <see cref="PreviewFrame"/> window with a known, reasoned-about
/// shape (a bright/dense/covering/moving sample vs. a dim/sparse/static one) -- not a mock of
/// <see cref="IDisplayMetricsCalculator"/> itself. The calculator under test is the real
/// <see cref="DisplayMetricsCalculator"/>.
/// </para>
/// </summary>
public sealed class Step088AcceptanceTests
{
    private static readonly ModelId ModelA = new("ModelA");
    private static readonly ModelId ModelB = new("ModelB");
    private static readonly ModelId ModelC = new("ModelC");
    private static readonly ModelId ModelD = new("ModelD");

    private static PreviewFrame Frame(long timeTicks, params PixelPoint[] illuminated) =>
        new(timeTicks, illuminated, UnsupportedEffectIds: []);

    [Fact]
    public void Step088_Acceptance_Passes()
    {
        // Bright/dense/covering/moving sample: every one of 4 known points lit at near-full white, and the
        // illuminated centroid moves across 3 frames.
        var brightMovingWindow = new[]
        {
            Frame(0,
                new PixelPoint(ModelA, 0.0, 0.0, 255, 255, 255),
                new PixelPoint(ModelB, 0.2, 0.2, 255, 255, 255)),
            Frame(100_000,
                new PixelPoint(ModelC, 0.5, 0.5, 255, 255, 255),
                new PixelPoint(ModelD, 0.6, 0.6, 255, 255, 255)),
            Frame(200_000,
                new PixelPoint(ModelA, 1.0, 1.0, 255, 255, 255),
                new PixelPoint(ModelB, 0.9, 0.9, 255, 255, 255)),
        };

        // Dim/sparse/static sample: the same single point, dimly lit, in the same place every frame.
        var dimStaticWindow = new[]
        {
            Frame(0, new PixelPoint(ModelA, 0.5, 0.5, 20, 20, 20)),
            Frame(100_000, new PixelPoint(ModelA, 0.5, 0.5, 20, 20, 20)),
            Frame(200_000, new PixelPoint(ModelA, 0.5, 0.5, 20, 20, 20)),
        };

        var calculator = new DisplayMetricsCalculator();
        var bright = calculator.Compute(brightMovingWindow, totalDisplayPointCount: 4);
        var dim = calculator.Compute(dimStaticWindow, totalDisplayPointCount: 4);

        // Sensible order: the designed-bright sample measures higher on every axis than the designed-dim one.
        Assert.True(bright.MeanBrightness > dim.MeanBrightness);
        Assert.True(bright.CoverageRatio > dim.CoverageRatio);
        Assert.True(bright.DensityRatio > dim.DensityRatio);
        Assert.True(bright.SpatialMotion > dim.SpatialMotion);

        // Grounded absolute values, not just relative order.
        Assert.Equal(1.0, bright.MeanBrightness, precision: 6);
        Assert.Equal(1.0, bright.CoverageRatio, precision: 6);
        Assert.Equal(0.5, bright.DensityRatio, precision: 6); // 2 of 4 points lit every frame

        Assert.Equal(20.0 / 255.0, dim.MeanBrightness, precision: 6);
        Assert.Equal(0.25, dim.CoverageRatio, precision: 6); // 1 of 4 points ever lit
        Assert.Equal(0.25, dim.DensityRatio, precision: 6); // 1 of 4 points lit every frame
        Assert.Equal(0.0, dim.SpatialMotion, precision: 6); // same point, never moves
    }

    [Fact]
    public void Step088_Acceptance_CoverageIsCumulativeButDensityIsInstantaneous()
    {
        // 4 distinct points, each lit alone in its own frame: every point is *ever* lit (full coverage),
        // but only one point is lit *at a time* (low instantaneous density).
        var window = new[]
        {
            Frame(0, new PixelPoint(ModelA, 0.0, 0.0, 255, 255, 255)),
            Frame(100_000, new PixelPoint(ModelB, 0.0, 0.0, 255, 255, 255)),
            Frame(200_000, new PixelPoint(ModelC, 0.0, 0.0, 255, 255, 255)),
            Frame(300_000, new PixelPoint(ModelD, 0.0, 0.0, 255, 255, 255)),
        };

        var result = new DisplayMetricsCalculator().Compute(window, totalDisplayPointCount: 4);

        Assert.Equal(1.0, result.CoverageRatio, precision: 6); // all 4 ever lit
        Assert.Equal(0.25, result.DensityRatio, precision: 6); // only 1 of 4 lit, on average
    }

    [Fact]
    public void Step088_Acceptance_EmptyWindow_YieldsZeroMetricsWithoutThrowing()
    {
        var result = new DisplayMetricsCalculator().Compute([], totalDisplayPointCount: 10);

        Assert.Equal(new DisplayMetrics(0.0, 0.0, 0.0, 0.0), result);
    }

    [Fact]
    public void Step088_Acceptance_AllFramesDark_YieldsZeroMetricsWithoutThrowing()
    {
        var window = new[] { Frame(0), Frame(100_000), Frame(200_000) };

        var result = new DisplayMetricsCalculator().Compute(window, totalDisplayPointCount: 10);

        Assert.Equal(new DisplayMetrics(0.0, 0.0, 0.0, 0.0), result);
    }

    [Fact]
    public void NegativePath_NullFrameWindow_ThrowsWithoutProducingMetrics()
    {
        Assert.Throws<DisplayMetricsInvalidException>(
            () => new DisplayMetricsCalculator().Compute(null!, totalDisplayPointCount: 4));
    }

    [Fact]
    public void NegativePath_NullFrameInsideWindow_ThrowsRatherThanSkippingIt()
    {
        var window = new PreviewFrame?[] { Frame(0, new PixelPoint(ModelA, 0, 0, 1, 1, 1)), null };

        var ex = Assert.Throws<DisplayMetricsInvalidException>(
            () => new DisplayMetricsCalculator().Compute(window!, totalDisplayPointCount: 4));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NegativePath_NonPositiveTotalPointCount_ThrowsRatherThanDividingByZero(int totalDisplayPointCount)
    {
        var window = new[] { Frame(0, new PixelPoint(ModelA, 0, 0, 1, 1, 1)) };

        Assert.Throws<DisplayMetricsInvalidException>(
            () => new DisplayMetricsCalculator().Compute(window, totalDisplayPointCount));
    }
}
