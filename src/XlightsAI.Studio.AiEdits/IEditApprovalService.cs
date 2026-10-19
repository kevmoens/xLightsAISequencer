using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11.md Section 3's own proposed signature is <c>Task&lt;EditTransaction&gt; ApproveAsync(Guid
/// proposalId, CancellationToken ct)</c>, implying a server-side pending-proposal store keyed by id this
/// service resolves internally. <b>Reconciled, disclosed deviation</b>, the same pattern
/// <see cref="IEditTransactionService.ApplyAsync"/>'s own doc comment already establishes for this exact
/// repository: no persisted pending-proposal store exists yet -- building one is Step 078's own later scope
/// ("Persist proposal audit, undo/redo and failure recovery"), not Step 076's. <see cref="AcceptAsync"/>
/// therefore takes the <see cref="EditProposalDryRun"/>, its originating <see cref="SelectionContext"/> and
/// the current <see cref="SequenceDocument"/> explicitly, and returns the already-accepted
/// <see cref="EditApplyResult"/> shape every other PHASE-03 command apply path already returns, rather than
/// a bare <see cref="EditTransaction"/> -- a caller always needs the updated document alongside the audit
/// record to keep editing.
/// </summary>
public interface IEditApprovalService
{
    /// <exception cref="ArgumentNullException"><paramref name="dryRun"/>, <paramref name="context"/>, or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was already canceled.</exception>
    /// <exception cref="EditProposalRejectedException">
    /// Re-validation against the current <paramref name="document"/> fails -- the document changed between
    /// preview and accept (a concurrent edit landed, a lock was added, etc.). No mutation occurs; the
    /// caller's own document is left byte-equivalent. "Reject leaves state byte-equivalent" (Step 076's own
    /// required acceptance) applies symmetrically here: a would-be accept that turns out stale behaves
    /// exactly like an explicit reject.
    /// </exception>
    Task<EditApplyResult> AcceptAsync(EditProposalDryRun dryRun, SelectionContext context, SequenceDocument document, CancellationToken ct = default);
}
