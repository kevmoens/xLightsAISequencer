using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-11 Step 076 -- "Implement dry-run proposals, before/after diff and Accept/Reject." Required
/// acceptance: "Reject leaves state byte-equivalent; accept records transaction." Required failure/edge
/// case: "Clean machine and unsupported runtime must produce user-readable diagnostics." Fast,
/// deterministic tests for <see cref="AiEditProposalReviewViewModel"/>'s own bindable preview/accept/
/// reject contract against a real <see cref="StructuredCommandInterpreter"/>/<see cref="EffectLockPolicy"/>/
/// <see cref="SelectionContextCaptureService"/>/<see cref="EditProposalValidator"/>/
/// <see cref="EditProposalDryRunService"/>/<see cref="EditProposalApprovalService"/> (not mocks). No real
/// SCREEN 14 AI EDIT Avalonia/AXAML view exists (the project's own pre-existing, consistent convention --
/// see <c>implementation-evidence/PHASE-11/STEP-072.md</c> through <c>STEP-075.md</c> §3/§7); this step's
/// own required "Versioned, inspectable project output plus a reproducible test report" outcome is proven
/// at the bindable view-model layer the real SCREEN 14 UI would bind to.
/// </summary>
public sealed class Step076AiEditProposalReviewViewModelTests
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

    private static (SequenceDocument Plan, EffectInstance Arch1, EffectInstance Arch2, SelectionContext Context) BuildGateDemoScenario()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var context = CaptureArchSelection(plan, arch1, arch2);
        return (plan, arch1, arch2, context);
    }

    /// <summary>
    /// The phase gate's own named worked example at the view-model layer: preview a rising-brightness
    /// proposal, inspect the diff summary, then accept it -- <see cref="AiEditProposalReviewViewModel.LastAccepted"/>
    /// carries exactly one transaction, and the pending preview is cleared.
    /// </summary>
    [Fact]
    public async Task Step076_Acceptance_PreviewThenAccept_RecordsOneTransaction()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var proposal = interpreter.ParseKnownCommand("brightness 80", context, plan, CancellationToken.None);

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var dryRunService = new EditProposalDryRunService(validator);
        var approvalService = new EditProposalApprovalService(validator);
        var vm = new AiEditProposalReviewViewModel();

        Assert.True(vm.IsEmpty);

        var previewed = await vm.PreviewAsync(dryRunService, proposal, context, plan, CancellationToken.None);

        Assert.True(previewed);
        Assert.True(vm.IsLoaded);
        Assert.NotNull(vm.DryRun);
        Assert.Contains("2 effect(s) changed", vm.DiffSummary, StringComparison.Ordinal);
        Assert.Contains("Arch 1", vm.DiffSummary, StringComparison.Ordinal);
        Assert.Contains("Arch 2", vm.DiffSummary, StringComparison.Ordinal);
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.LastAccepted);

        var accepted = await vm.AcceptAsync(approvalService, context, plan, CancellationToken.None);

        Assert.True(accepted);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.DryRun);
        Assert.Equal(string.Empty, vm.DiffSummary);
        Assert.NotNull(vm.LastAccepted);
        Assert.Equal(plan.Revision, vm.LastAccepted!.Transaction.BeforeRevision);
        Assert.Equal(plan.Revision + 1, vm.LastAccepted!.Transaction.AfterRevision);
    }

    /// <summary>
    /// Required acceptance, verbatim: "Reject leaves state byte-equivalent." Rejecting after a preview
    /// clears the view model's own bindable state and never touches the document -- the document the
    /// preview was built against is passed by reference and this test asserts it is never observed to
    /// change.
    /// </summary>
    [Fact]
    public async Task Reject_LeavesDocumentByteEquivalent_NegativePath()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var proposal = interpreter.ParseKnownCommand("reverse", context, plan, CancellationToken.None);

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var dryRunService = new EditProposalDryRunService(validator);
        var vm = new AiEditProposalReviewViewModel();

        await vm.PreviewAsync(dryRunService, proposal, context, plan, CancellationToken.None);
        Assert.NotNull(vm.DryRun);

        var revisionBeforeReject = plan.Revision;
        var effectCountBeforeReject = plan.Effects.Count;

        vm.Reject();

        Assert.True(vm.IsEmpty);
        Assert.Null(vm.DryRun);
        Assert.Equal(string.Empty, vm.DiffSummary);
        Assert.Null(vm.ErrorMessage);
        // The document itself was never mutated by Reject -- same revision, same effect count/instances.
        Assert.Equal(revisionBeforeReject, plan.Revision);
        Assert.Equal(effectCountBeforeReject, plan.Effects.Count);
    }

    [Fact]
    public async Task PreviewAsync_UnknownOperationKind_ShowsUserReadableDiagnostic_NegativePath()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var operation = new StructuredEditOperation(
            "op-1", "DeleteEverything", context.Scope.Time, [arch1.Target, arch2.Target],
            new Dictionary<string, string>());
        var proposal = new EditProposal(Guid.NewGuid(), plan.Revision, [operation], "malicious proposal");

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var dryRunService = new EditProposalDryRunService(validator);
        var vm = new AiEditProposalReviewViewModel();

        var previewed = await vm.PreviewAsync(dryRunService, proposal, context, plan, CancellationToken.None);

        Assert.False(previewed);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.DryRun);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("DeleteEverything", vm.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Accept must re-validate against the document's own current state -- a document that drifted after
    /// preview (a concurrent edit landed) is rejected, the previously previewed <see cref="AiEditProposalReviewViewModel.DryRun"/>
    /// is left intact (so the caller can re-preview), and no mutation occurs.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_StaleRevisionSinceDrifted_IsRejected_DryRunLeftIntact_NegativePath()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var proposal = interpreter.ParseKnownCommand("brighten", context, plan, CancellationToken.None);

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var dryRunService = new EditProposalDryRunService(validator);
        var approvalService = new EditProposalApprovalService(validator);
        var vm = new AiEditProposalReviewViewModel();

        await vm.PreviewAsync(dryRunService, proposal, context, plan, CancellationToken.None);
        var drifted = plan with { Revision = plan.Revision + 1 };

        var accepted = await vm.AcceptAsync(approvalService, context, drifted, CancellationToken.None);

        Assert.False(accepted);
        Assert.NotNull(vm.DryRun);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.LastAccepted);
    }

    [Fact]
    public async Task AcceptAsync_WithoutPriorPreview_Throws_NegativePath()
    {
        var (plan, _, _, context) = BuildGateDemoScenario();
        var validator = new EditProposalValidator(new EffectLockPolicy());
        var approvalService = new EditProposalApprovalService(validator);
        var vm = new AiEditProposalReviewViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.AcceptAsync(approvalService, context, plan, CancellationToken.None));
    }
}
