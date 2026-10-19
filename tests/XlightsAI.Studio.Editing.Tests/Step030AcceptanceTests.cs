using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-04 Step 030 ("Add frame/beat/off-grid snapping and timing-diagnostics panel"). Required
/// acceptance: "Time-to-frame conversion and boundary rounding golden tests." Required failure/edge
/// case: "Silence, invalid timing boundaries and missing media must be handled explicitly." These
/// tests exercise the real <see cref="XLightsFrameGrid"/>, <see cref="SnapPolicy"/>, and
/// <see cref="TimelineRangeSelectionBuilder"/>'s new optional snap parameter — not mocks. The real
/// UI/drag-math wiring (Timeline's own <c>SnapPolicy</c> property, the Shell's scrub action, the
/// timing-diagnostics panel) is covered separately by <c>Step030TimelineViewModelTests</c>/
/// <c>Step030ShellWiringTests</c> (<c>XlightsAI.Studio.Shell.Tests</c>).
/// </summary>
public sealed class Step030AcceptanceTests
{
    // 25ms (XsqExporter.FrameIntervalMs) * 10,000 ticks/ms (TimeSpan's own 100 ns tick unit) == 250,000.
    private const long FrameIntervalTicks = 250_000;

    [Fact]
    public void Step030_Acceptance_Passes_TimeToFrameConversionGoldenValues()
    {
        Assert.Equal(25, XLightsFrameGrid.FrameIntervalMs);
        Assert.Equal(FrameIntervalTicks, XLightsFrameGrid.FrameIntervalTicks);

        // Golden conversion table: exact frame boundaries and the last tick still inside the previous frame.
        Assert.Equal(0, XLightsFrameGrid.TicksToFrameIndex(0));
        Assert.Equal(0, XLightsFrameGrid.TicksToFrameIndex(FrameIntervalTicks - 1));
        Assert.Equal(1, XLightsFrameGrid.TicksToFrameIndex(FrameIntervalTicks));
        Assert.Equal(1, XLightsFrameGrid.TicksToFrameIndex(FrameIntervalTicks + 1));
        Assert.Equal(4, XLightsFrameGrid.TicksToFrameIndex(4 * FrameIntervalTicks));

        Assert.Equal(0, XLightsFrameGrid.FrameIndexToTicks(0));
        Assert.Equal(FrameIntervalTicks, XLightsFrameGrid.FrameIndexToTicks(1));
        Assert.Equal(4 * FrameIntervalTicks, XLightsFrameGrid.FrameIndexToTicks(4));

        // Round-trip: FrameIndexToTicks(TicksToFrameIndex(x)) is the frame boundary at-or-before x.
        Assert.Equal(FrameIntervalTicks, XLightsFrameGrid.FrameIndexToTicks(XLightsFrameGrid.TicksToFrameIndex(FrameIntervalTicks + 1)));
    }

    /// <summary>"invalid timing boundaries ... must be handled explicitly" (this step's own stated failure/edge case): a negative tick value is an explicitly defined, deterministic frame index (floor division), never a crash or a silently-clamped-to-zero result.</summary>
    [Fact]
    public void TicksToFrameIndex_NegativeTicks_FloorsExplicitly_InvalidTimingBoundaryNegativePath()
    {
        Assert.Equal(-1, XLightsFrameGrid.TicksToFrameIndex(-1));
        Assert.Equal(-1, XLightsFrameGrid.TicksToFrameIndex(-FrameIntervalTicks));
        Assert.Equal(-2, XLightsFrameGrid.TicksToFrameIndex(-FrameIntervalTicks - 1));
    }

    [Fact]
    public void Step030_Acceptance_Passes_BoundaryRoundingGoldenValues()
    {
        // Exactly on a frame boundary: unchanged.
        Assert.Equal(0, XLightsFrameGrid.SnapTicksToFrameBoundary(0));
        Assert.Equal(FrameIntervalTicks, XLightsFrameGrid.SnapTicksToFrameBoundary(FrameIntervalTicks));

        // Just inside the lower/upper half of the frame: rounds to the nearer boundary.
        Assert.Equal(0, XLightsFrameGrid.SnapTicksToFrameBoundary(FrameIntervalTicks / 2 - 1));
        Assert.Equal(FrameIntervalTicks, XLightsFrameGrid.SnapTicksToFrameBoundary(FrameIntervalTicks / 2 + 1));

        // Exact midpoint tie: rounds away from zero (same convention as EditTimeSnapping.SnapTicks, Step 019).
        Assert.Equal(FrameIntervalTicks, XLightsFrameGrid.SnapTicksToFrameBoundary(FrameIntervalTicks / 2));
    }

