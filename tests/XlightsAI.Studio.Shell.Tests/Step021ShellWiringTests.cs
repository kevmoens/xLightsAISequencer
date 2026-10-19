using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-03 Step 021 ("Multi-row time-range selection with inclusive/exclusive boundary semantics")
/// — proves drag-rectangle range selection is wired end to end through the real
/// <see cref="ShellViewModel"/>/<see cref="TimelineViewModel"/>. Required acceptance: "Selected model
/// IDs and time range match UI; edge-case tests." Required failure/edge case: "Out-of-scope, locked
/// or stale revision must be rejected without partial mutation" — proven here by tying a real
/// shell-built <see cref="EditScope"/> into the existing, already-PASS
/// <see cref="EditTransactionService"/> rejection pipeline, same technique
/// <c>Step018ShellWiringTests</c> already used for single-effect selection.
/// </summary>
public sealed class Step021ShellWiringTests
{
    private static ShellViewModel CreateReadyShell()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        return shell;
    }

    private static DisplayGraph GateDemoLayout() => new(
        "TEST-HASH-NOT-A-REAL-FILE",
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", [], []),
        ],
        [],
        []);

    private static SequenceDocument GateDemoPlan(DisplayGraph layout) =>
        new ThreeEffectPlanFactory().CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], 300_000_000, new EffectRecipeRegistry());

    private static ShellViewModel LoadedShell(DisplayGraph layout, SequenceDocument plan)
    {
        var shell = CreateReadyShell();
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = plan.Effects.Max(e => e.EndTicks) + 10_000_000;
        shell.Timeline.LoadTimeline(layout, plan);
        return shell;
    }

    [Fact]
    public void Step021_Acceptance_Passes_SelectedModelIdsAndTimeRangeMatchUI_ThroughRealShell()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var shell = LoadedShell(layout, plan);
        // Rows are [Roofline, Arch 1, Arch 2] (ungrouped) — drag rows 0..1.
        var viewport = shell.Timeline.Viewport!;

        shell.Timeline.RangeSelectionStartRowIndex = 0;
        shell.Timeline.RangeSelectionEndRowIndex = 1;
        shell.Timeline.RangeSelectionStartPixel = 5d;
        shell.Timeline.RangeSelectionEndPixel = 200d;

        shell.TimelineApplyRangeSelectionCommand.Execute(null);

        var scope = shell.Timeline.SelectedRangeScope;
        Assert.NotNull(scope);
        Assert.Equal(new HashSet<ModelId> { new("Roofline"), new("Arch 1") }, scope!.Models);
        Assert.Equal(viewport.TimeFromX(5d), scope.Time.StartTicks);
        Assert.Equal(viewport.TimeFromX(200d), scope.Time.EndTicks);

        // "Display exact scope" — the real bindable summary string names the exact models and range.
        Assert.NotNull(shell.Timeline.SelectedRangeScopeSummary);
        Assert.Contains("Roofline", shell.Timeline.SelectedRangeScopeSummary);
        Assert.Contains("Arch 1", shell.Timeline.SelectedRangeScopeSummary);
        Assert.DoesNotContain("Arch 2", shell.Timeline.SelectedRangeScopeSummary);
    }

    [Fact]
    public void ReversedRowAndPixelOrder_ProducesTheSameScope_ThroughRealShell()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var shell = LoadedShell(layout, plan);

        shell.Timeline.RangeSelectionStartRowIndex = 2;
        shell.Timeline.RangeSelectionEndRowIndex = 0;
        shell.Timeline.RangeSelectionStartPixel = 300d;
        shell.Timeline.RangeSelectionEndPixel = 10d;
        shell.TimelineApplyRangeSelectionCommand.Execute(null);
        var reversedScope = shell.Timeline.SelectedRangeScope;

        shell.Timeline.RangeSelectionStartRowIndex = 0;
        shell.Timeline.RangeSelectionEndRowIndex = 2;
        shell.Timeline.RangeSelectionStartPixel = 10d;
        shell.Timeline.RangeSelectionEndPixel = 300d;
        shell.TimelineApplyRangeSelectionCommand.Execute(null);
        var forwardScope = shell.Timeline.SelectedRangeScope;

        Assert.Equal(forwardScope!.Models, reversedScope!.Models);
        Assert.Equal(forwardScope.Time, reversedScope.Time);
    }

    [Fact]
    public void ClearRangeSelection_ClearsScopeAndSummary()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var shell = LoadedShell(layout, plan);
        shell.Timeline.RangeSelectionEndRowIndex = 2;
        shell.Timeline.RangeSelectionEndPixel = 100d;
        shell.TimelineApplyRangeSelectionCommand.Execute(null);
        Assert.NotNull(shell.Timeline.SelectedRangeScope);

        shell.TimelineClearRangeSelectionCommand.Execute(null);

        Assert.Null(shell.Timeline.SelectedRangeScope);
        Assert.Null(shell.Timeline.SelectedRangeScopeSummary);
    }

    [Fact]
    public void NoMediaLoaded_ApplyRangeSelection_IsNoOp_SilenceEdgeCase()
    {
        var shell = CreateReadyShell();
        shell.NavigateToTimelineCommand.Execute(null);

        shell.Timeline.RangeSelectionEndRowIndex = 5;
        shell.Timeline.RangeSelectionEndPixel = 500d;
        shell.Timeline.ApplyRangeSelection();

        Assert.Null(shell.Timeline.SelectedRangeScope);
    }

    [Fact]
    public void ReloadingTimeline_ClearsAnyPriorRangeSelection()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var shell = LoadedShell(layout, plan);
        shell.Timeline.RangeSelectionEndRowIndex = 2;
        shell.Timeline.RangeSelectionEndPixel = 100d;
        shell.TimelineApplyRangeSelectionCommand.Execute(null);
        Assert.NotNull(shell.Timeline.SelectedRangeScope);

        shell.Timeline.LoadTimeline(layout, plan);

        Assert.Null(shell.Timeline.SelectedRangeScope);
    }

    /// <summary>
    /// Required failure/edge case, tied into the real command pipeline: a real shell-built range
    /// scope that deliberately excludes a row still has that row's effect rejected as out-of-scope
    /// when an edit is attempted through the real <see cref="EditTransactionService"/> — the same
    /// proof <c>Step021AcceptanceTests.DragExcludingARow_...</c> gives at the pure-domain layer, now
    /// through the real, UI-bound <see cref="TimelineViewModel"/> state.
    /// </summary>
    [Fact]
    public async Task ShellBuiltScope_ExcludingARow_EffectOnThatRow_IsRejectedOutOfScope_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var shell = LoadedShell(layout, plan);
        var viewport = shell.Timeline.Viewport!;
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");

        // Drag only across rows 0..1 (Roofline, Arch 1); Arch 2's row is deliberately excluded.
        shell.Timeline.RangeSelectionStartRowIndex = 0;
        shell.Timeline.RangeSelectionEndRowIndex = 1;
        shell.Timeline.RangeSelectionStartPixel = 0d;
        shell.Timeline.RangeSelectionEndPixel = viewport.XFromTime(plan.Effects.Max(e => e.EndTicks));
        shell.TimelineApplyRangeSelectionCommand.Execute(null);

        var scope = shell.Timeline.SelectedRangeScope!;
        Assert.DoesNotContain(arch2.Target, scope.Models);

        var command = new MoveEffect(new DocumentRevision(plan.Revision), scope, arch2.Id, arch2.StartTicks + 1_000_000);
        var service = new EditTransactionService(new EffectLockPolicy());

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
        Assert.Equal(arch2.StartTicks, plan.Effects.Single(e => e.Id == arch2.Id).StartTicks);
    }
}
