using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-03 Step 018 ("Render effect blocks and inspector selection") — proves effect-block
/// rendering, click selection, keyboard navigation and the inspector are wired end to end through the
/// real <see cref="ShellViewModel"/>/<see cref="TimelineViewModel"/> against an already-generated
/// <see cref="SequenceDocument"/> (<see cref="ShowImportViewModel.Plan"/>). Required acceptance:
/// "Selection maps exactly to effect ID and time span." Required failure/edge case: "Out-of-scope,
/// locked or stale revision must be rejected without partial mutation" — proven at this step's own
/// selection/inspector layer by tying a real selected, locked effect into the existing, already-PASS
/// <see cref="EditTransactionService"/>/<see cref="EffectLockPolicy"/> rejection pipeline: selecting a
/// locked effect must never itself mutate anything, and a forbidden edit attempt against it is still
/// rejected.
/// </summary>
public sealed class Step018ShellWiringTests
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
    public void Step018_Acceptance_Passes_RowsRenderRealEffectBlocks_MappedToExactEffectId()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var shell = LoadedShell(layout, plan);

        var arch1Row = shell.Timeline.Rows.Single(r => r.ModelId == arch1.Target);
        var layerRow = Assert.Single(arch1Row.EffectLayers);
        var block = Assert.Single(layerRow.Blocks);

        Assert.Equal(arch1.Id, block.EffectId);
        Assert.False(block.IsSelected);
        Assert.False(block.IsLocked);
    }

    [Fact]
    public void Step018_Acceptance_Passes_ClickingABlock_SelectsExactlyThatEffectId()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var shell = LoadedShell(layout, plan);

        var arch1Row = shell.Timeline.Rows.Single(r => r.ModelId == arch1.Target);
        var block = arch1Row.EffectLayers.Single().Blocks.Single();

        block.SelectCommand.Execute(null);

        Assert.Equal(arch1.Id, shell.Timeline.SelectedEffectId);
        Assert.NotNull(shell.Timeline.SelectedEffectInspector);
        Assert.Equal(arch1.Id, shell.Timeline.SelectedEffectInspector!.EffectId);
        Assert.Equal(arch1.Target.Value, shell.Timeline.SelectedEffectInspector.TargetModelName);
        Assert.Equal(arch1.StartTicks, shell.Timeline.SelectedEffectInspector.StartTicks);
        Assert.Equal(arch1.EndTicks, shell.Timeline.SelectedEffectInspector.EndTicks);

        // Re-querying Rows after selection marks exactly the selected block, never any other.
        var rooflineRow = shell.Timeline.Rows.Single(r => r.ModelId == roofline.Target);
        Assert.True(shell.Timeline.Rows.Single(r => r.ModelId == arch1.Target).EffectLayers.Single().Blocks.Single().IsSelected);
        Assert.False(rooflineRow.EffectLayers.Single().Blocks.Single().IsSelected);
    }

    [Fact]
    public void Step018_Acceptance_Passes_KeyboardNavigation_CyclesThroughEveryEffect_WrapsAround()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var shell = LoadedShell(layout, plan);

        var orderedIds = shell.Timeline.Rows
            .SelectMany(r => r.EffectLayers)
            .SelectMany(l => l.Blocks)
            .Select(b => b.EffectId)
            .ToList();
        Assert.Equal(3, orderedIds.Count);

        shell.TimelineSelectNextEffectCommand.Execute(null);
        Assert.Equal(orderedIds[0], shell.Timeline.SelectedEffectId);

        shell.TimelineSelectNextEffectCommand.Execute(null);
        Assert.Equal(orderedIds[1], shell.Timeline.SelectedEffectId);

        shell.TimelineSelectNextEffectCommand.Execute(null);
        Assert.Equal(orderedIds[2], shell.Timeline.SelectedEffectId);

        // Wraps back to the first.
        shell.TimelineSelectNextEffectCommand.Execute(null);
        Assert.Equal(orderedIds[0], shell.Timeline.SelectedEffectId);

        // And backward wraps to the last.
        shell.TimelineSelectPreviousEffectCommand.Execute(null);
        Assert.Equal(orderedIds[2], shell.Timeline.SelectedEffectId);

        shell.TimelineClearSelectionCommand.Execute(null);
        Assert.Null(shell.Timeline.SelectedEffectId);
        Assert.Null(shell.Timeline.SelectedEffectInspector);
    }

    [Fact]
    public void SelectingUnknownEffectId_IsNoOp_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var shell = LoadedShell(layout, plan);

        shell.Timeline.SelectEffect(Guid.NewGuid());

        Assert.Null(shell.Timeline.SelectedEffectId);
    }

    [Fact]
    public void NoDocumentLoaded_RowsHaveNoEffectBlocks_KeyboardNavigationIsNoOp_SilenceEdgeCase()
    {
        var shell = CreateReadyShell();
        shell.NavigateToTimelineCommand.Execute(null);
        shell.Timeline.TotalDurationTicks = 1_000_000;
        // Load with a layout but no plan yet — rows exist (Step 017), but no effect blocks (this step).
        shell.Timeline.LoadTimeline(GateDemoLayout());

        Assert.True(shell.Timeline.IsLoaded);
        Assert.All(shell.Timeline.Rows, r => Assert.Empty(r.EffectLayers));

        shell.TimelineSelectNextEffectCommand.Execute(null);
        Assert.Null(shell.Timeline.SelectedEffectId);
    }

    /// <summary>
    /// Required failure/edge case, tied into the real command pipeline: a selected, locked effect is
    /// still fully visible/selectable (never hidden — "out-of-scope... must be rejected", not "must
    /// be invisible"), but any actual edit attempt against it through the real
    /// <see cref="EditTransactionService"/> is still rejected without partial mutation, exactly as
    /// Steps 019/020 already proved for the non-UI path.
    /// </summary>
    [Fact]
    public async Task SelectedLockedEffect_IsVisible_ButForbiddenEditIsStillRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var lockedPlan = plan with { Effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList() };
        var shell = LoadedShell(layout, lockedPlan);

        var arch2Row = shell.Timeline.Rows.Single(r => r.ModelId == arch2.Target);
        var block = arch2Row.EffectLayers.Single().Blocks.Single();
        Assert.True(block.IsLocked);

        block.SelectCommand.Execute(null);
        Assert.Equal(arch2.Id, shell.Timeline.SelectedEffectId);
        Assert.True(shell.Timeline.SelectedEffectInspector!.IsLocked);

        var service = new EditTransactionService(new EffectLockPolicy());
        var scope = new EditScope(new TimeWindow(0, long.MaxValue), lockedPlan.Effects.Select(e => e.Target).ToHashSet());
        var command = new MoveEffect(new DocumentRevision(lockedPlan.Revision), scope, arch2.Id, arch2.StartTicks + 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(lockedPlan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
        // No partial mutation: the document in the shell's own loaded plan is untouched.
        Assert.Equal(arch2.StartTicks, lockedPlan.Effects.Single(e => e.Id == arch2.Id).StartTicks);
    }
}
