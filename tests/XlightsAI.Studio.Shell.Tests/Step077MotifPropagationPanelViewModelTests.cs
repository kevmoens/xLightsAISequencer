using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-11 Step 077 -- "Implement explicit motif propagation and edit-scope intersection validation."
/// Required acceptance: "Locked/out-of-scope effects cannot change even with malicious proposal." Required
/// failure/edge case: "Out-of-scope, locked or stale revision must be rejected without partial mutation."
/// Fast, deterministic tests for <see cref="MotifPropagationPanelViewModel"/>'s own bindable
/// attach/include/exclude/preview/accept/reject contract against the real
/// <see cref="MotifPropagationExpander"/>/<see cref="MotifPropagationValidator"/>/
/// <see cref="MotifPropagationDryRunService"/>/<see cref="MotifPropagationApprovalService"/>/
/// <see cref="EffectLockPolicy"/> (not mocks). No real SCREEN 14 AI EDIT Avalonia/AXAML view exists -- the
/// project's own pre-existing, consistent convention (see `implementation-evidence/PHASE-11/STEP-072.md`
/// through `STEP-076.md`).
/// </summary>
public sealed class Step077MotifPropagationPanelViewModelTests
{
    private static readonly EffectRecipeRegistry Recipes = new();

    private static EffectInstance On(string target, long startTicks, long endTicks, bool locked = false) =>
        Recipes.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId(target), startTicks, endTicks) with { Locked = locked };

    private static SequenceDocument BuildFixture() => new(
        Guid.NewGuid(),
        Revision: 1,
        Effects:
        [
            On("Roofline", 0, 500_000_000, locked: true),
            On("Arch 1", 0, 100_000_000),
            On("Arch 2", 0, 100_000_000),
            On("Arch 3", 200_000_000, 300_000_000),
            On("Arch 4", 200_000_000, 300_000_000, locked: true),
            On("Arch 5", 400_000_000, 500_000_000),
            On("Arch 6", 400_000_000, 500_000_000),
        ]);

    private static ModelId Arch(SequenceDocument doc, int n) =>
        doc.Effects.Single(e => e.Target.Value == $"Arch {n}").Target;

    private static EditScope BaseScope(SequenceDocument doc) => new(
        new TimeWindow(0, 100_000_000),
        new HashSet<ModelId> { Arch(doc, 1), Arch(doc, 2) });

    private static EditProposal BaseProposal(SequenceDocument doc) => new(
        Guid.NewGuid(),
        doc.Revision,
        [
            new StructuredEditOperation(
                "op-1",
                StructuredEditOperationKinds.SetBrightness,
                new TimeWindow(0, 100_000_000),
                [Arch(doc, 1), Arch(doc, 2)],
                new Dictionary<string, string> { ["Level"] = "80" }),
        ],
        "rising piano pops");

    private static MotifOccurrenceTarget OccurrenceB(SequenceDocument doc) => new(
        Guid.NewGuid(),
        new EditScope(new TimeWindow(200_000_000, 300_000_000), new HashSet<ModelId> { Arch(doc, 3), Arch(doc, 4) }));

    private static MotifOccurrenceTarget OccurrenceC(SequenceDocument doc) => new(
        Guid.NewGuid(),
        new EditScope(new TimeWindow(400_000_000, 500_000_000), new HashSet<ModelId> { Arch(doc, 5), Arch(doc, 6) }));

    [Fact]
    public async Task Step077_Acceptance_IncludeOccurrence_PreviewThenAccept_ChangesOnlyIncludedOccurrences()
    {
        var doc = BuildFixture();
        var occurrenceC = OccurrenceC(doc);
        var vm = new MotifPropagationPanelViewModel();

        Assert.True(vm.IsEmpty);

        vm.AttachCandidates(BaseProposal(doc), BaseScope(doc), [occurrenceC]);
        Assert.True(vm.IsLoaded);
        Assert.Empty(vm.SelectedOccurrenceIds);

        vm.IncludeOccurrence(occurrenceC.OccurrenceId);
        Assert.Contains(occurrenceC.OccurrenceId, vm.SelectedOccurrenceIds);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRunService = new MotifPropagationDryRunService(validator);
        var approvalService = new MotifPropagationApprovalService(validator);

        var previewed = await vm.PreviewAsync(dryRunService, doc, CancellationToken.None);

        Assert.True(previewed);
        Assert.NotNull(vm.DryRun);
        Assert.Equal(4, vm.DryRun!.Diff.ChangedEffectCount);
        Assert.Contains("Arch 5", vm.DiffSummary, StringComparison.Ordinal);
        Assert.Null(vm.ErrorMessage);

        var accepted = await vm.AcceptAsync(approvalService, doc, CancellationToken.None);

        Assert.True(accepted);
        Assert.Null(vm.DryRun);
        Assert.NotNull(vm.LastAccepted);
        Assert.Equal(doc.Revision + 1, vm.LastAccepted!.Transaction.AfterRevision);

        foreach (var name in new[] { "Arch 1", "Arch 2", "Arch 5", "Arch 6" })
        {
            Assert.True(vm.LastAccepted!.Document.Effects.Single(e => e.Target.Value == name).Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
        }

        // Occurrence B (never included) and Roofline are untouched.
        Assert.False(vm.LastAccepted!.Document.Effects.Single(e => e.Target.Value == "Arch 3").Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
        Assert.False(vm.LastAccepted!.Document.Effects.Single(e => e.Target.Value == "Roofline").Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    /// <summary>"Support instance-only choice" (verbatim): never including any occurrence previews/accepts only the original edit.</summary>
    [Fact]
    public async Task InstanceOnly_NeverIncludingAnOccurrence_PreviewsOnlyTheOriginalEdit()
    {
        var doc = BuildFixture();
        var occurrenceC = OccurrenceC(doc);
        var vm = new MotifPropagationPanelViewModel();
        vm.AttachCandidates(BaseProposal(doc), BaseScope(doc), [occurrenceC]);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRunService = new MotifPropagationDryRunService(validator);

        var previewed = await vm.PreviewAsync(dryRunService, doc, CancellationToken.None);

        Assert.True(previewed);
        Assert.Equal(2, vm.DryRun!.Diff.ChangedEffectCount);
    }

    /// <summary>Toggling an occurrence on then off returns to instance-only.</summary>
    [Fact]
    public void ExcludeOccurrence_AfterInclude_ReturnsToInstanceOnly()
    {
        var doc = BuildFixture();
        var occurrenceC = OccurrenceC(doc);
        var vm = new MotifPropagationPanelViewModel();
        vm.AttachCandidates(BaseProposal(doc), BaseScope(doc), [occurrenceC]);

        vm.IncludeOccurrence(occurrenceC.OccurrenceId);
        Assert.Single(vm.SelectedOccurrenceIds);

        vm.ExcludeOccurrence(occurrenceC.OccurrenceId);
        Assert.Empty(vm.SelectedOccurrenceIds);
    }

    /// <summary>
    /// Required acceptance, verbatim: "Locked/out-of-scope effects cannot change even with malicious
    /// proposal." Including the occurrence with a locked effect is rejected with a user-readable
    /// diagnostic, and no document mutation occurs.
    /// </summary>
    [Fact]
    public async Task PreviewAsync_IncludingAnOccurrenceWithALockedEffect_IsRejected_NegativePath()
    {
        var doc = BuildFixture();
        var occurrenceB = OccurrenceB(doc);
        var vm = new MotifPropagationPanelViewModel();
        vm.AttachCandidates(BaseProposal(doc), BaseScope(doc), [occurrenceB]);
        vm.IncludeOccurrence(occurrenceB.OccurrenceId);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRunService = new MotifPropagationDryRunService(validator);

        var previewed = await vm.PreviewAsync(dryRunService, doc, CancellationToken.None);

        Assert.False(previewed);
        Assert.Null(vm.DryRun);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("locked", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(doc.Effects, e => e.Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    [Fact]
    public async Task Reject_LeavesDocumentByteEquivalent_NegativePath()
    {
        var doc = BuildFixture();
        var occurrenceC = OccurrenceC(doc);
        var vm = new MotifPropagationPanelViewModel();
        vm.AttachCandidates(BaseProposal(doc), BaseScope(doc), [occurrenceC]);
        vm.IncludeOccurrence(occurrenceC.OccurrenceId);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRunService = new MotifPropagationDryRunService(validator);

        await vm.PreviewAsync(dryRunService, doc, CancellationToken.None);
        Assert.NotNull(vm.DryRun);

        var revisionBeforeReject = doc.Revision;

        vm.Reject();

        Assert.Null(vm.DryRun);
        Assert.Equal(string.Empty, vm.DiffSummary);
        Assert.Equal(revisionBeforeReject, doc.Revision);
        Assert.DoesNotContain(doc.Effects, e => e.Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    [Fact]
    public async Task AcceptAsync_WithoutPriorPreview_Throws_NegativePath()
    {
        var doc = BuildFixture();
        var occurrenceC = OccurrenceC(doc);
        var vm = new MotifPropagationPanelViewModel();
        vm.AttachCandidates(BaseProposal(doc), BaseScope(doc), [occurrenceC]);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var approvalService = new MotifPropagationApprovalService(validator);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.AcceptAsync(approvalService, doc, CancellationToken.None));
    }

    [Fact]
    public async Task PreviewAsync_WithoutAttachingCandidates_Throws_NegativePath()
    {
        var doc = BuildFixture();
        var vm = new MotifPropagationPanelViewModel();
        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRunService = new MotifPropagationDryRunService(validator);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.PreviewAsync(dryRunService, doc, CancellationToken.None));
    }
}
