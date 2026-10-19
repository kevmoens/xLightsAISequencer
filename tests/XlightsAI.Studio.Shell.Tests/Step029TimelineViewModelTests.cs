using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 029 ("Add zoom-to-selection, A/B loop playback and keyboard navigation") — fast,
/// deterministic, hardware-free unit tests for <see cref="TimelineViewModel"/>'s own zoom-to-selection,
/// loop-window-validation, and auto-scroll contracts, independent of any real
/// <see cref="XlightsAI.Studio.Audio.IAudioTransport"/>. The real-device end-to-end slice (the loop
/// actually wrapping real playback) is covered separately by <c>Step029ShellWiringTests</c>
/// (Windows-only, real <c>winmm</c> transport).
/// </summary>
public sealed class Step029TimelineViewModelTests
{
    private static TimelineViewModel CreateLoadedTimeline(long totalDurationTicks = 100_000_000)
    {
        var timeline = new TimelineViewModel { TotalDurationTicks = totalDurationTicks };
        timeline.LoadTimeline();
        return timeline;
    }

    /// <summary>Builds a real <see cref="TimelineViewModel.SelectedRangeScope"/> spanning exactly <c>[0, endPixel)</c> at the default zoom (1,000,000 ticks/pixel) — the same real drag-rectangle path Step 021's own tests use, never a synthetic shortcut.</summary>
    private static void SelectRange(TimelineViewModel timeline, double startPixel, double endPixel)
    {
        timeline.RangeSelectionStartRowIndex = 0;
        timeline.RangeSelectionEndRowIndex = 0;
        timeline.RangeSelectionStartPixel = startPixel;
        timeline.RangeSelectionEndPixel = endPixel;
        timeline.ApplyRangeSelection();
    }

    [Fact]
    public void ZoomToSelection_FillsViewportWithTheSelectedWindow_ExactHalfOpenBoundary()
    {
        var timeline = CreateLoadedTimeline();
        // 10 pixels * 1,000,000 ticks/pixel == a real 1-second (10,000,000-tick) selection — the same
        // "loop 1 second" shape the phase-wide gate demonstration names.
        SelectRange(timeline, 0, 10);
        Assert.NotNull(timeline.SelectedRangeScope);
        var window = timeline.SelectedRangeScope!.Time;
        Assert.Equal(0, window.StartTicks);
        Assert.Equal(10_000_000, window.EndTicks);

        timeline.ZoomToSelection();

        Assert.NotNull(timeline.Viewport);
        var viewport = timeline.Viewport!;
        Assert.Equal(window.StartTicks, viewport.ScrollOffsetTicks);
        var expectedTicksPerPixel = (window.EndTicks - window.StartTicks) / timeline.ViewportWidthPixels;
        Assert.Equal(expectedTicksPerPixel, viewport.TicksPerPixel, precision: 6);

        // The selection now exactly fills the visible window (up to pixel rounding at the far edge).
        var visible = viewport.VisibleWindow(timeline.ViewportWidthPixels);
        Assert.Equal(window.StartTicks, visible.StartTicks);
    }

    [Fact]
    public void ZoomToSelection_NoSelection_IsNoOp_SilenceEdgeCase()
    {
        var timeline = CreateLoadedTimeline();
        var before = timeline.Viewport;

        timeline.ZoomToSelection();

        Assert.Equal(before!.TicksPerPixel, timeline.Viewport!.TicksPerPixel);
        Assert.Equal(before.ScrollOffsetTicks, timeline.Viewport!.ScrollOffsetTicks);
    }

    [Fact]
    public void ZoomToSelection_NoTimelineLoaded_IsNoOp_MissingMediaEdgeCase()
    {
        var timeline = new TimelineViewModel();

        timeline.ZoomToSelection();

        Assert.Null(timeline.Viewport);
    }

    [Fact]
    public void ValidateLoopWindow_NoViewportLoaded_ThrowsInvalidOperationException_StaleNegativePath()
    {
        var timeline = new TimelineViewModel();

        Assert.Throws<InvalidOperationException>(() => timeline.ValidateLoopWindow(new TimeWindow(0, 1_000_000)));
    }

    [Fact]
    public void ValidateLoopWindow_BeyondContentBounds_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 1_000_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ValidateLoopWindow(new TimeWindow(0, 2_000_000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ValidateLoopWindow(new TimeWindow(-1, 500_000)));
    }

    [Fact]
    public void ValidateLoopWindow_InvertedOrEmptyWindow_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 1_000_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ValidateLoopWindow(new TimeWindow(500_000, 500_000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ValidateLoopWindow(new TimeWindow(500_000, 100_000)));
    }

