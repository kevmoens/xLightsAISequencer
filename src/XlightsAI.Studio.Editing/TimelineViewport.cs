using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 016 ("Build time ruler, horizontal scroll and zoom"): an immutable snapshot of
/// one timeline's scroll offset and zoom level, plus the virtualized content bounds it is
/// clamped against. <see cref="TimeFromX"/>/<see cref="XFromTime"/> are the single shared
/// pixel&lt;-&gt;tick transform every timeline-aware control (ruler, rows, effect blocks, hit
/// testing) maps through, per PHASE-03 Section 3's "Architectural design notes": "UI uses
/// virtualization for many rows and effects; hit testing maps horizontal pixels through the
/// common time transform."
///
/// Every mutator (<see cref="ZoomAt"/>, <see cref="ScrollByPixels"/>) returns a new,
/// independently valid instance rather than mutating in place — the same immutable-value
/// discipline PHASE-03 Section 3 requires of <c>EditScope</c>/<c>EditCommand</c> — and every
/// factory path funnels through <see cref="Create"/>, so an invalid zoom or an inverted content
/// window can never silently produce a viewport that answers pixel/tick questions with garbage.
/// </summary>
public sealed class TimelineViewport : ITimelineViewport
{
    /// <summary>Most zoomed-in allowed level: 1 pixel == 100 ticks == 10 microseconds.</summary>
    public const double MinTicksPerPixel = 100d;

    /// <summary>Most zoomed-out allowed level: 1 pixel == 10,000,000 ticks == 1 second.</summary>
    public const double MaxTicksPerPixel = 10_000_000d;

    private TimelineViewport(TimeWindow contentBounds, long scrollOffsetTicks, double ticksPerPixel)
    {
        ContentBounds = contentBounds;
        ScrollOffsetTicks = scrollOffsetTicks;
        TicksPerPixel = ticksPerPixel;
    }

    /// <summary>The full, virtualized extent of the timeline content this viewport scrolls over (half-open <c>[Start, End)</c>).</summary>
    public TimeWindow ContentBounds { get; }

    /// <summary>The canonical tick value shown at pixel <c>0</c>.</summary>
    public long ScrollOffsetTicks { get; }

    /// <summary>The current zoom level: how many ticks one horizontal pixel spans.</summary>
    public double TicksPerPixel { get; }

    /// <summary>
    /// Builds a new viewport. Rejects an inverted <paramref name="contentBounds"/> (End before
    /// Start) and a non-positive/non-finite <paramref name="ticksPerPixel"/> explicitly
    /// (<see cref="TimelineViewportInvalidException"/>) rather than silently clamping them into
    /// something plausible-looking. A valid, zero-length <paramref name="contentBounds"/> (the
    /// "silence" edge case — an empty document with nothing on the timeline yet) is explicitly
    /// supported: the viewport is still constructed, pinned to that single instant.
    /// </summary>
    public static TimelineViewport Create(TimeWindow contentBounds, double ticksPerPixel, long? scrollOffsetTicks = null)
    {
        if (contentBounds.EndTicks < contentBounds.StartTicks)
        {
            throw new TimelineViewportInvalidException(
                $"Invalid timeline content bounds: EndTicks ({contentBounds.EndTicks}) is before StartTicks ({contentBounds.StartTicks}).");
        }

        if (double.IsNaN(ticksPerPixel) || double.IsInfinity(ticksPerPixel) || ticksPerPixel <= 0)
        {
            throw new TimelineViewportInvalidException(
                $"Invalid zoom level: ticksPerPixel ({ticksPerPixel}) must be a positive, finite number.");
        }

        var clampedZoom = Math.Clamp(ticksPerPixel, MinTicksPerPixel, MaxTicksPerPixel);
        var offset = ClampOffset(scrollOffsetTicks ?? contentBounds.StartTicks, contentBounds);
        return new TimelineViewport(contentBounds, offset, clampedZoom);
    }

    /// <summary>The canonical tick at horizontal pixel <paramref name="pixel"/>, given the current scroll/zoom.</summary>
    public long TimeFromX(double pixel) =>
        ScrollOffsetTicks + (long)Math.Round(pixel * TicksPerPixel, MidpointRounding.AwayFromZero);

    /// <summary>The horizontal pixel offset of canonical tick <paramref name="ticks"/>, given the current scroll/zoom. The exact inverse of <see cref="TimeFromX"/> up to pixel rounding.</summary>
    public double XFromTime(long ticks) => (ticks - ScrollOffsetTicks) / TicksPerPixel;

    /// <summary>
    /// Returns a new viewport zoomed by <paramref name="zoomFactor"/> (&lt;1 zooms in, &gt;1 zooms
    /// out), clamped to <see cref="MinTicksPerPixel"/>/<see cref="MaxTicksPerPixel"/>, anchored so
    /// the tick currently under <paramref name="anchorPixel"/> stays under that same pixel after
    /// the zoom — the "zoom anchors" PHASE-03 Step 016 names explicitly.
    /// </summary>
    public TimelineViewport ZoomAt(double anchorPixel, double zoomFactor)
    {
        if (double.IsNaN(zoomFactor) || double.IsInfinity(zoomFactor) || zoomFactor <= 0)
        {
            throw new TimelineViewportInvalidException(
                $"Invalid zoom factor: zoomFactor ({zoomFactor}) must be a positive, finite number.");
        }

        var anchorTicks = TimeFromX(anchorPixel);
        var newTicksPerPixel = Math.Clamp(TicksPerPixel * zoomFactor, MinTicksPerPixel, MaxTicksPerPixel);
        var newOffset = anchorTicks - (long)Math.Round(anchorPixel * newTicksPerPixel, MidpointRounding.AwayFromZero);
        return new TimelineViewport(ContentBounds, ClampOffset(newOffset, ContentBounds), newTicksPerPixel);
    }

    /// <summary>Returns a new viewport scrolled by <paramref name="pixelDelta"/> horizontal pixels, clamped to <see cref="ContentBounds"/> (the "virtualized timeline bounds" Step 016 names explicitly).</summary>
    public TimelineViewport ScrollByPixels(double pixelDelta)
    {
        var newOffset = ScrollOffsetTicks + (long)Math.Round(pixelDelta * TicksPerPixel, MidpointRounding.AwayFromZero);
        return new TimelineViewport(ContentBounds, ClampOffset(newOffset, ContentBounds), TicksPerPixel);
    }

    /// <summary>The half-open tick range currently visible across a viewport of <paramref name="viewportWidthPixels"/> wide pixels, for virtualized rendering.</summary>
    public TimeWindow VisibleWindow(double viewportWidthPixels)
    {
        if (viewportWidthPixels < 0 || double.IsNaN(viewportWidthPixels) || double.IsInfinity(viewportWidthPixels))
        {
            throw new TimelineViewportInvalidException(
                $"Invalid viewport width: viewportWidthPixels ({viewportWidthPixels}) must be a non-negative, finite number.");
        }

        var endTicks = ScrollOffsetTicks + (long)Math.Ceiling(viewportWidthPixels * TicksPerPixel);
        return new TimeWindow(ScrollOffsetTicks, endTicks);
    }

    private static long ClampOffset(long offset, TimeWindow contentBounds) =>
        Math.Clamp(offset, contentBounds.StartTicks, Math.Max(contentBounds.StartTicks, contentBounds.EndTicks));
}
