namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 028 — fast, deterministic, hardware-free unit tests for
/// <see cref="TimelineViewModel"/>'s own playhead/scrub-validation contract, independent of any real
/// <see cref="XlightsAI.Studio.Audio.IAudioTransport"/>. The real-device end-to-end slice is covered
/// separately by <c>Step028ShellWiringTests</c> (Windows-only, real <c>winmm</c> transport).
/// </summary>
public sealed class Step028TimelineViewModelTests
{
    private static TimelineViewModel CreateLoadedTimeline(long totalDurationTicks = 100_000_000)
    {
        var timeline = new TimelineViewModel { TotalDurationTicks = totalDurationTicks };
        timeline.LoadTimeline();
        return timeline;
    }

    [Fact]
    public void SetPlayhead_UpdatesTicksAndVisibilityDerivedProperties()
    {
        var timeline = CreateLoadedTimeline();

        timeline.SetPlayhead(12_345_678, isActive: true);

        Assert.Equal(12_345_678, timeline.PlayheadTicks);
        Assert.True(timeline.HasPlayhead);
        Assert.True(timeline.IsPlayheadVisible);
        Assert.NotNull(timeline.PlayheadX);
        Assert.Equal(timeline.Viewport!.XFromTime(12_345_678), timeline.PlayheadX!.Value, precision: 6);
    }

    [Fact]
    public void IsPlayheadVisible_FalseWithoutLoadedViewport_EvenIfHasPlayheadIsTrue()
    {
        var timeline = new TimelineViewModel();
        timeline.SetPlayhead(500, isActive: true);

        Assert.True(timeline.HasPlayhead);
        Assert.Null(timeline.PlayheadX);
        Assert.False(timeline.IsPlayheadVisible);
        // Missing media must be handled explicitly: no viewport means no real pixel position, but
        // PlayheadMarginLeft must still be a well-defined, non-negative value for a plain binding.
        Assert.Equal(0d, timeline.PlayheadMarginLeft);
    }

    [Fact]
    public void ValidateScrubTarget_NoViewportLoaded_ThrowsInvalidOperationException_StaleNegativePath()
    {
        var timeline = new TimelineViewModel();

        Assert.Throws<InvalidOperationException>(() => timeline.ValidateScrubTarget(0));
    }

    [Fact]
    public void ValidateScrubTarget_BeyondContentBounds_ThrowsArgumentOutOfRangeException_OutOfScopeNegativePath()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 1_000_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ValidateScrubTarget(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ValidateScrubTarget(1_000_001));
    }

    [Fact]
    public void ValidateScrubTarget_WithinContentBounds_DoesNotThrow()
    {
        var timeline = CreateLoadedTimeline(totalDurationTicks: 1_000_000);

        timeline.ValidateScrubTarget(0);
        timeline.ValidateScrubTarget(500_000);
        timeline.ValidateScrubTarget(1_000_000);
    }

    [Fact]
    public void SetPlayhead_RepeatedCallsWithSameTicks_AreIdempotent_NoAccumulation()
    {
        var timeline = CreateLoadedTimeline();

        for (var i = 0; i < 10; i++)
        {
            timeline.SetPlayhead(42_000_000, isActive: true);
            Assert.Equal(42_000_000, timeline.PlayheadTicks);
        }
    }

    [Fact]
    public void Reset_ClearsScrubErrorMessage()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SetScrubError("some failure");
        Assert.NotNull(timeline.ScrubErrorMessage);

        timeline.Reset();

        Assert.Null(timeline.ScrubErrorMessage);
    }

    [Fact]
    public void LoadTimeline_ClearsScrubErrorMessage()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SetScrubError("some failure");

        timeline.LoadTimeline();

        Assert.Null(timeline.ScrubErrorMessage);
    }
}
