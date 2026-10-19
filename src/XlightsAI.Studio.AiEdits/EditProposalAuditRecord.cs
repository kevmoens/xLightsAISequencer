namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 078's own persisted audit entry for exactly one <see cref="EditProposal"/>'s lifecycle --
/// implementation guidance, verbatim: "Persist proposal lifecycle, validation failures and accepted
/// transactions." One record is written per proposal id, overwritten in place as its
/// <see cref="Status"/> advances (<see cref="EditProposalAuditStatus.Pending"/> -&gt;
/// <see cref="EditProposalAuditStatus.Rejected"/> or <see cref="EditProposalAuditStatus.Committed"/>), never
/// deleted -- a rejected or committed record remains inspectable after the fact, satisfying "accepted
/// transactions" and "validation failures" as two distinct, equally-persisted outcomes rather than only
/// logging the successful path.
/// </summary>
/// <param name="ProposalId">The <see cref="EditProposal.Id"/> this record tracks -- stable across every status transition.</param>
/// <param name="SelectionContextId">
/// The originating <see cref="SelectionContext.Id"/>, when one exists. A <see cref="MotifPropagatedProposal"/>
/// (PHASE-11 Step 077) is not captured against a single <see cref="SelectionContext"/> -- see that record's
/// own remarks -- so this is <see langword="null"/> for a motif-propagated proposal's own audit record.
/// </param>
/// <param name="BaseRevision">The <see cref="EditProposal.BaseRevision"/> this proposal was built against.</param>
/// <param name="Status">The proposal's current lifecycle state.</param>
/// <param name="ValidationErrors">Every independent validation error at the point <see cref="Status"/> became <see cref="EditProposalAuditStatus.Rejected"/>; empty for <see cref="EditProposalAuditStatus.Pending"/>/<see cref="EditProposalAuditStatus.Committed"/>.</param>
/// <param name="TransactionId">The resulting <see cref="XlightsAI.Studio.Editing.EditTransaction.Id"/> once <see cref="Status"/> is <see cref="EditProposalAuditStatus.Committed"/>; otherwise <see langword="null"/>.</param>
/// <param name="CommittedRevision">The document revision produced by committing this proposal, once <see cref="Status"/> is <see cref="EditProposalAuditStatus.Committed"/>; otherwise <see langword="null"/>.</param>
/// <param name="RecordedAtUtc">When this specific status transition was written -- provenance only.</param>
public sealed record EditProposalAuditRecord(
    Guid ProposalId,
    Guid? SelectionContextId,
    long BaseRevision,
    EditProposalAuditStatus Status,
    IReadOnlyList<string> ValidationErrors,
    Guid? TransactionId,
    long? CommittedRevision,
    DateTimeOffset RecordedAtUtc);
