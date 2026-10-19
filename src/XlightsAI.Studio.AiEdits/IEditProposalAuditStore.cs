namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 078's own persistence boundary for a proposal's lifecycle (see
/// <see cref="EditProposalAuditRecord"/>). Kept separate from <see cref="ISelectionContextStore"/> and
/// <see cref="XlightsAI.Studio.Editing.IEditTransactionHistoryStore"/> -- a selection is "what the user
/// picked," a transaction-history entry is "one committed document mutation and its undo/redo slot," and
/// this is "what happened to one specific proposal, including the ones that were never committed." An
/// optional collaborator of <see cref="IEditProposalDryRunService"/>/<see cref="IEditApprovalService"/>/
/// their motif-propagation counterparts; when not supplied, no lifecycle audit is written (the same
/// "additive, optional collaborator" convention <see cref="XlightsAI.Studio.Editing.IEditTransactionHistoryStore"/>
/// already established for <c>EditTransactionService</c>).
/// </summary>
public interface IEditProposalAuditStore
{
    /// <summary>Persists <paramref name="record"/> atomically, replacing any prior record for the same <see cref="EditProposalAuditRecord.ProposalId"/>.</summary>
    void Save(EditProposalAuditRecord record);

    /// <summary>Returns the most recently saved record for <paramref name="proposalId"/>, or <see langword="null"/> when none has ever been saved.</summary>
    EditProposalAuditRecord? Load(Guid proposalId);
}
