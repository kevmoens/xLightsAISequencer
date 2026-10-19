namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 078's own proposal lifecycle states (implementation guidance, verbatim: "Persist proposal
/// lifecycle, validation failures and accepted transactions"). A proposal moves <see cref="Pending"/> (a
/// valid dry-run was previewed, not yet accepted) to exactly one of <see cref="Rejected"/> (validation
/// failed, either at preview or at accept time) or <see cref="Committed"/> (accept succeeded) and never
/// moves again -- a crash/restart between <see cref="Pending"/> and the next transition is this step's own
/// required acceptance, verbatim: "...partial proposal uncommitted."
/// </summary>
public enum EditProposalAuditStatus
{
    Pending,
    Rejected,
    Committed,
}
