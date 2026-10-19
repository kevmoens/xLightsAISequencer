namespace XlightsAI.Studio.Editing;

/// <summary>
/// The snap targets this step ("Add frame/beat/off-grid snapping...") actually ships: <see cref="None"/>
/// (off-grid, the pre-Step-030 default — every existing drag/scrub call site keeps behaving identically)
/// and <see cref="Frame"/> (the xLights export frame grid, see <see cref="XLightsFrameGrid"/>). "Later
/// beat/note" (this step's own implementation guidance) names PHASE-05's <c>BeatGrid</c> and PHASE-07's
/// <c>MelodyTrack</c>/<c>NoteEvent</c> — neither exists yet in this repository, so no <c>Beat</c>/<c>Note</c>
/// member is added here; <see cref="SnapPolicy"/> itself (an open <see cref="SnapMode"/> plus a plain
/// <see cref="IntervalTicks"/>) is the extension point a future phase adds to, not a type this step
/// needs to pre-guess the shape of.
/// </summary>
public enum SnapMode
{
    None,
    Frame,
}

/// <summary>
/// This step's own "snap policy... as a dependency not baked into drag math" (Section 6, Step 030
/// implementation guidance): a plain, stateless value a caller passes into a drag/scrub call site —
/// <see cref="TimelineRangeSelectionBuilder.Build"/>'s new optional parameter, and
/// <c>ShellViewModel</c>'s scrub action — rather than a hardcoded frame-rounding call baked into either
/// one. <see cref="Snap"/> simply delegates to the existing, already-tested
/// <see cref="EditTimeSnapping.SnapTicks"/> (Step 019) — this type's only real new contribution is naming
/// <em>which</em> interval a caller means ("no snap" vs. "the xLights frame grid") without that caller
/// needing to know the frame grid's own tick value.
/// </summary>
public sealed record SnapPolicy(SnapMode Mode, long? IntervalTicks)
{
    /// <summary>No snapping — a drag/scrub target passes through unchanged. The default every existing call site keeps using unless it explicitly opts into a different policy.</summary>
    public static readonly SnapPolicy None = new(SnapMode.None, null);

    /// <summary>Snaps to the nearest multiple of <see cref="XLightsFrameGrid.FrameIntervalTicks"/> — the exact frame grid <c>XsqExporter</c> itself rounds effect start/end times to at export time (never a second, independently-chosen frame length).</summary>
    public static readonly SnapPolicy Frame = new(SnapMode.Frame, XLightsFrameGrid.FrameIntervalTicks);

    /// <summary>Rounds <paramref name="ticks"/> per this policy's own <see cref="IntervalTicks"/> (unchanged for <see cref="None"/>) via <see cref="EditTimeSnapping.SnapTicks"/>.</summary>
    public long Snap(long ticks) => EditTimeSnapping.SnapTicks(ticks, IntervalTicks);
}