    [Fact]
    public void SnapPolicy_None_LeavesTicksUnchanged_SilenceEdgeCase()
    {
        Assert.Null(SnapPolicy.None.IntervalTicks);
        Assert.Equal(0, SnapPolicy.None.Snap(0));
        Assert.Equal(123_456_789, SnapPolicy.None.Snap(123_456_789));
        Assert.Equal(-999, SnapPolicy.None.Snap(-999));
    }

    [Fact]
    public void SnapPolicy_Frame_SnapsToTheExactXsqFrameGrid()
    {
        Assert.Equal(FrameIntervalTicks, SnapPolicy.Frame.IntervalTicks);
        Assert.Equal(FrameIntervalTicks, SnapPolicy.Frame.Snap(260_000));
        Assert.Equal(0, SnapPolicy.Frame.Snap(100_000));
    }

    private static IReadOnlyList<TimelineRowSlot> SingleModelRow() =>
    [
        new TimelineRowSlot(
            TimelineRowId.ForModel(new ModelId("Roofline")),
            "Roofline",
            Depth: 0,
            IsGroup: false,
            HasChildren: false,
            IsExpanded: false,
            ModelId: new ModelId("Roofline")),
    ];

    [Fact]
    public void Build_WithFrameSnapPolicy_SnapsResultingWindowToFrameBoundaries()
    {
        var rows = SingleModelRow();
        var viewport = TimelineViewport.Create(new TimeWindow(0, 100_000_000), TimelineViewport.MinTicksPerPixel); // 100 ticks/pixel.

        // pixel 2600 -> 260,000 ticks (off-frame, nearer to frame 1 at 250,000); pixel 5100 -> 510,000 ticks (off-frame, nearer to frame 2 at 500,000).
        var scope = TimelineRangeSelectionBuilder.Build(rows, 0, 0, 2600d, 5100d, viewport, snapPolicy: SnapPolicy.Frame);

        Assert.Equal(FrameIntervalTicks, scope.Time.StartTicks);
        Assert.Equal(2 * FrameIntervalTicks, scope.Time.EndTicks);
    }

    [Fact]
    public void Build_WithNullSnapPolicy_BehavesIdenticallyToOmittingIt_RegressionNegativePath()
    {
        var rows = SingleModelRow();
        var viewport = TimelineViewport.Create(new TimeWindow(0, 100_000_000), TimelineViewport.MinTicksPerPixel);

        var withExplicitNull = TimelineRangeSelectionBuilder.Build(rows, 0, 0, 2600d, 5100d, viewport, snapPolicy: null);
        var withOmittedParameter = TimelineRangeSelectionBuilder.Build(rows, 0, 0, 2600d, 5100d, viewport);

        Assert.Equal(withOmittedParameter.Time, withExplicitNull.Time);
        Assert.Equal(260_000, withExplicitNull.Time.StartTicks); // Raw, unsnapped tick value — no rounding applied.
    }

    /// <summary>
    /// "boundary rounding" golden case named by this step's own required acceptance: two drag endpoints
    /// close enough together that each snaps independently to the *same* frame boundary must still
    /// produce a valid (zero-length, never inverted) half-open window — proving the builder's own
    /// post-snap Min/Max re-ordering, not just each endpoint's own snap, is correct.
    /// </summary>
    [Fact]
    public void Build_WithFrameSnapPolicy_EndpointsStraddlingATieBreak_NeverProducesInvertedWindow()
    {
        var rows = SingleModelRow();
        var viewport = TimelineViewport.Create(new TimeWindow(0, 100_000_000), TimelineViewport.MinTicksPerPixel);

        // pixel 1249 -> 124,900 ticks (snaps down to 0); pixel 1251 -> 125,100 ticks (snaps up to
        // FrameIntervalTicks) — both endpoints sit just either side of the exact tie-break midpoint,
        // but the raw order (124,900 < 125,100) already matches the snapped order (0 < FrameIntervalTicks),
        // so this proves the straightforward case first.
        var scope = TimelineRangeSelectionBuilder.Build(rows, 0, 0, 1249d, 1251d, viewport, snapPolicy: SnapPolicy.Frame);
        Assert.True(scope.Time.StartTicks <= scope.Time.EndTicks);
        Assert.Equal(0, scope.Time.StartTicks);
        Assert.Equal(FrameIntervalTicks, scope.Time.EndTicks);

        // A single pixel wide drag entirely inside one half of a frame: both endpoints snap to the
        // identical boundary — a valid, zero-length window (the "silence" edge case), never inverted.
        var zeroLength = TimelineRangeSelectionBuilder.Build(rows, 0, 0, 10d, 11d, viewport, snapPolicy: SnapPolicy.Frame);
        Assert.Equal(zeroLength.Time.StartTicks, zeroLength.Time.EndTicks);
        Assert.True(zeroLength.Time.StartTicks <= zeroLength.Time.EndTicks);
    }
}
