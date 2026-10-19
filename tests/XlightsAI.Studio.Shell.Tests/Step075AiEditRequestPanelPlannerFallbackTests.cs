using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-11 Step 075's own UI-facing slice: <see cref="AiEditRequestPanelViewModel"/>'s new
/// <see cref="AiEditRequestPanelViewModel.SubmitAsync(IStructuredEditInterpreter, IOptionalAiPlanner?, SequenceDocument, CancellationToken)"/>
/// overload -- Section 3's own flowchart, verbatim: "Known deterministic command? Yes -> Local
/// structured interpreter... No -> Optional model configured + consent? No -> Explain manual
/// alternatives; Yes -> Request structured AI proposal." Uses the real
/// <see cref="StructuredCommandInterpreter"/> plus a small in-memory <see cref="IOptionalAiPlanner"/>
/// test double (not a mocking library, matching every prior Shell/AiEdits test in this repository) to
/// prove the routing without a real network call.
/// </summary>
public sealed class Step075AiEditRequestPanelPlannerFallbackTests
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

    private static (SequenceDocument Plan, SelectionContext Context) BuildGateDemoSelection()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        return (plan, CaptureArchSelection(plan, arch1, arch2));
    }

    [Fact]
    public async Task KnownCommand_NeverConsultsPlanner_EvenWhenOneIsProvided()
    {
        var (plan, context) = BuildGateDemoSelection();
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "brighten";
        var planner = new RecordingPlanner(_ => throw new InvalidOperationException("The local interpreter already recognized this instruction; the planner must never be consulted."));

        var ok = await vm.SubmitAsync(new StructuredCommandInterpreter(new EffectLockPolicy()), planner, plan, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(0, planner.CallCount);
        Assert.NotNull(vm.LastProposal);
        Assert.Equal(StructuredEditOperationKinds.SetBrightness, Assert.Single(vm.LastProposal!.Operations).Kind);
    }

    /// <summary>Section 3's own flowchart: unrecognized instruction + a configured planner -> "Request structured AI proposal."</summary>
    [Fact]
    public async Task UnrecognizedInstruction_WithPlannerConfigured_FallsBackToPlanner()
    {
        var (plan, context) = BuildGateDemoSelection();
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "make the piano move left-to-right";

        var aiOperation = new StructuredEditOperation(
            "ai-op-1",
            StructuredEditOperationKinds.SetDirection,
            context.Scope.Time,
            context.Scope.Models.ToArray(),
            new Dictionary<string, string> { ["Direction"] = "LeftToRight" });
        var aiProposal = new EditProposal(Guid.NewGuid(), plan.Revision, [aiOperation], "Follow the piano, left to right.");
        var planner = new RecordingPlanner(_ => Task.FromResult(aiProposal));

        var ok = await vm.SubmitAsync(new StructuredCommandInterpreter(new EffectLockPolicy()), planner, plan, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(1, planner.CallCount);
        Assert.Same(aiProposal, vm.LastProposal);
        Assert.Equal("Follow the piano, left to right.", vm.Explanation);
        Assert.Null(vm.ActionErrorMessage);
    }

    /// <summary>Section 3's own flowchart, "No -> Explain manual alternatives": no planner configured, unrecognized instruction is rejected exactly as Step 074 already proved, with no exception leaking.</summary>
    [Fact]
    public async Task UnrecognizedInstruction_WithNoPlannerConfigured_ExplainsManualAlternatives_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "make the piano move left-to-right";

        var ok = await vm.SubmitAsync(new StructuredCommandInterpreter(new EffectLockPolicy()), planner: null, plan, CancellationToken.None);

        Assert.False(ok);
        Assert.Null(vm.LastProposal);
        Assert.NotNull(vm.ActionErrorMessage);
    }

    [Fact]
    public async Task UnrecognizedInstruction_WithDisabledProvider_ShowsManualAlternativesMessage_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "make the piano move left-to-right";
        var disabledPlanner = OptionalAiPlannerFactory.Create(AiProviderOptions.Disabled);

        var ok = await vm.SubmitAsync(new StructuredCommandInterpreter(new EffectLockPolicy()), disabledPlanner, plan, CancellationToken.None);

        Assert.False(ok);
        Assert.Null(vm.LastProposal);
        Assert.Contains("No optional AI provider is enabled", vm.ActionErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlannerRejectsProposal_SurfacesErrorMessage_NoMutation_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "make the piano move left-to-right";
        var planner = new RecordingPlanner(_ => throw new AiProposalRejectedException(
            AiProposalRejectionReason.MalformedJson,
            "AI provider returned JSON that could not be parsed."));

        var ok = await vm.SubmitAsync(new StructuredCommandInterpreter(new EffectLockPolicy()), planner, plan, CancellationToken.None);

        Assert.False(ok);
        Assert.Null(vm.LastProposal);
        Assert.Equal("AI provider returned JSON that could not be parsed.", vm.ActionErrorMessage);
    }

    /// <summary>A locked/stale rejection from the interpreter must never fall through to the planner, even when one is configured.</summary>
    [Fact]
    public async Task LockedTarget_NeverConsultsPlanner_NegativePath()
    {
        var (plan, context) = BuildGateDemoSelection();
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };

        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "brighten";
        var planner = new RecordingPlanner(_ => throw new InvalidOperationException("A locked-target rejection must never fall back to the planner."));

        var ok = await vm.SubmitAsync(new StructuredCommandInterpreter(new EffectLockPolicy()), planner, planWithLock, CancellationToken.None);

        Assert.False(ok);
        Assert.Equal(0, planner.CallCount);
    }

    /// <summary>The pre-existing 3-argument overload (Step 074) keeps behaving exactly as before -- no migration required for existing callers.</summary>
    [Fact]
    public async Task PreExisting3ArgumentOverload_StillWorksUnchanged()
    {
        var (plan, context) = BuildGateDemoSelection();
        var vm = new AiEditRequestPanelViewModel();
        vm.AttachContext(context);
        vm.InstructionText = "brighten";

        var ok = await vm.SubmitAsync(new StructuredCommandInterpreter(new EffectLockPolicy()), plan, CancellationToken.None);

        Assert.True(ok);
        Assert.NotNull(vm.LastProposal);
    }

    private sealed class RecordingPlanner : IOptionalAiPlanner
    {
        private readonly Func<string, Task<EditProposal>> _respond;

        public RecordingPlanner(Func<string, Task<EditProposal>> respond)
        {
            _respond = respond;
        }

        public int CallCount { get; private set; }

        public Task<EditProposal> ProposeAsync(string instruction, SelectionContext context, SequenceDocument document, CancellationToken ct = default)
        {
            CallCount++;
            return _respond(instruction);
        }
    }
}
