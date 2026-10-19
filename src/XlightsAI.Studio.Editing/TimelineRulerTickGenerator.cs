namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 016 ("Build time ruler"): turns a <see cref="TimelineViewport"/>'s current
/// scroll/zoom into the actual labeled tick marks a ruler control renders, choosing a "nice"
/// (1/2/5 x 10^n) tick spacing so labels never overlap regardless of zoom level, and only
/// generating ticks across the currently visible pixel width — never the full, potentially huge,
/// content bounds — per Section 3's virtualization requirement.
/// </summary>
public static class TimelineRulerTickGenerator
{
    private static readonly double[] NiceFactors = [1d, 2d, 5d];

    /// <summary>
    /// Chooses the smallest "nice" tick interval (in canonical ticks) whose on-screen pixel
    /// spacing, at <paramref name="ticksPerPixel"/>, is at least <paramref name="targetPixelSpacing"/>.
    /// </summary>
    public static long ChooseTickIntervalTicks(double ticksPerPixel, double targetPixelSpacing = 80d)
    {
        if (double.IsNaN(ticksPerPixel) || double.IsInfinity(ticksPerPixel) || ticksPerPixel <= 0)
        {
            throw new TimelineViewportInvalidException(
                $"Invalid zoom level: ticksPerPixel ({ticksPerPixel}) must be a positive, finite number.");
        }

        if (double.IsNaN(targetPixelSpacing) || double.IsInfinity(targetPixelSpacing) || targetPixelSpacing <= 0)
        {
            throw new TimelineViewportInvalidException(
                $"Invalid target pixel spacing: targetPixelSpacing ({targetPixelSpacing}) must be a positive, finite number.");
        }

        var raw = ticksPerPixel * targetPixelSpacing;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var factor in NiceFactors)
        {
            var candidate = factor * magnitude;
            if (candidate >= raw)
            {
                return (long)Math.Round(candidate, MidpointRounding.AwayFromZero);
            }
        }

        return (long)Math.Round(10d * magnitude, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Generates every ruler tick visible across <paramref name="viewportWidthPixels"/> pixels of
    /// <paramref name="viewport"/>'s current scroll/zoom, each labeled in milliseconds (PHASE-03
    /// Section 2: "UI may show milliseconds").
    /// </summary>
    public static IReadOnlyList<TimelineRulerTick> GenerateTicks(
        TimelineViewport viewport, double viewportWidthPixels, double targetPixelSpacing = 80d)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        var visible = viewport.VisibleWindow(viewportWidthPixels);
        var interval = ChooseTickIntervalTicks(viewport.TicksPerPixel, targetPixelSpacing);
        var firstTick = (long)Math.Floor((double)visible.StartTicks / interval) * interval;

        var ticks = new List<TimelineRulerTick>();
        for (var t = firstTick; t <= visible.EndTicks; t += interval)
        {
            var x = viewport.XFromTime(t);
            var milliseconds = t / 10_000d;
            ticks.Add(new TimelineRulerTick(t, x, $"{milliseconds:0.##} ms"));
        }

        return ticks;
    }
}
