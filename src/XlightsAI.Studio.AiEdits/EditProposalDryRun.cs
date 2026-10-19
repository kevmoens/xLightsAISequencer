using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 076's own "sandbox copy" result (Section 2: "Prepare proposal in a sandbox copy, render
/// and show a clear diff"). <see cref="Candidate"/> is a brand-new <see cref="SequenceDocument"/> instance;
/// the document <see cref="IEditProposalDryRunService.Preview"/> was called with is never mutated and
/// remains reachable/unchanged by the caller for as long as this dry run is only previewed, never accepted
/// -- "Reject leaves state byte-equivalent" (Step 076's own required acceptance) is then true by
/// construction: rejecting is simply never calling <see cref="IEditApprovalService.Accept"/>.
/// </summary>
/// <param name="Proposal">The proposal this dry run was computed from.</param>
/// <param name="Candidate">The would-be document if this proposal were accepted; not persisted or committed.</param>
/// <param name="Diff">The changed effect count/models/time this proposal would produce.</param>
public sealed record EditProposalDryRun(
    EditProposal Proposal,
    SequenceDocument Candidate,
    ProposalDiff Diff);
