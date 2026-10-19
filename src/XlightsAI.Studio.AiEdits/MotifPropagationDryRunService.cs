using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Default <see cref="IMotifPropagationDryRunService"/>. See interface doc for ownership/scope.
///
/// <para>
/// PHASE-11 Step 078 adds the same optional <see cref="IEditProposalAuditStore"/> collaborator as
/// <see cref="EditProposalDryRunService"/> (see that type's own remarks). <see cref="EditProposalAuditRecord.SelectionContextId"/>
/// is always <see langword="null"/> here -- a <see cref="MotifPropagatedProposal"/> is not captured
/// against a single <see cref="SelectionContext"/> (see that record's own remarks on
/// <see cref="MotifPropagatedProposal.PermittedScopeByOperationId"/>).
/// </para>
/// </summary>
public sealed class MotifPropagationDryRunService : IMotifPropagationDryRunService
{
    private readonly IMotifPropagationValidator _validator;
    private readonly IEditProposalAuditStore? _auditStore;

    public MotifPropagationDryRunService(IMotifPropagationValidator validator)
        : this(validator, auditStore: null)
    {
    }

    public MotifPropagationDryRunService(IMotifPropagationValidator validator, IEditProposalAuditStore? auditStore)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
        _auditStore = auditStore;
    }

    public EditProposalDryRun Preview(MotifPropagatedProposal propagated, SequenceDocument document)
    {
        ArgumentNullException.ThrowIfNull(propagated);
        ArgumentNullException.ThrowIfNull(document);

        var validation = _validator.Validate(propagated, document);
        if (!validation.Allowed)
        {
            _auditStore?.Save(new EditProposalAuditRecord(
                propagated.Proposal.Id, SelectionContextId: null, propagated.Proposal.BaseRevision,
                EditProposalAuditStatus.Rejected, validation.Errors, TransactionId: null, CommittedRevision: null, DateTimeOffset.UtcNow));
            throw new EditProposalRejectedException(validation.Errors);
        }

        _auditStore?.Save(new EditProposalAuditRecord(
            propagated.Proposal.Id, SelectionContextId: null, propagated.Proposal.BaseRevision,
            EditProposalAuditStatus.Pending, [], TransactionId: null, CommittedRevision: null, DateTimeOffset.UtcNow));

        var applied = ProposalApplier.Apply(document, propagated.Proposal);
        var candidate = document with { Revision = document.Revision + 1, Effects = applied.AfterEffects };

        return new EditProposalDryRun(propagated.Proposal, candidate, applied.Diff);
    }
}
