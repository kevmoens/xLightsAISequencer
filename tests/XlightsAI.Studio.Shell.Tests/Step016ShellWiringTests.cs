namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-03 Step 016: proves <see cref="ShellViewModel.Timeline"/>/the Timeline navigation and
/// Load/Zoom/Scroll commands are wired end to end through the real <see cref="TimelineViewModel"/>
/// (not a stand-in) — required acceptance: "Pixel-to-time conversion tests at zoom extremes."
/// Required failure/edge case: "Silence, invalid timing boundaries and missing media must be
/// handled explicitly."
/// </summary>
public sealed class Step016ShellWiringTests
{
    private static ShellViewModel CreateReadyShell()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        return shell;
    }

    [Fact]
    public void Step016_Acceptance_Passes_NavigateAndLoadTimeline_BuildsUsableViewport()
    {
        var shell = CreateReadyShell();

        Assert.True(shell.NavigateToTimelineCommand.CanExecute(null));
        shell.NavigateToTimelineCommand.Execute(null);

        Assert.True(shell.IsShowingTimeline);
        // Missing media: navigating in never auto-loads a viewport (explicit empty state).
        Assert.True(shell.Timeline.IsEmpty);
        Assert.Null(shell.Timeline.Viewport);

        shell.Timeline.TotalDurationTicks = 50_000_000;
        Assert.True(shell.LoadTimelineCommand.CanExecute(null));
        shell.LoadTimelineCommand.Execute(null);

        Assert.True(shell.Timeline.IsLoaded);
        Assert.NotNull(shell.Timeline.Viewport);
        Assert.NotEmpty(shell.Timeline.RulerTicks);
    }

    [Fact]
    public void Step016_Acceptance_Passes_ZoomCommandsRespectExtremesThroughRealShell()
    {
        var shell = CreateReadyShell();
        shell.NavigateToTimelineCommand.Execute(null);
        shell.LoadTimelineCommand.Execute(null);

        // Zoom in repeatedly past the extreme.
        for (var i = 0; i < 64; i++)
        {
            shell.TimelineZoomInCommand.Execute(null);
        }

        Assert.Equal(Editing.TimelineViewport.MinTicksPerPixel, shell.Timeline.TicksPerPixel);

        // Zoom out repeatedly past the extreme.
        for (var i = 0; i < 64; i++)
        {
            shell.TimelineZoomOutCommand.Execute(null);
        }

        Assert.Equal(Editing.TimelineViewport.MaxTicksPerPixel, shell.Timeline.TicksPerPixel);
    }

    [Fact]
    public void Step016_Acceptance_Passes_ScrollCommandsClampToContentBounds()
    {
        var shell = CreateReadyShell();
        shell.Timeline.TotalDurationTicks = 1_000_000;
        shell.NavigateToTimelineCommand.Execute(null);
        shell.LoadTimelineCommand.Execute(null);

        for (var i = 0; i < 64; i++)
        {
            shell.TimelineScrollLeftCommand.Execute(null);
        }

        Assert.Equal(0L, shell.Timeline.Viewport!.ScrollOffsetTicks);

        for (var i = 0; i < 64; i++)
        {
            shell.TimelineScrollRightCommand.Execute(null);
        }

        Assert.Equal(1_000_000L, shell.Timeline.Viewport!.ScrollOffsetTicks);
    }

    [Fact]
    public void Step016_Acceptance_Passes_ZoomScrollCommands_DisabledWithoutLoadedMedia_NegativePath()
    {
        var shell = CreateReadyShell();
        shell.NavigateToTimelineCommand.Execute(null);

        // Missing media: zoom/scroll must not be executable (and are explicit no-ops even if
        // invoked directly against the view model) before Load Timeline has ever succeeded.
        Assert.False(shell.TimelineZoomInCommand.CanExecute(null));
        Assert.False(shell.TimelineZoomOutCommand.CanExecute(null));
        Assert.False(shell.TimelineScrollLeftCommand.CanExecute(null));
        Assert.False(shell.TimelineScrollRightCommand.CanExecute(null));

        shell.Timeline.ZoomIn();
        shell.Timeline.ScrollByPixels(100);
        Assert.Null(shell.Timeline.Viewport);
    }

    [Fact]
    public void Step016_Acceptance_Passes_InvalidTimingBoundary_SetsExplicitErrorState_NegativePath()
    {
        var shell = CreateReadyShell();
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = -1;

        shell.LoadTimelineCommand.Execute(null);

        Assert.True(shell.Timeline.IsError);
        Assert.False(shell.Timeline.IsLoaded);
        Assert.Null(shell.Timeline.Viewport);
        Assert.False(string.IsNullOrWhiteSpace(shell.Timeline.ErrorMessage));
    }
}
