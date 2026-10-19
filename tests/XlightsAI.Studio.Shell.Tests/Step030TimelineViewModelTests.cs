using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-04 Step 030 ("Add frame/beat/off-grid snapping and timing-diagnostics panel") — fast,
/// deterministic, hardware-free unit tests for <see cref="TimelineViewModel"/>'s own
/// <see cref="TimelineViewModel.SnapPolicy"/>, <see cref="TimelineViewModel.PlayheadFrameIndex"/>, and
/// scrub sync/drift telemetry contracts, independent of any real
/// <see cref="XlightsAI.Studio.Audio.IAudioTransport"/>. The real-device end-to-end slice (a real scrub
/// actually snapping and recording real telemetry) is covered separately by
/// <c>Step030ShellWiringTests</c> (Windows-only, real <c>winmm</c> transport).
/// </summary>
public sealed class Step030TimelineViewModelTests
{
    private static TimelineViewModel CreateLoadedTimeline(long totalDurationTicks = 100_000_000)
    {
        var timeline = new TimelineViewModel { TotalDurationTicks = totalDurationTicks };
        timeline.LoadTimeline();
        return timeline;
    }

    private static void SelectRange(TimelineViewModel timeline, double startPixel, double endPixel)
    {
        timeline.RangeSelectionStartRowIndex = 0;
        timeline.RangeSelectionEndRowIndex = 0;
        timeline.RangeSelectionStartPixel = startPixel;
        timeline.RangeSelectionEndPixel = endPixel;
        timeline.ApplyRangeSelection();
    }

    [Fact]
    public void SnapPolicy_DefaultsToNone_PreStep030BehaviorUnchanged()
    {
        var timeline = new TimelineViewModel();
        Assert.Equal(SnapPolicy.None, timeline.SnapPolicy);
    }

    [Fact]
    public void ApplyRangeSelection_DefaultSnapPolicy_RawOffFrameTicks_RegressionNegativePath()
    {
        // Default zoom is 1,000,000 ticks/pixel (TimelineViewModel.DefaultTicksPerPixel): pixel 0.26
        // -> 260,000 ticks — deliberately off the 250,000-tick xLights frame grid. With the default
        // SnapPolicy.None, this must pass through completely unchanged (every pre-Step-030 drag kept
        // its exact raw tick value; this step changes nothing unless a user explicitly opts in).
        var timeline = CreateLoadedTimeline();
        SelectRange(timeline, 0, 0.26);

        Assert.Equal(0, timeline.SelectedRangeScope!.Time.StartTicks);
        Assert.Equal(260_000, timeline.SelectedRangeScope!.Time.EndTicks);
    }

    [Fact]
    public void ApplyRangeSelection_FrameSnapPolicy_SnapsSelectedRangeScopeToFrameBoundaries()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SnapPolicy = SnapPolicy.Frame;

        // Same raw 260,000-tick drag as the regression test above — now snapped to the nearest
        // 250,000-tick frame boundary (260,000 / 250,000 == 1.04, rounds to frame 1 == 250,000).
        SelectRange(timeline, 0, 0.26);

        Assert.Equal(0, timeline.SelectedRangeScope!.Time.StartTicks);
        Assert.Equal(250_000, timeline.SelectedRangeScope!.Time.EndTicks);
    }

    [Fact]
    public void PlayheadFrameIndex_ReflectsPlayheadTicksViaXLightsFrameGrid()
    {
        var timeline = CreateLoadedTimeline();

        timeline.SetPlayhead(600_000, isActive: true); // < 1 frame (250,000 * 3 == 750,000) -> frame 2.

        Assert.Equal(2, timeline.PlayheadFrameIndex);
    }

    [Fact]
    public void RecordScrubTelemetry_SetsRequestedActualAndDriftTicks()
    {
        var timeline = CreateLoadedTimeline();
        Assert.Null(timeline.LastScrubRequestedTicks);
        Assert.Null(timeline.LastScrubActualTicks);
        Assert.Null(timeline.LastScrubDriftTicks);

        timeline.RecordScrubTelemetry(requestedTicks: 1_000_000, actualTicks: 1_000_125);

        Assert.Equal(1_000_000, timeline.LastScrubRequestedTicks);
        Assert.Equal(1_000_125, timeline.LastScrubActualTicks);
        Assert.Equal(125, timeline.LastScrubDriftTicks);
        Assert.Contains("drift 125 ticks", timeline.TimingDiagnosticsSummary);
    }

    [Fact]
    public void TimingDiagnosticsSummary_NoScrubYet_ReportsExplicitly_SilenceEdgeCase()
    {
        var timeline = CreateLoadedTimeline();

        Assert.Contains("n/a (no scrub yet)", timeline.TimingDiagnosticsSummary);
        Assert.Contains("Snap: None", timeline.TimingDiagnosticsSummary);
    }

    [Fact]
    public void TimingDiagnosticsSummary_FrameSnapPolicy_NamesTheFrameGrid()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SnapPolicy = SnapPolicy.Frame;

        Assert.Contains("Frame (25 ms", timeline.TimingDiagnosticsSummary);
    }

    [Fact]
    public void Reset_ClearsScrubTelemetry_ButPreservesSnapPolicy_UserPreferenceNotSessionState()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SnapPolicy = SnapPolicy.Frame;
        timeline.RecordScrubTelemetry(1_000_000, 1_000_125);

        timeline.Reset();

        Assert.Null(timeline.LastScrubRequestedTicks);
        Assert.Null(timeline.LastScrubActualTicks);
        Assert.Null(timeline.LastScrubDriftTicks);
        Assert.Equal(SnapPolicy.Frame, timeline.SnapPolicy); // Not a playback-session value — survives reset.
    }

    [Fact]
    public void LoadTimeline_ClearsScrubTelemetry_ButPreservesSnapPolicy_UserPreferenceNotSessionState()
    {
        var timeline = CreateLoadedTimeline();
        timeline.SnapPolicy = SnapPolicy.Frame;
        timeline.RecordScrubTelemetry(1_000_000, 1_000_125);

        timeline.LoadTimeline();

        Assert.Null(timeline.LastScrubRequestedTicks);
        Assert.Null(timeline.LastScrubActualTicks);
        Assert.Equal(SnapPolicy.Frame, timeline.SnapPolicy);
    }
}
