using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 076 ("Implement dry-run proposals, before/after diff and Accept/Reject"). Section 2's own
/// flowchart: "Apply to working copy + calculate diff" -> "Preview original vs candidate." Never mutates
/// <c>document</c> -- the returned <see cref="EditProposalDryRun.Candidate"/> is a brand-new instance a
/// caller may inspect, show a before/after diff for, and either discard (reject -- document stays
/// byte-equivalent by construction, nothing further to do) or hand to
/// <see cref="IEditApprovalService.Accept"/> (accept).
/// </summary>
public interface IEditProposalDryRunService
{
    /// <exception cref="ArgumentNullException"><paramref name="proposal"/>, <paramref name="context"/>, or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="EditProposalRejectedException">
    /// <see cref="IEditProposalValidator.Validate"/> reports one or more errors (unknown operation kind,
    /// out-of-scope target/time, locked effect, stale revision, or an empty operation list) -- "Clean
    /// machine and unsupported runtime must produce user-readable diagnostics" (Step 076's own required
    /// failure/edge case). No candidate document is ever constructed in this case.
    /// </exception>
    EditProposalDryRun Preview(EditProposal proposal, SelectionContext context, SequenceDocument document);
}
