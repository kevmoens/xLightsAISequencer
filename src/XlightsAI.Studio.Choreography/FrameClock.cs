using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>Named fractions of a bar/beat a Look layer's <c>timescale</c>/fade can be expressed in (architecture §4's <c>"timescale":"bar"</c>, §7's "fades... in note values, never milliseconds").</summary>
public enum NoteValue
{
    Bar,
    HalfBar,
    QuarterBar,
    Beat,
    HalfBeat,
    QuarterBeat,
}

/// <summary>
/// Build-plan Phase 4 task 1 ("frame-interval-aligned boundaries... fades expressed in note values,
/// never milliseconds"): rounds a tick boundary to the exporter's own frame grid, and converts a
/// declared note value into a real tick duration from the song's own measured <see cref="BarGrid"/> (its
/// real detected bar/beat spans, never an assumed tempo-period formula) -- "compute half a bar at 143
/// BPM and serialize the result," architecture §7, done here from the real grid rather than a BPM
/// multiplication so beat jitter (±0.05s, per <c>MUSIC_FLOW_ANALYSIS.md</c>) never desyncs a fade from
/// the music it is supposed to track.
/// </summary>
public static class FrameClock
{
    private const long TicksPerSecond = 10_000_000L;
    private const long TicksPerMillisecond = TicksPerSecond / 1000L;

    /// <summary>The exporter's own frame grid (currently 25ms) -- a single source of truth so an IR-level boundary and the exported file's own rounding never disagree.</summary>
    public static int FrameIntervalMs => XsqExporter.FrameIntervalMs;

    /// <summary>One frame interval, in ticks -- the minimum duration any effect can round to a nonzero length at export time.</summary>
    public static long FrameIntervalTicks => FrameIntervalMs * TicksPerMillisecond;

    /// <summary>Rounds <paramref name="ticks"/> to the nearest exact frame boundary, in ticks.</summary>
    public static long RoundToFrameTicks(long ticks)
    {
        var frameTicks = FrameIntervalMs * TicksPerMillisecond;
        return (long)Math.Round(ticks / (double)frameTicks, MidpointRounding.AwayFromZero) * frameTicks;
    }

    /// <summary>
    /// The real tick duration of <paramref name="value"/> inside <paramref name="bar"/>, using the bar's
    /// own measured span (for <see cref="NoteValue.Bar"/>/<see cref="NoteValue.HalfBar"/>/<see cref="NoteValue.QuarterBar"/>)
    /// or an even subdivision of it by <paramref name="beatsPerBar"/> (for the beat-scale values) --
    /// never a BPM-derived formula. The result is frame-rounded before being returned.
    /// </summary>
    public static long NoteValueTicks(BarWindow bar, NoteValue value, int beatsPerBar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        if (beatsPerBar <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "Must be positive.");
        }

        var barSpan = bar.Window.EndTicks - bar.Window.StartTicks;
        var beatSpan = barSpan / (double)beatsPerBar;

        var raw = value switch
        {
            NoteValue.Bar => barSpan,
            NoteValue.HalfBar => barSpan / 2.0,
            NoteValue.QuarterBar => barSpan / 4.0,
            NoteValue.Beat => beatSpan,
            NoteValue.HalfBeat => beatSpan / 2.0,
            NoteValue.QuarterBeat => beatSpan / 4.0,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown note value."),
        };

        return RoundToFrameTicks((long)Math.Round(raw));
    }
}
