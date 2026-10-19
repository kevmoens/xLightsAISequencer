using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-11 Step 074 -- "Add edit request panel, suggestions and change explanation." Required
/// acceptance: "Request always displays target context." Required failure/edge case: "Out-of-scope,
/// locked or stale revision must be rejected without partial mutation." Fast, deterministic tests for
/// <see cref="AiEditRequestPanelViewModel"/>'s own bindable request-panel contract against a real
/// <see cref="StructuredCommandInterpreter"/>/<see cref="EffectLockPolicy"/>/
/// <see cref="SelectionContextCaptureService"/> (not mocks). No real SCREEN 14 AI EDIT Avalonia/AXAML
/// view exists (the project's own pre-existing, consistent convention -- see
/// <c>implementation-evidence/PHASE-11/STEP-072.md</c> and <c>STEP-073.md</c> §3/§7); this step's own
/// required "Versioned, inspectable project output plus a reproducible test report" outcome is proven at
/// the bindable view-model layer the real SCREEN 14 UI would bind to.
/// </summary>
public sealed class Step074AiEditRequestPanelViewModelTests
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

    /// <summary>
    /// The required acceptance, verbatim: attaching a real captured selection immediately shows the
    /// affected props and exact time range -- before any instruction has been typed or submitted.
    /// </summary>
    [Fact]
    public void Step074_Acceptance_RequestAlwaysDisplaysTargetContext()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var vm = new AiEditRequestPanelViewModel();

        Assert.True(vm.IsEmpty);
        Assert.Equal(string.Empty, vm.TargetSummary);

        vm.AttachContext(context);

        Assert.True(vm.IsLoaded);
        Assert.Contains("Arch 1", vm.TargetSummary, StringComparison.Ordinal);
        Assert.Contains("Arch 2", vm.TargetSummary, StringComparison.Ordinal);
        Assert.Contains("10s", vm.TargetSummary, StringComparison.Ordinal);
        Assert.Contains("30s", vm.TargetSummary, StringComparison.Ordinal);
        // No instruction typed or submitted yet -- the target context is already fully displayed.
        Assert.Equal(string.Empty, vm.InstructionText);
        Assert.Null(vm.LastProposal);
        Assert.Null(vm.Explanation);
    }

    [Fact]
    public void SuggestionsAreFixedAndMatchKnownOfflineGrammar()
    {
        Assert.NotEmpty(AiEditRequestPanelViewModel.Suggestions);
        Assert.Contains("brighten", AiEditRequestPanelViewModel.Suggestions);
        Assert.Contains("snap to grid", AiEditRequestPanelViewModel.Suggestions);
    }

    /// <summary>"do not send until explicit action" (implementation guidance, verbatim): typing alone never reaches the interpreter.</summary>
    [Fact]
    public void SettingInstructionText_NeverInvokesInterpreter_UntilSubmitAsync()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);

        vm.InstructionText = "make the piano move left-to-right";
        vm.InstructionText = "brighten";

        Assert.Null(vm.LastProposal);
        Assert.Null(vm.ActionErrorMessage);
        Assert.Equal(3, plan.Effects.Count);
    }

    [Fact]
    public async Task Step074_SubmitAsync_AcceptedInstructionPopulatesProposalAndExplanation()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        var targetSummaryBeforeSubmit = vm.TargetSummary;
        vm.InstructionText = "brightness 75";

        var ok = await vm.SubmitAsync(interpreter, plan);

        Assert.True(ok);
        Assert.NotNull(vm.LastProposal);
        Assert.Equal(StructuredEditOperationKinds.SetBrightness, vm.LastProposal!.Operations[0].Kind);
        Assert.False(string.IsNullOrWhiteSpace(vm.Explanation));
        Assert.Null(vm.ActionErrorMessage);
        // Target context display is unaffected by submitting a request.
        Assert.Equal(targetSummaryBeforeSubmit, vm.TargetSummary);
    }

    [Fact]
    public async Task UnrecognizedInstruction_IsRejected_LeavesTargetContextAndProposalUnchanged_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        var targetSummaryBeforeSubmit = vm.TargetSummary;
        vm.InstructionText = "make the piano move left-to-right";

        var ok = await vm.SubmitAsync(interpreter, plan);

        Assert.False(ok);
        Assert.Null(vm.LastProposal);
        Assert.Null(vm.Explanation);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Equal(targetSummaryBeforeSubmit, vm.TargetSummary);
        Assert.Equal(3, plan.Effects.Count);
    }

    [Fact]
    public async Task StaleRevision_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var staleDocument = plan with { Revision = plan.Revision + 1 };
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        var targetSummaryBeforeSubmit = vm.TargetSummary;
        vm.InstructionText = "brighten";

        var ok = await vm.SubmitAsync(interpreter, staleDocument);

        Assert.False(ok);
        Assert.Null(vm.LastProposal);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Equal(targetSummaryBeforeSubmit, vm.TargetSummary);
    }

    [Fact]
    public async Task LockedTarget_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "brighten";

        var ok = await vm.SubmitAsync(interpreter, planWithLock);

        Assert.False(ok);
        Assert.Null(vm.LastProposal);
        Assert.NotNull(vm.ActionErrorMessage);
    }

    [Fact]
    public async Task SubmitAsync_WithoutAttachedContext_Throws_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new AiEditRequestPanelViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.SubmitAsync(interpreter, plan));
    }

    [Fact]
    public async Task SubmitAsync_NullArguments_Throw_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => vm.SubmitAsync(null!, plan));
        await Assert.ThrowsAsync<ArgumentNullException>(() => vm.SubmitAsync(interpreter, null!));
    }

    [Fact]
    public void AttachContext_Null_Throws_NegativePath()
    {
        var vm = new AiEditRequestPanelViewModel();
        Assert.Throws<ArgumentNullException>(() => vm.AttachContext(null!));
    }

    /// <summary>Re-attaching a new context after a successful submit resets instruction/proposal/error -- a stale request from a previous selection is never confused with the newly attached one.</summary>
    [Fact]
    public async Task AttachContext_AfterSubmit_ResetsInstructionAndProposal()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "brighten";
        await vm.SubmitAsync(interpreter, plan);
        Assert.NotNull(vm.LastProposal);

        vm.AttachContext(context);

        Assert.Equal(string.Empty, vm.InstructionText);
        Assert.Null(vm.LastProposal);
        Assert.Null(vm.ActionErrorMessage);
    }
}
