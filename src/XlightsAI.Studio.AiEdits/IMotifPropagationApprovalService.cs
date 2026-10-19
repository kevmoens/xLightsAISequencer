using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 077's own counterpart to <see cref="IEditApprovalService"/>, over a
/// <see cref="MotifPropagatedProposal"/> instead of a single-scope <see cref="EditProposal"/>. Commits
/// exactly the operation set the user already previewed via <see cref="IMotifPropagationDryRunService.Preview"/>
/// -- the same "re-validate against the live document at accept time" rule Step 076's
/// <see cref="EditProposalApprovalService"/> already established, now re-checking every operation's own
/// per-occurrence permitted scope, not one single <see cref="SelectionContext.Scope"/>.
/// </summary>
public interface IMotifPropagationApprovalService
{
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="EditProposalRejectedException">
    /// Re-validation against <paramref name="document"/>'s own current state fails (a concurrent edit, a
    /// newly applied lock, or a stale revision landed since <paramref name="dryRun"/> was computed) -- no
    /// mutation occurs in this case.
    /// </exception>
    Task<EditApplyResult> AcceptAsync(
        EditProposalDryRun dryRun,
        MotifPropagatedProposal propagated,
        SequenceDocument document,
        CancellationToken ct = default);
}
