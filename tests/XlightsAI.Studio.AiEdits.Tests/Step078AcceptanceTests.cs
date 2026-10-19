using System.Text.Json;
using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits.Tests;

/// <summary>
/// PHASE-11 Step 078 ("Persist proposal audit, undo/redo and failure recovery"). Required acceptance
/// (verbatim): "Crash/restart leaves committed edits intact, partial proposal uncommitted." Required
/// failure/edge case (verbatim): "Out-of-scope, locked or stale revision must be rejected without partial
/// mutation." These tests exercise the real <see cref="JsonEditProposalAuditStore"/>,
/// <see cref="XlightsAI.Studio.Editing.JsonEditTransactionHistoryStore"/>,
/// <see cref="XlightsAI.Studio.Xlights.JsonSequenceDocumentStore"/>, <see cref="EditProposalValidator"/>,
/// <see cref="EditProposalDryRunService"/>, <see cref="EditProposalApprovalService"/>,
/// <see cref="MotifPropagationValidator"/>, <see cref="MotifPropagationDryRunService"/>,
/// <see cref="MotifPropagationApprovalService"/> and <see cref="EditTransactionService"/> against real
/// temp-directory-backed files -- no mocking library, no in-memory stand-in for the file system -- so
/// "restart" is modeled by constructing brand-new service instances pointed at the same on-disk
/// directories, never by reusing an existing in-memory object graph.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Step078AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"step078-{Guid.NewGuid():N}");

    public Step078AcceptanceTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

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

    private static SelectionContext CaptureArchSelection(SequenceDocument plan, EffectInstance arch1, EffectInstance arch2) =>
        new SelectionContextCaptureService(new EffectLockPolicy()).Capture(
            plan,
            new DocumentRevision(plan.Revision),
            new EditScope(
                new TimeWindow(Math.Min(arch1.StartTicks, arch2.StartTicks), Math.Max(arch1.EndTicks, arch2.EndTicks)),
                new HashSet<ModelId> { arch1.Target, arch2.Target }),
            [], [], [], []);

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
    /// The phase gate's own named worked example, narrowed to this step's own scope: preview and accept one
    /// proposal with the audit store and history store both wired in, then construct brand-new service
    /// instances pointed at the same directories ("restart") and prove (a) the audit record for the
    /// committed proposal still reports <see cref="EditProposalAuditStatus.Committed"/> with its real
    /// transaction id, and (b) a fresh <see cref="EditTransactionService"/> can undo that AI-approved edit
    /// purely by reading the persisted history store -- "Accept creates one undoable transaction," PHASE-11
    /// Section 7's own cross-cutting test #8, verbatim.
    /// </summary>
    [Fact]
    public async Task Step078_Acceptance_CommittedProposal_IsAuditedAndUndoableAfterRestart()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var proposal = interpreter.ParseKnownCommand("brightness 80", context, plan, CancellationToken.None);

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var auditStore = new JsonEditProposalAuditStore(_root);
        var historyStore = new JsonEditTransactionHistoryStore(_root);
        var dryRunService = new EditProposalDryRunService(validator, auditStore);
        var approvalService = new EditProposalApprovalService(validator, auditStore, historyStore);

        var dryRun = dryRunService.Preview(proposal, context, plan);

        var pendingRecord = auditStore.Load(proposal.Id);
        Assert.NotNull(pendingRecord);
        Assert.Equal(EditProposalAuditStatus.Pending, pendingRecord!.Status);
        Assert.Equal(context.Id, pendingRecord.SelectionContextId);

        var result = await approvalService.AcceptAsync(dryRun, context, plan, CancellationToken.None);

        // --- "restart": brand-new service instances, same directories, nothing carried over in memory ---
        var restartedAuditStore = new JsonEditProposalAuditStore(_root);
        var restartedHistoryStore = new JsonEditTransactionHistoryStore(_root);
        var restartedTransactionService = new EditTransactionService(new EffectLockPolicy(), new EffectRecipeRegistry(), restartedHistoryStore);

        var committedRecord = restartedAuditStore.Load(proposal.Id);
        Assert.NotNull(committedRecord);
        Assert.Equal(EditProposalAuditStatus.Committed, committedRecord!.Status);
        Assert.Equal(result.Transaction.Id, committedRecord.TransactionId);
        Assert.Equal(result.Document.Revision, committedRecord.CommittedRevision);
        Assert.Empty(committedRecord.ValidationErrors);

        var restoredBeforeUndo = await restartedTransactionService.UndoAsync(result.Document.Id, CancellationToken.None);

        Assert.Equal(plan.Revision, restoredBeforeUndo.Revision);
        Assert.Equal(
            JsonSerializer.Serialize(plan.Effects.Single(e => e.Id == arch1.Id)),
            JsonSerializer.Serialize(restoredBeforeUndo.Effects.Single(e => e.Id == arch1.Id)));
        Assert.Equal(
            JsonSerializer.Serialize(plan.Effects.Single(e => e.Id == arch2.Id)),
            JsonSerializer.Serialize(restoredBeforeUndo.Effects.Single(e => e.Id == arch2.Id)));
    }

    /// <summary>
    /// Required acceptance, verbatim: "Crash/restart leaves committed edits intact, partial proposal
    /// uncommitted." A second proposal is only ever previewed (persisting its own
    /// <see cref="EditProposalAuditStatus.Pending"/> record) -- Accept is deliberately never called,
    /// standing in for a crash between preview and accept. After "restart" (fresh store instances), the
    /// first proposal's committed edit is still fully intact and undoable, the second proposal's own audit
    /// record is still <see cref="EditProposalAuditStatus.Pending"/> (never silently promoted to
    /// <see cref="EditProposalAuditStatus.Committed"/>), and the real persisted <see cref="SequenceDocument"/>
    /// on disk reflects only the first (committed) proposal's own change -- the second proposal's own
    /// candidate mutation never reached the persisted document at all.
    /// </summary>
    [Fact]
    public async Task Step078_Acceptance_CrashBetweenPreviewAndAccept_LeavesCommittedEditIntactAndPendingProposalUncommitted()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var auditStore = new JsonEditProposalAuditStore(_root);
        var historyStore = new JsonEditTransactionHistoryStore(_root);
        var documentStore = new JsonSequenceDocumentStore(_root);
        var dryRunService = new EditProposalDryRunService(validator, auditStore);
        var approvalService = new EditProposalApprovalService(validator, auditStore, historyStore);

        // Proposal 1: previewed and accepted, then the resulting document is durably persisted -- the
        // realistic "caller saves the document right after a successful accept" sequencing.
        var proposal1 = interpreter.ParseKnownCommand("brightness 80", context, plan, CancellationToken.None);
        var dryRun1 = dryRunService.Preview(proposal1, context, plan);
        var result1 = await approvalService.AcceptAsync(dryRun1, context, plan, CancellationToken.None);
        var documentPath = await documentStore.SaveAsync(result1.Document, CancellationToken.None);

        // Proposal 2: previewed against the now-current document (so it would itself be perfectly valid),
        // but Accept is deliberately never invoked -- simulating a crash between preview and accept, before
        // any commit happened.
        var context2 = CaptureArchSelection(result1.Document, arch1, arch2) with
        {
            BaseDocumentRevision = new DocumentRevision(result1.Document.Revision),
        };
        var proposal2 = interpreter.ParseKnownCommand("reverse", context2, result1.Document, CancellationToken.None);
        var dryRun2 = dryRunService.Preview(proposal2, context2, result1.Document);
        Assert.NotNull(dryRun2); // proves proposal2 really was valid and previewable -- not rejected outright

        // --- "restart": brand-new stores/services, same directories ---
        var restartedAuditStore = new JsonEditProposalAuditStore(_root);
        var restartedHistoryStore = new JsonEditTransactionHistoryStore(_root);
        var restartedDocumentStore = new JsonSequenceDocumentStore(_root);
        var restartedTransactionService = new EditTransactionService(new EffectLockPolicy(), new EffectRecipeRegistry(), restartedHistoryStore);

        // Committed edit (proposal 1) is intact: its own audit record is Committed, and the persisted
        // document on disk already carries its change.
        var record1 = restartedAuditStore.Load(proposal1.Id);
        Assert.Equal(EditProposalAuditStatus.Committed, record1!.Status);
        var persistedDocument = await restartedDocumentStore.LoadAsync(documentPath, CancellationToken.None);
        Assert.Equal(result1.Document.Revision, persistedDocument.Revision);
        Assert.Equal("80", persistedDocument.Effects.Single(e => e.Id == arch1.Id).Parameters["AiEdit.SetBrightness.Level"]);

        // Proposal 2 (the "crashed" one) is still Pending -- never silently advanced to Committed, and no
        // trace of its own "reverse" operation exists on the persisted document.
        var record2 = restartedAuditStore.Load(proposal2.Id);
        Assert.NotNull(record2);
        Assert.Equal(EditProposalAuditStatus.Pending, record2!.Status);
        Assert.Null(record2.TransactionId);
        Assert.Equal(persistedDocument.Revision, result1.Document.Revision); // no second revision bump ever landed

        // The committed edit is still undoable purely from the persisted history store -- "committed edits
        // intact" proven as a real, exercisable undo, not merely a status string.
        var restored = await restartedTransactionService.UndoAsync(persistedDocument.Id, CancellationToken.None);
        Assert.Equal(plan.Revision, restored.Revision);
    }

    /// <summary>
    /// A proposal rejected at preview time (an unknown operation kind, "a malicious/hallucinated proposal")
    /// has its rejection persisted with the real diagnostic text -- "validation failures" (implementation
    /// guidance, verbatim) are not just thrown and forgotten; they are durably recorded too, and no partial
    /// mutation of the document occurs.
    /// </summary>
    [Fact]
    public void RejectedAtPreview_PersistsRejectionWithErrors_NoPartialMutation_NegativePath()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var operation = new StructuredEditOperation(
            "op-1", "DeleteEverything", context.Scope.Time, [arch1.Target, arch2.Target],
            new Dictionary<string, string>());
        var proposal = new EditProposal(Guid.NewGuid(), plan.Revision, [operation], "malicious proposal");

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var auditStore = new JsonEditProposalAuditStore(_root);
        var dryRunService = new EditProposalDryRunService(validator, auditStore);

        Assert.Throws<EditProposalRejectedException>(() => dryRunService.Preview(proposal, context, plan));

        var record = auditStore.Load(proposal.Id);
        Assert.NotNull(record);
        Assert.Equal(EditProposalAuditStatus.Rejected, record!.Status);
        Assert.Contains(record.ValidationErrors, e => e.Contains("unknown kind", StringComparison.OrdinalIgnoreCase));
        Assert.Null(record.TransactionId);
        Assert.Equal(1, plan.Revision);
        Assert.DoesNotContain(plan.Effects, e => e.Parameters.Keys.Any(k => k.StartsWith("AiEdit.", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Required failure/edge case, verbatim: "Out-of-scope, locked or stale revision must be rejected
    /// without partial mutation." A proposal previewed successfully (recorded Pending) but whose target
    /// becomes locked before Accept is called is rejected at accept time -- the audit record is updated from
    /// Pending to Rejected (never silently left as a stale "Pending" forever, and never advanced to
    /// Committed), and the document is left with no partial mutation.
    /// </summary>
    [Fact]
    public async Task LockedBetweenPreviewAndAccept_UpdatesAuditFromPendingToRejected_NoPartialMutation_NegativePath()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var proposal = interpreter.ParseKnownCommand("brighten", context, plan, CancellationToken.None);

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var auditStore = new JsonEditProposalAuditStore(_root);
        var dryRunService = new EditProposalDryRunService(validator, auditStore);
        var approvalService = new EditProposalApprovalService(validator, auditStore, historyStore: null);

        var dryRun = dryRunService.Preview(proposal, context, plan);
        Assert.Equal(EditProposalAuditStatus.Pending, auditStore.Load(proposal.Id)!.Status);

        var lockedArch2 = arch2 with { Locked = true };
        var driftedDocument = plan with { Effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList() };

        var ex = await Assert.ThrowsAsync<EditProposalRejectedException>(
            () => approvalService.AcceptAsync(dryRun, context, driftedDocument, CancellationToken.None));

        var record = auditStore.Load(proposal.Id);
        Assert.Equal(EditProposalAuditStatus.Rejected, record!.Status);
        Assert.Contains(record.ValidationErrors, e => e.Contains("locked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(driftedDocument.Effects, e => e.Parameters.Keys.Any(k => k.StartsWith("AiEdit.", StringComparison.Ordinal)));
    }

    /// <summary>Same required failure/edge case, for a stale revision discovered only at accept time.</summary>
    [Fact]
    public async Task StaleRevisionBetweenPreviewAndAccept_UpdatesAuditToRejected_NoPartialMutation_NegativePath()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var interpreter = new StructuredCommandInterpreter(new EffectLockPolicy());
        var proposal = interpreter.ParseKnownCommand("brighten", context, plan, CancellationToken.None);

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var auditStore = new JsonEditProposalAuditStore(_root);
        var dryRunService = new EditProposalDryRunService(validator, auditStore);
        var approvalService = new EditProposalApprovalService(validator, auditStore, historyStore: null);

        var dryRun = dryRunService.Preview(proposal, context, plan);
        var driftedDocument = plan with { Revision = plan.Revision + 1 };

        await Assert.ThrowsAsync<EditProposalRejectedException>(
            () => approvalService.AcceptAsync(dryRun, context, driftedDocument, CancellationToken.None));

        var record = auditStore.Load(proposal.Id);
        Assert.Equal(EditProposalAuditStatus.Rejected, record!.Status);
        Assert.Contains(record.ValidationErrors, e => e.Contains("revision", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(driftedDocument.Effects, e => e.Parameters.Keys.Any(k => k.StartsWith("AiEdit.", StringComparison.Ordinal)));
    }

    /// <summary>
    /// "Out-of-scope" half of the required failure/edge case: an operation naming Roofline is rejected at
    /// preview time, persisted as Rejected, and Roofline itself never carries any AiEdit metadata.
    /// </summary>
    [Fact]
    public void OutOfScopeTarget_RooflineOutsideSelectedArches_PersistsRejection_NegativePath()
    {
        var (plan, arch1, arch2, context) = BuildGateDemoScenario();
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var operation = new StructuredEditOperation(
            "op-1", StructuredEditOperationKinds.SetBrightness, context.Scope.Time, [roofline.Target],
            new Dictionary<string, string> { ["Level"] = "100" });
        var proposal = new EditProposal(Guid.NewGuid(), plan.Revision, [operation], "widen scope to roofline");

        var validator = new EditProposalValidator(new EffectLockPolicy());
        var auditStore = new JsonEditProposalAuditStore(_root);
        var dryRunService = new EditProposalDryRunService(validator, auditStore);

        Assert.Throws<EditProposalRejectedException>(() => dryRunService.Preview(proposal, context, plan));

        var record = auditStore.Load(proposal.Id);
        Assert.Equal(EditProposalAuditStatus.Rejected, record!.Status);
        Assert.Contains(record.ValidationErrors, e => e.Contains("Roofline", StringComparison.Ordinal));
        Assert.DoesNotContain(roofline.Parameters, kv => kv.Key.StartsWith("AiEdit.", StringComparison.Ordinal));
    }

    /// <summary>A proposal never previewed at all has no audit record -- loading an unknown id returns <see langword="null"/>, never throws.</summary>
    [Fact]
    public void Load_UnknownProposalId_ReturnsNull()
    {
        var auditStore = new JsonEditProposalAuditStore(_root);
        Assert.Null(auditStore.Load(Guid.NewGuid()));
    }

    /// <summary>Motif-propagated accepts (Step 077) are audited identically, keyed by the widened proposal's own id, with no <see cref="SelectionContext"/> to report.</summary>
    [Fact]
    public async Task MotifPropagatedProposal_CommittedAccept_IsAuditedAndUndoableAfterRestart()
    {
        var doc = new SequenceDocument(
            Guid.NewGuid(),
            Revision: 1,
            Effects:
            [
                new EffectRecipeRegistry().CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Arch 1"), 0, 100_000_000),
                new EffectRecipeRegistry().CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Arch 2"), 0, 100_000_000),
                new EffectRecipeRegistry().CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Arch 5"), 400_000_000, 500_000_000),
                new EffectRecipeRegistry().CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Arch 6"), 400_000_000, 500_000_000),
            ]);

        var baseScope = new EditScope(new TimeWindow(0, 100_000_000), new HashSet<ModelId> { new("Arch 1"), new("Arch 2") });
        var occurrenceScope = new EditScope(new TimeWindow(400_000_000, 500_000_000), new HashSet<ModelId> { new("Arch 5"), new("Arch 6") });
        var baseProposal = new EditProposal(
            Guid.NewGuid(), doc.Revision,
            [new StructuredEditOperation("op-1", StructuredEditOperationKinds.SetBrightness, new TimeWindow(0, 100_000_000), [new ModelId("Arch 1"), new ModelId("Arch 2")], new Dictionary<string, string> { ["Level"] = "80" })],
            "rising piano pops");
        var occurrence = new MotifOccurrenceTarget(Guid.NewGuid(), occurrenceScope);
        var selection = new MotifPropagationSelection(new HashSet<Guid> { occurrence.OccurrenceId });
        var expanded = MotifPropagationExpander.Expand(baseProposal, baseScope, [occurrence], selection);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var auditStore = new JsonEditProposalAuditStore(_root);
        var historyStore = new JsonEditTransactionHistoryStore(_root);
        var dryRunService = new MotifPropagationDryRunService(validator, auditStore);
        var approvalService = new MotifPropagationApprovalService(validator, auditStore, historyStore);

        var dryRun = dryRunService.Preview(expanded, doc);
        var pending = auditStore.Load(expanded.Proposal.Id);
        Assert.Equal(EditProposalAuditStatus.Pending, pending!.Status);
        Assert.Null(pending.SelectionContextId);

        var result = await approvalService.AcceptAsync(dryRun, expanded, doc, CancellationToken.None);

        var restartedAuditStore = new JsonEditProposalAuditStore(_root);
        var restartedHistoryStore = new JsonEditTransactionHistoryStore(_root);
        var restartedTransactionService = new EditTransactionService(new EffectLockPolicy(), new EffectRecipeRegistry(), restartedHistoryStore);

        var committed = restartedAuditStore.Load(expanded.Proposal.Id);
        Assert.Equal(EditProposalAuditStatus.Committed, committed!.Status);
        Assert.Equal(result.Transaction.Id, committed.TransactionId);

        var restored = await restartedTransactionService.UndoAsync(result.Document.Id, CancellationToken.None);
        Assert.Equal(doc.Revision, restored.Revision);
    }
}
