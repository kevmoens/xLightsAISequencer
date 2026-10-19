using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 021 ("Multi-row time-range selection with inclusive/exclusive boundary semantics"):
/// turns a rectangular drag selection — a row index range plus a horizontal pixel range — into the
/// exact <see cref="EditScope"/> it covers. Row indices index into the same pre-order, collapse-aware
/// <see cref="TimelineRowSlot"/> list <see cref="TimelineRowFlattener"/> already produces (Step 017's
/// own "virtualized rows" list) — never a separate, parallel row numbering. The resulting
/// <see cref="EditScope.Models"/> contains only the stable <see cref="ModelId"/> of each row actually
/// in the selected index range (Section 3: "never mixing selected row indices with identity"); a group
/// row contributes no id of its own, and a collapsed group's hidden children — not present in
/// <paramref name="visibleRows"/> at all — are never silently swept in just because the drag passed
/// over their collapsed ancestor. "Selections crossing nonadjacent rows must be explicit" (this step's
/// own implementation guidance) is satisfied by construction: every row's identity is resolved
/// individually from the real flattened list, never inferred or interpolated between the two
/// endpoints.
/// </summary>
public static class TimelineRangeSelectionBuilder
{
    /// <summary>
    /// Builds the half-open (<see cref="TimeWindow"/>, PHASE-01's own canonical
    /// <c>[Start, End)</c> rule) <see cref="EditScope"/> covering every row whose index lies between
    /// <paramref name="rowIndexA"/> and <paramref name="rowIndexB"/> (order-independent; either may be
    /// the drag's start or end) and the horizontal span between
    /// <paramref name="pixelA"/>/<paramref name="pixelB"/> (also order-independent — a right-to-left or
    /// bottom-to-top drag normalizes to the same scope a top-to-bottom/left-to-right drag over the same
    /// region would produce). Row/pixel indices outside the real bounds of <paramref name="visibleRows"/>
    /// are clamped, never thrown — a drag that starts or ends off the visible row list still produces a
    /// safe, real scope rather than crashing the gesture. An empty <paramref name="visibleRows"/>
    /// produces an empty-model, zero-length scope (the "silence" edge case this step shares with Step
    /// 016's own viewport).
    /// </summary>
    /// <param name="snapPolicy">
    /// PHASE-04 Step 030 ("Add frame/beat/off-grid snapping..."): an optional snap policy applied to the
    /// resolved <paramref name="pixelA"/>/<paramref name="pixelB"/> tick values before the half-open
    /// window is built — the real "snap policy... as a dependency not baked into drag math" this step's
    /// own implementation guidance names. Defaults to <see langword="null"/> (treated identically to
    /// <see cref="SnapPolicy.None"/>), so every pre-Step-030 call site keeps compiling and behaving
    /// identically.
    /// </param>
    public static EditScope Build(
        IReadOnlyList<TimelineRowSlot> visibleRows,
        int rowIndexA,
        int rowIndexB,
        double pixelA,
        double pixelB,
        ITimelineViewport viewport,
        bool respectLocks = true,
        SnapPolicy? snapPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(visibleRows);
        ArgumentNullException.ThrowIfNull(viewport);

        var rawStartTicks = viewport.TimeFromX(Math.Min(pixelA, pixelB));
        var rawEndTicks = viewport.TimeFromX(Math.Max(pixelA, pixelB));
        if (snapPolicy is not null)
        {
            rawStartTicks = snapPolicy.Snap(rawStartTicks);
            rawEndTicks = snapPolicy.Snap(rawEndTicks);
        }

        // PHASE-04 Step 030: re-ordered with Min/Max *after* snapping (never before) — a drag whose two
        // raw endpoints straddle a single snap tie-break could otherwise produce an inverted window.
        var startTicks = Math.Min(rawStartTicks, rawEndTicks);
        var endTicks = Math.Max(rawStartTicks, rawEndTicks);
        var window = new TimeWindow(startTicks, endTicks);

        if (visibleRows.Count == 0)
        {
            return new EditScope(window, new HashSet<ModelId>(), RespectLocks: respectLocks);
        }

        var lo = Math.Clamp(Math.Min(rowIndexA, rowIndexB), 0, visibleRows.Count - 1);
        var hi = Math.Clamp(Math.Max(rowIndexA, rowIndexB), 0, visibleRows.Count - 1);

        var models = new HashSet<ModelId>();
        for (var i = lo; i <= hi; i++)
        {
            if (visibleRows[i].ModelId is { } modelId)
            {
                models.Add(modelId);
            }
        }

        return new EditScope(window, models, RespectLocks: respectLocks);
    }
}
