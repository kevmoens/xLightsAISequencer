using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-11 Step 073 -- "Implement deterministic command AST for brightness/motion/direction/speed/
/// replace." Required acceptance: "UI quick actions work offline and no token usage." Required
/// failure/edge case: "Invalid input, cancellation or stale persisted state must not corrupt the
/// project." Fast, deterministic tests for <see cref="StructuredCommandQuickActionsViewModel"/>'s own
/// bindable quick-action contract against a real <see cref="StructuredCommandInterpreter"/>/
/// <see cref="EffectLockPolicy"/>/<see cref="SelectionContextCaptureService"/> (not mocks). No real
/// SCREEN 14 AI EDIT Avalonia/AXAML view exists (the project's own pre-existing, consistent convention --
/// see <c>implementation-evidence/PHASE-11/STEP-072.md</c> §3/§7); this step's own required "Versioned,
/// inspectable project output plus a reproducible test report" outcome is proven at the bindable
/// view-model layer the real SCREEN 14 UI would bind to, the same precedent Step 072's own evidence
/// already established.
/// </summary>
public sealed class Step073StructuredCommandQuickActionsViewModelTests
{
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", [], []),
        ],
        Groups: [],
        Warnings: []);

    private static SequenceDocument GateDemoPlan(DisplayGraph layout) =>
        new ThreeEffectPlanFactory().CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], 300_000_000, new EffectRecipeRegistry());

    private static SelectionContext CaptureArchSelection(SequenceDocument plan, EffectInstance arch1, EffectInstance arch2)
    {
        var scope = new EditScope(
            new TimeWindow(Math.Min(arch1.StartTicks, arch2.StartTicks), Math.Max(arch1.EndTicks, arch2.EndTicks)),
            new HashSet<ModelId> { arch1.Target, arch2.Target });
        return new SelectionContextCaptureService(new EffectLockPolicy())
            .Capture(plan, new DocumentRevision(plan.Revision), scope, [], [], [], []);
    }

    [Fact]
    public async Task Step073_Acceptance_QuickActionWorksOfflineWithNoTokenUsage()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new StructuredCommandQuickActionsViewModel();

        Assert.True(vm.IsEmpty);

        var ok = await vm.ParseAsync(interpreter, "brighten", context, plan);

        Assert.True(ok);
        Assert.True(vm.IsLoaded);
        Assert.NotNull(vm.LastProposal);
        Assert.Equal(StructuredEditOperationKinds.SetBrightness, vm.LastProposal!.Operations[0].Kind);
        Assert.Equal("100", vm.LastProposal.Operations[0].Parameters["Level"]);
        Assert.Null(vm.ActionErrorMessage);
    }

    [Fact]
    public async Task UnrecognizedInstruction_IsRejected_LeavesLastProposalUnchanged_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new StructuredCommandQuickActionsViewModel();

        var firstOk = await vm.ParseAsync(interpreter, "brighten", context, plan);
        Assert.True(firstOk);
        var firstProposal = vm.LastProposal;

        var secondOk = await vm.ParseAsync(interpreter, "do something clever", context, plan);

        Assert.False(secondOk);
        // Still loaded (a prior successful proposal exists) and LastProposal is completely unchanged --
        // never a partial overwrite from the rejected attempt.
        Assert.True(vm.IsLoaded);
        Assert.Same(firstProposal, vm.LastProposal);
        Assert.NotNull(vm.ActionErrorMessage);
    }

    [Fact]
    public async Task StaleRevision_IsRejected_NeverLeavesEmptyState_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var staleDocument = plan with { Revision = plan.Revision + 1 };
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new StructuredCommandQuickActionsViewModel();

        var ok = await vm.ParseAsync(interpreter, "brighten", context, staleDocument);

        Assert.False(ok);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.LastProposal);
        Assert.NotNull(vm.ActionErrorMessage);
    }

    [Fact]
    public async Task ParseAsync_NullInterpreter_Throws_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var vm = new StructuredCommandQuickActionsViewModel();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => vm.ParseAsync(null!, "brighten", context, plan));
    }
}
