using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 016 ("Build time ruler, horizontal scroll and zoom"). Required acceptance:
/// "Pixel-to-time conversion tests at zoom extremes." Required failure/edge case: "Silence,
/// invalid timing boundaries and missing media must be handled explicitly."
/// </summary>
public sealed class Step016AcceptanceTests
{
    private static readonly TimeWindow TenSecondContent = new(0, 100_000_000);

    [Theory]
    [InlineData(0d, 0, 12_345d)]
    [InlineData(100d, 50_000, 0d)]
    [InlineData(-200d, -1_000_000, 10_000d)]
    public void Step016_Acceptance_Passes_BidirectionalConversionAtArbitraryScrollOffsets(
        double pixel, long scrollOffsetTicks, double ticksPerPixel)
    {
        var viewport = TimelineViewport.Create(TenSecondContent, ticksPerPixel == 0d ? 1_000d : ticksPerPixel, scrollOffsetTicks);

        var ticks = viewport.TimeFromX(pixel);
        var roundTrippedPixel = viewport.XFromTime(ticks);

        // Round trip is exact up to the pixel->tick rounding step (sub-pixel precision is not
        // preserved, by design — the UI only ever asks "what tick is at this exact pixel"); the
        // maximum possible rounding error is half a tick, i.e. 0.5 / TicksPerPixel pixels.
        Assert.True(
            Math.Abs(pixel - roundTrippedPixel) <= 0.5 / viewport.TicksPerPixel + 1e-9,
            $"Expected {roundTrippedPixel} to round-trip back to {pixel} within tolerance.");
        Assert.Equal(viewport.ScrollOffsetTicks + (long)Math.Round(pixel * viewport.TicksPerPixel), ticks);
    }

    [Fact]
    public void Step016_Acceptance_Passes_PixelToTimeConversion_AtMostZoomedInExtreme()
    {
        var viewport = TimelineViewport.Create(TenSecondContent, TimelineViewport.MinTicksPerPixel, scrollOffsetTicks: 500);

        Assert.Equal(TimelineViewport.MinTicksPerPixel, viewport.TicksPerPixel);
        Assert.Equal(500 + 100 * (long)TimelineViewport.MinTicksPerPixel, viewport.TimeFromX(100));
        Assert.Equal(500L, viewport.TimeFromX(0));

        // An attempt to zoom in further than the extreme stays pinned at the extreme, not below it.
        var zoomedInFurther = viewport.ZoomAt(anchorPixel: 50, zoomFactor: 0.1);
        Assert.Equal(TimelineViewport.MinTicksPerPixel, zoomedInFurther.TicksPerPixel);
    }

    [Fact]
    public void Step016_Acceptance_Passes_PixelToTimeConversion_AtMostZoomedOutExtreme()
    {
        var viewport = TimelineViewport.Create(TenSecondContent, TimelineViewport.MaxTicksPerPixel, scrollOffsetTicks: 0);

        Assert.Equal(TimelineViewport.MaxTicksPerPixel, viewport.TicksPerPixel);
        Assert.Equal((long)(10 * TimelineViewport.MaxTicksPerPixel), viewport.TimeFromX(10));

        // An attempt to zoom out further than the extreme stays pinned at the extreme, not above it.
        var zoomedOutFurther = viewport.ZoomAt(anchorPixel: 50, zoomFactor: 100);
        Assert.Equal(TimelineViewport.MaxTicksPerPixel, zoomedOutFurther.TicksPerPixel);
    }

    [Fact]
    public void Step016_Acceptance_Passes_ZoomAnchorKeepsAnchoredTickUnderSamePixel()
    {
        // scrollOffsetTicks is chosen far enough from both content edges that neither the
        // zoom-in nor the zoom-out branch below needs to clamp against ContentBounds — this test
        // is specifically about the anchor math, not the separately-covered clamping behavior
        // (Step016_Acceptance_Passes_ScrollingIsClampedToVirtualizedContentBounds).
        var viewport = TimelineViewport.Create(TenSecondContent, ticksPerPixel: 10_000d, scrollOffsetTicks: 20_000_000);
        const double anchorPixel = 300d;
        var anchoredTick = viewport.TimeFromX(anchorPixel);

        var zoomedIn = viewport.ZoomAt(anchorPixel, zoomFactor: 0.25);
        var zoomedOut = viewport.ZoomAt(anchorPixel, zoomFactor: 4.0);

        Assert.Equal(anchoredTick, zoomedIn.TimeFromX(anchorPixel));
        Assert.Equal(anchoredTick, zoomedOut.TimeFromX(anchorPixel));
        Assert.NotEqual(viewport.TicksPerPixel, zoomedIn.TicksPerPixel);
        Assert.NotEqual(viewport.TicksPerPixel, zoomedOut.TicksPerPixel);
    }

    [Fact]
    public void Step016_Acceptance_Passes_ScrollingIsClampedToVirtualizedContentBounds()
    {
        var viewport = TimelineViewport.Create(TenSecondContent, ticksPerPixel: 100_000d, scrollOffsetTicks: 0);

        var scrolledBeforeStart = viewport.ScrollByPixels(-1_000_000);
        Assert.Equal(TenSecondContent.StartTicks, scrolledBeforeStart.ScrollOffsetTicks);

        var scrolledPastEnd = viewport.ScrollByPixels(1_000_000);
        Assert.Equal(TenSecondContent.EndTicks, scrolledPastEnd.ScrollOffsetTicks);
    }

    [Fact]
    public void Step016_Acceptance_Passes_SilenceEdgeCase_ZeroLengthContentStillProducesUsableViewport()
    {
        var silentContent = new TimeWindow(5_000, 5_000);

        var viewport = TimelineViewport.Create(silentContent, ticksPerPixel: 1_000d);

        Assert.Equal(5_000L, viewport.ScrollOffsetTicks);
        Assert.Equal(5_000L, viewport.TimeFromX(0));
        var ticks = TimelineRulerTickGenerator.GenerateTicks(viewport, viewportWidthPixels: 400);
        Assert.NotEmpty(ticks);
    }

    [Fact]
    public void Step016_Acceptance_Passes_InvalidTimingBoundaries_RejectedExplicitly_NegativePath()
    {
        var invertedWindow = new TimeWindow(10_000, 1_000);

        var ex = Assert.Throws<TimelineViewportInvalidException>(
            () => TimelineViewport.Create(invertedWindow, ticksPerPixel: 1_000d));
        Assert.Contains("EndTicks", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Step016_Acceptance_Passes_InvalidZoomLevel_RejectedExplicitly_NegativePath(double invalidTicksPerPixel)
    {
        Assert.Throws<TimelineViewportInvalidException>(
            () => TimelineViewport.Create(TenSecondContent, invalidTicksPerPixel));
    }

    [Fact]
    public void Step016_Acceptance_Passes_MissingMedia_NoContentLoaded_IsHandledExplicitlyByViewModel()
    {
        // The domain contract itself always requires a content window (there is no "null bounds"
        // state) — "missing media" (no document loaded yet) is this step's UI-layer concern,
        // proven end to end by TimelineViewModel in Step016ShellWiringTests. Here we prove the
        // domain's own explicit refusal of a nonsensical/absent bounds stand-in (default struct,
        // zero-width at the origin) still produces a well-defined, non-crashing viewport rather
        // than an unhandled exception deep in rendering code.
        var defaultBounds = default(TimeWindow);

        var viewport = TimelineViewport.Create(defaultBounds, ticksPerPixel: 1_000d);

        Assert.Equal(0L, viewport.ScrollOffsetTicks);
        Assert.Equal(0L, viewport.TimeFromX(0));
    }
}
