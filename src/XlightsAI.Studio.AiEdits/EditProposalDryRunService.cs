using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Default <see cref="IEditProposalDryRunService"/>. See interface doc for ownership/scope.
///
/// <para>
/// PHASE-11 Step 078 ("Persist proposal audit, undo/redo and failure recovery") adds an optional
/// <see cref="IEditProposalAuditStore"/> collaborator -- additive, the same pattern
/// <see cref="XlightsAI.Studio.Editing.EditTransactionService"/>'s own optional
/// <see cref="XlightsAI.Studio.Editing.IEditTransactionHistoryStore"/> constructor already established.
/// When supplied, every <see cref="Preview"/> call writes exactly one
/// <see cref="EditProposalAuditRecord"/>: <see cref="EditProposalAuditStatus.Pending"/> for a validated
/// proposal (implementation guidance, verbatim: "Persist proposal lifecycle"), or
/// <see cref="EditProposalAuditStatus.Rejected"/> with the full error list for one that fails validation
/// ("validation failures," verbatim) -- written <em>before</em> the matching exception is thrown, so a
/// crash immediately afterward still leaves the rejection on record.
/// </para>
/// </summary>
public sealed class EditProposalDryRunService : IEditProposalDryRunService
{
    private readonly IEditProposalValidator _validator;
    private readonly IEditProposalAuditStore? _auditStore;

    public EditProposalDryRunService(IEditProposalValidator validator)
        : this(validator, auditStore: null)
    {
    }

    public EditProposalDryRunService(IEditProposalValidator validator, IEditProposalAuditStore? auditStore)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
        _auditStore = auditStore;
    }

    public EditProposalDryRun Preview(EditProposal proposal, SelectionContext context, SequenceDocument document)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        var validation = _validator.Validate(proposal, context, document);
        if (!validation.Allowed)
        {
            _auditStore?.Save(new EditProposalAuditRecord(
                proposal.Id, context.Id, proposal.BaseRevision, EditProposalAuditStatus.Rejected,
                validation.Errors, TransactionId: null, CommittedRevision: null, DateTimeOffset.UtcNow));
            throw new EditProposalRejectedException(validation.Errors);
        }

        _auditStore?.Save(new EditProposalAuditRecord(
            proposal.Id, context.Id, proposal.BaseRevision, EditProposalAuditStatus.Pending,
            [], TransactionId: null, CommittedRevision: null, DateTimeOffset.UtcNow));

        var applied = ProposalApplier.Apply(document, proposal);
        var candidate = document with { Revision = document.Revision + 1, Effects = applied.AfterEffects };

        return new EditProposalDryRun(proposal, candidate, applied.Diff);
    }
}
