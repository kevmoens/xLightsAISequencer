using System.Text.Json;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Default <see cref="IEditApprovalService"/>. See interface doc for ownership/scope.
///
/// <para>
/// PHASE-11 Step 078 ("Persist proposal audit, undo/redo and failure recovery") adds two optional,
/// additive collaborators, the same "optional, backward-compatible constructor" pattern
/// <see cref="XlightsAI.Studio.Editing.EditTransactionService"/>'s own history-store constructor already
/// established: an <see cref="IEditProposalAuditStore"/> (persists this proposal's final
/// <see cref="EditProposalAuditStatus.Rejected"/>/<see cref="EditProposalAuditStatus.Committed"/> outcome),
/// and an <see cref="XlightsAI.Studio.Editing.IEditTransactionHistoryStore"/> (pushes the committed
/// transaction onto the same persisted undo/redo stack
/// <see cref="XlightsAI.Studio.Editing.EditTransactionService"/> itself reads -- "Accept creates one
/// undoable transaction," PHASE-11 Section 7's own cross-cutting test #8, verbatim).
/// </para>
/// </summary>
public sealed class EditProposalApprovalService : IEditApprovalService
{
    private readonly IEditProposalValidator _validator;
    private readonly IEditProposalAuditStore? _auditStore;
    private readonly IEditTransactionHistoryStore? _historyStore;

    public EditProposalApprovalService(IEditProposalValidator validator)
        : this(validator, auditStore: null, historyStore: null)
    {
    }

    public EditProposalApprovalService(
        IEditProposalValidator validator,
        IEditProposalAuditStore? auditStore,
        IEditTransactionHistoryStore? historyStore)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
        _auditStore = auditStore;
        _historyStore = historyStore;
    }

    public async Task<EditApplyResult> AcceptAsync(EditProposalDryRun dryRun, SelectionContext context, SequenceDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dryRun);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        ct.ThrowIfCancellationRequested();

        // Re-validate against the document's own current state at accept time, not the state it was
        // previewed against -- time may have passed, and a concurrent edit (or a newly applied lock)
        // between preview and accept must never silently commit a proposal the user never actually saw
        // the effect of.
        var validation = _validator.Validate(dryRun.Proposal, context, document);
        if (!validation.Allowed)
        {
            _auditStore?.Save(new EditProposalAuditRecord(
                dryRun.Proposal.Id, context.Id, dryRun.Proposal.BaseRevision, EditProposalAuditStatus.Rejected,
                validation.Errors, TransactionId: null, CommittedRevision: null, DateTimeOffset.UtcNow));
            throw new EditProposalRejectedException(validation.Errors);
        }

        // Recomputed fresh against the live document (never the possibly-stale cached
        // dryRun.Candidate) -- the exact same deterministic logic Preview used, so what gets committed
        // is provably the same mutation the user already reviewed.
        var applied = ProposalApplier.Apply(document, dryRun.Proposal);

        var newRevision = document.Revision + 1;
        var updatedDocument = document with { Revision = newRevision, Effects = applied.AfterEffects };

        var transaction = new EditTransaction(
            Guid.NewGuid(),
            document.Revision,
            newRevision,
            JsonSerializer.Serialize(applied.BeforeAffected),
            JsonSerializer.Serialize(applied.AfterAffected),
            DateTimeOffset.UtcNow);

        if (_historyStore is not null)
        {
            await EditTransactionHistoryRecorder.AppendAsync(_historyStore, document, updatedDocument, transaction, ct).ConfigureAwait(false);
        }

        _auditStore?.Save(new EditProposalAuditRecord(
            dryRun.Proposal.Id, context.Id, dryRun.Proposal.BaseRevision, EditProposalAuditStatus.Committed,
            [], transaction.Id, newRevision, DateTimeOffset.UtcNow));

        return new EditApplyResult(updatedDocument, transaction);
    }
}
