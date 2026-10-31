using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 1 ("Grid correctness: beat-index bar definition... never by multiplying a
/// tempo period"): bars defined purely from <see cref="BeatGrid.Beats"/>' own <see cref="BeatMarker.BarIndex"/>
/// grouping -- <c>bar b = beats[4b...4b+4]</c>, architecture §7, verbatim -- not from a fixed
/// beats-per-bar time interval. <see cref="Rhythm.BeatGridBuilder"/> already computes <see cref="BeatMarker.BarIndex"/>
/// this way (Phase 5); this type is a typed, queryable view over that existing grid, not a new analysis.
/// </summary>
public sealed class BarGrid
{
    private readonly IReadOnlyList<BarWindow> _bars;
    private readonly IReadOnlyList<BeatMarker> _beats;

    private BarGrid(IReadOnlyList<BarWindow> bars, IReadOnlyList<BeatMarker> beats)
    {
        _bars = bars;
        _beats = beats;
    }

    /// <summary>Every bar, ordered by <see cref="BarIndex"/>.</summary>
    public IReadOnlyList<BarWindow> Bars => _bars;

    /// <summary>Every beat, ordered by <see cref="BeatMarker.TimeTicks"/> -- the raw grid this bar view was built from.</summary>
    public IReadOnlyList<BeatMarker> Beats => _beats;

    /// <summary>
    /// Builds a <see cref="BarGrid"/> from an already-approved <see cref="BeatGrid"/>, grouping beats by
    /// their own <see cref="BeatMarker.BarIndex"/> (never recomputing bar boundaries from a fixed period).
    /// A bar's window runs from its own first beat's <see cref="BeatMarker.TimeTicks"/> up to the next
    /// bar's own first beat, or <paramref name="songEndTicks"/> for the final bar. Returns an empty grid
    /// (never throws) for a <see cref="BeatGrid"/> with no beats -- the same "silence is a valid,
    /// inspectable result" convention this codebase uses throughout.
    /// </summary>
    public static BarGrid FromBeatGrid(BeatGrid beatGrid, long songEndTicks)
    {
        ArgumentNullException.ThrowIfNull(beatGrid);

        if (beatGrid.Beats.Length == 0)
        {
            return new BarGrid([], []);
        }

        var orderedBeats = beatGrid.Beats.OrderBy(b => b.TimeTicks).ToList();

        var barStarts = orderedBeats
            .GroupBy(b => b.BarIndex)
            .Select(g => (BarIndex: g.Key, StartTicks: g.Min(b => b.TimeTicks)))
            .OrderBy(b => b.StartTicks)
            .ToList();

        var bars = new List<BarWindow>(barStarts.Count);
        for (var i = 0; i < barStarts.Count; i++)
        {
            var start = barStarts[i].StartTicks;
            var end = i + 1 < barStarts.Count ? barStarts[i + 1].StartTicks : Math.Max(songEndTicks, start);
            bars.Add(new BarWindow(new BarIndex(barStarts[i].BarIndex), new TimeWindow(start, end)));
        }

        return new BarGrid(bars, orderedBeats);
    }

    /// <summary>The bar containing <paramref name="tick"/>, or <see langword="null"/> when outside every bar's own window.</summary>
    public BarWindow? BarAt(long tick) => _bars.FirstOrDefault(b => tick >= b.Window.StartTicks && tick < b.Window.EndTicks);

    /// <summary>One bar's own window ticks, in <c>[startBar, endBarExclusive)</c> -- the half-open tick range spanning that bar range, or <see langword="null"/> when either bound does not exist in this grid.</summary>
    public TimeWindow? WindowForBarRange(BarIndex startBar, BarIndex endBarExclusive)
    {
        var start = _bars.FirstOrDefault(b => b.Index == startBar);
        if (start is null)
        {
            return null;
        }

        var endBarInclusive = new BarIndex(endBarExclusive.Value - 1);
        var end = _bars.FirstOrDefault(b => b.Index == endBarInclusive);
        return end is null ? null : new TimeWindow(start.Window.StartTicks, end.Window.EndTicks);
    }
}
