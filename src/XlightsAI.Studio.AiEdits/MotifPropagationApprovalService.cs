using System.Text.Json;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Default <see cref="IMotifPropagationApprovalService"/>. See interface doc for ownership/scope.
///
/// <para>
/// PHASE-11 Step 078 adds the same optional <see cref="IEditProposalAuditStore"/>/
/// <see cref="IEditTransactionHistoryStore"/> collaborators as <see cref="EditProposalApprovalService"/>
/// (see that type's own remarks) -- a motif-propagated accept produces exactly one
/// <see cref="EditTransaction"/>, pushed onto the same persisted undo/redo stack a single-occurrence
/// accept would use, keyed by the same <see cref="SequenceDocument.Id"/>.
/// </para>
/// </summary>
public sealed class MotifPropagationApprovalService : IMotifPropagationApprovalService
{
    private readonly IMotifPropagationValidator _validator;
    private readonly IEditProposalAuditStore? _auditStore;
    private readonly IEditTransactionHistoryStore? _historyStore;

    public MotifPropagationApprovalService(IMotifPropagationValidator validator)
        : this(validator, auditStore: null, historyStore: null)
    {
    }

    public MotifPropagationApprovalService(
        IMotifPropagationValidator validator,
        IEditProposalAuditStore? auditStore,
        IEditTransactionHistoryStore? historyStore)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
        _auditStore = auditStore;
        _historyStore = historyStore;
    }

    public async Task<EditApplyResult> AcceptAsync(
        EditProposalDryRun dryRun,
        MotifPropagatedProposal propagated,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dryRun);
        ArgumentNullException.ThrowIfNull(propagated);
        ArgumentNullException.ThrowIfNull(document);

        ct.ThrowIfCancellationRequested();

        // Re-validate against the document's own current state at accept time, not the state it was
        // previewed against -- identical reasoning to EditProposalApprovalService.AcceptAsync (Step 076):
        // a concurrent edit or a newly applied lock between preview and accept must never silently commit
        // a proposal the user never actually saw the effect of, now re-checked per-occurrence.
        var validation = _validator.Validate(propagated, document);
        if (!validation.Allowed)
        {
            _auditStore?.Save(new EditProposalAuditRecord(
                propagated.Proposal.Id, SelectionContextId: null, propagated.Proposal.BaseRevision,
                EditProposalAuditStatus.Rejected, validation.Errors, TransactionId: null, CommittedRevision: null, DateTimeOffset.UtcNow));
            throw new EditProposalRejectedException(validation.Errors);
        }

        var applied = ProposalApplier.Apply(document, propagated.Proposal);

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
            propagated.Proposal.Id, SelectionContextId: null, propagated.Proposal.BaseRevision,
            EditProposalAuditStatus.Committed, [], transaction.Id, newRevision, DateTimeOffset.UtcNow));

        return new EditApplyResult(updatedDocument, transaction);
    }
}