    [Fact]
    public void ValidateLoopWindow_WithinContentBounds_DoesNotThrow()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 1_000_000);

        timeline.ValidateLoopWindow(new TimeWindow(0, 1_000_000));
        timeline.ValidateLoopWindow(new TimeWindow(100_000, 500_000));
    }

    [Fact]
    public void SetLoop_UpdatesWindowAndDerivedProperties()
    {
        var timeline = CreateLoadedTimeline();
        var window = new TimeWindow(0, 10_000_000);

        timeline.SetLoop(window);

        Assert.True(timeline.HasLoop);
        Assert.Equal(window, timeline.LoopWindow);
        Assert.Equal("Loop: [0, 10000000)", timeline.LoopSummary);
    }

    [Fact]
    public void SetLoop_Null_ClearsLoop()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SetLoop(new TimeWindow(0, 10_000_000));
        Assert.True(timeline.HasLoop);

        timeline.SetLoop(null);

        Assert.False(timeline.HasLoop);
        Assert.Null(timeline.LoopWindow);
        Assert.Null(timeline.LoopSummary);
    }

    [Fact]
    public void Reset_ClearsLoopStateAndLoopErrorMessage()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SetLoop(new TimeWindow(0, 10_000_000));
        timeline.SetLoopError("some failure");

        timeline.Reset();

        Assert.False(timeline.HasLoop);
        Assert.Null(timeline.LoopWindow);
        Assert.Null(timeline.LoopErrorMessage);
    }

    [Fact]
    public void LoadTimeline_ClearsLoopStateAndLoopErrorMessage()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SetLoop(new TimeWindow(0, 10_000_000));
        timeline.SetLoopError("some failure");

        timeline.LoadTimeline();

        Assert.False(timeline.HasLoop);
        Assert.Null(timeline.LoopWindow);
        Assert.Null(timeline.LoopErrorMessage);
    }

    [Fact]
    public void SetPlayhead_PlayheadOutsideVisibleWindow_AutoScrollsWithoutChangingZoom()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 5_000_000_000);
        var ticksPerPixelBefore = timeline.Viewport!.TicksPerPixel;
        var visibleBefore = timeline.Viewport!.VisibleWindow(timeline.ViewportWidthPixels);

        // Well beyond the default-zoom visible window (760 pixels * 1,000,000 ticks/pixel).
        var farTicks = visibleBefore.EndTicks + 1_000_000_000;
        timeline.SetPlayhead(farTicks, isActive: true);

        Assert.Equal(ticksPerPixelBefore, timeline.Viewport!.TicksPerPixel); // zoom (TicksPerPixel) is unchanged — scroll, never zoom.
        var visibleAfter = timeline.Viewport!.VisibleWindow(timeline.ViewportWidthPixels);
        Assert.True(farTicks >= visibleAfter.StartTicks && farTicks < visibleAfter.EndTicks,
            $"Playhead {farTicks} is not within the auto-scrolled visible window [{visibleAfter.StartTicks}, {visibleAfter.EndTicks}).");
    }

    [Fact]
    public void SetPlayhead_AutoScrollWorksAtADifferentZoomLevel_IndependentOfZoom()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 5_000_000_000);
        timeline.ZoomIn();
        timeline.ZoomIn();
        var ticksPerPixelBefore = timeline.Viewport!.TicksPerPixel;
        var visibleBefore = timeline.Viewport!.VisibleWindow(timeline.ViewportWidthPixels);

        var farTicks = visibleBefore.EndTicks + 10_000_000;
        timeline.SetPlayhead(farTicks, isActive: true);

        Assert.Equal(ticksPerPixelBefore, timeline.Viewport!.TicksPerPixel);
        var visibleAfter = timeline.Viewport!.VisibleWindow(timeline.ViewportWidthPixels);
        Assert.True(farTicks >= visibleAfter.StartTicks && farTicks < visibleAfter.EndTicks,
            $"Playhead {farTicks} is not within the auto-scrolled visible window [{visibleAfter.StartTicks}, {visibleAfter.EndTicks}).");
    }

    [Fact]
    public void SetPlayhead_PlayheadWithinVisibleWindow_DoesNotScroll()
    {
        var timeline = CreateLoadedTimeline();
        var offsetBefore = timeline.Viewport!.ScrollOffsetTicks;

        timeline.SetPlayhead(1_000_000, isActive: true);

        Assert.Equal(offsetBefore, timeline.Viewport!.ScrollOffsetTicks);
    }

    [Fact]
    public void SetPlayhead_Inactive_DoesNotAutoScroll_EvenIfTicksAreFarOutsideVisibleWindow()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 5_000_000_000);
        var offsetBefore = timeline.Viewport!.ScrollOffsetTicks;

        timeline.SetPlayhead(4_000_000_000, isActive: false);

        Assert.Equal(offsetBefore, timeline.Viewport!.ScrollOffsetTicks);
        Assert.False(timeline.HasPlayhead);
    }
}
