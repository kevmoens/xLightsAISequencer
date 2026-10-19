using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// This step's own "time-to-frame conversion and boundary rounding" (Section 6, Step 030 required
/// acceptance): the real, reusable half of the xLights frame grid <see cref="XsqExporter"/> already
/// rounds every exported effect's start/end time to (<see cref="XsqExporter.FrameIntervalMs"/>) — pulled
/// out here as a pure, stateless conversion so this step's own new interactive snapping/diagnostics
/// surface (<see cref="SnapPolicy.Frame"/>, the Timeline's timing-diagnostics panel) can share the exact
/// same frame length the exporter itself uses, never a second, independently-chosen one. Section 2's own
/// architectural assumption: "Convert to xLights frames only when explicitly snapping or exporting" —
/// this type is the "explicitly snapping" half; <see cref="XsqExporter"/>'s own private export-time
/// rounding is the "exporting" half, both now reading the same <see cref="FrameIntervalMs"/> constant.
/// </summary>
public static class XLightsFrameGrid
{
    /// <summary>The xLights sequence frame grid, in milliseconds — identical to and sourced from <see cref="XsqExporter.FrameIntervalMs"/> (a compile-time constant, so this is not a copy that could silently drift out of sync).</summary>
    public const int FrameIntervalMs = XsqExporter.FrameIntervalMs;

    /// <summary>The frame grid expressed in this project's own canonical 100 ns ticks (<c>TimelineTick</c>'s own unit) — <c>25ms == 250,000 ticks</c> at the default <see cref="FrameIntervalMs"/>.</summary>
    public static readonly long FrameIntervalTicks = TimeSpan.FromMilliseconds(FrameIntervalMs).Ticks;

    /// <summary>
    /// The zero-based frame index <paramref name="ticks"/> falls within — <c>floor(ticks / FrameIntervalTicks)</c>,
    /// so every tick within a frame's own half-open span maps to that frame, including a negative
    /// <paramref name="ticks"/> (an explicitly handled invalid/out-of-bounds timing value, never a crash
    /// or a silently wrapped-positive result): e.g. <c>-1</c> maps to frame <c>-1</c>, not frame <c>0</c>.
    /// </summary>
    public static long TicksToFrameIndex(long ticks) => (long)Math.Floor(ticks / (double)FrameIntervalTicks);

    /// <summary>The inverse of <see cref="TicksToFrameIndex"/>: the exact tick a frame index begins at.</summary>
    public static long FrameIndexToTicks(long frameIndex) => frameIndex * FrameIntervalTicks;

    /// <summary>Rounds <paramref name="ticks"/> to the nearest frame-boundary tick (ties round away from zero, same convention as <see cref="EditTimeSnapping.SnapTicks"/>, which this delegates to).</summary>
    public static long SnapTicksToFrameBoundary(long ticks) => EditTimeSnapping.SnapTicks(ticks, FrameIntervalTicks);
}
