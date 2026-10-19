using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 084 ("Add candidate gallery, score explanations, preview and explicit selection"):
/// the explicit, deliberate human accept/reject boundary this step's own heading names. PHASE-12
/// Section 12's own bounded-refinement pseudocode ends with <c>MarkAwaitingHumanApprovalAsync</c> --
/// "not a commit to <c>SequenceDocument</c>." This interface is exactly that deferred, later commit:
/// turning one already-recorded, non-violating <see cref="CandidateAttempt"/> (the retained best, or
/// any other alternative the gallery lets the user compare -- this step's own required acceptance,
/// verbatim: "User can compare alternatives and commit chosen candidate") into a real
/// <see cref="EditApplyResult"/>, or declining the whole run with an optional feedback label.
///
/// <para>
/// Realizes the same, already-accepted "no partial mutation" rule every PHASE-03 command and
/// <see cref="XlightsAI.Studio.AiEdits.IEditApprovalService"/> already enforce -- this step's own
/// required failure/edge case, verbatim: "Out-of-scope, locked or stale revision must be rejected
/// without partial mutation." Every gate in <see cref="AcceptAsync"/> is checked, and every effect this
/// step would compile is computed, strictly before this run's own persisted <see cref="LoopRun.Status"/>
/// is ever touched -- a failed gate leaves both the <see cref="LoopRun"/> and the caller's own
/// <paramref name="document" /> that was passed in byte-equivalent.
/// </para>
///
/// <para>
/// Deliberately mirrors <see cref="XlightsAI.Studio.AiEdits.IEditApprovalService"/>'s own disclosed
/// shape rather than inventing a parallel one: this service never persists the returned
/// <see cref="EditApplyResult.Document"/> itself (no <see cref="ISequenceDocumentStore"/> dependency
/// exists here) -- the caller, exactly like every other Accept path in this repository, is responsible
/// for persisting it and appending <see cref="EditApplyResult.Transaction"/> to undo/redo history.
/// </para>
/// </summary>
public interface IRefinementCandidateCommitService
{
    /// <summary>
    /// Commits <paramref name="candidateId"/> (one of <paramref name="runId"/>'s own recorded attempts)
    /// onto <paramref name="document"/> as one new <see cref="EditTransaction"/>, and transitions the
    /// run to <see cref="RefinementStatus.Accepted"/> (terminal).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was already canceled.</exception>
    /// <exception cref="RefinementRunInvalidException">
    /// No run with <paramref name="runId"/> exists; <paramref name="document"/>'s own
    /// <see cref="SequenceDocument.Id"/> does not match <see cref="LoopRun.DocumentId"/>; the run's own
    /// <see cref="LoopRun.Status"/> is not <see cref="RefinementStatus.AwaitingApproval"/>; no recorded
    /// attempt with <paramref name="candidateId"/> exists for this run; the named attempt carries one or
    /// more <see cref="CandidateAttempt.Violations"/> (this step's own required failure/edge case,
    /// restated from Steps 079-083: a malformed/violating candidate "must not auto-commit"); or the run
    /// could not be re-claimed to record the acceptance (already terminal, or held by another live
    /// claim).
    /// </exception>
    /// <exception cref="EditCommandRejectedException">
    /// <paramref name="document"/>'s own <see cref="SequenceDocument.Revision"/> no longer matches the
    /// run's own <see cref="LoopRun.BaseRevision"/> (<see cref="EditRejectionReason.StaleRevision"/> --
    /// this step's own required failure/edge case, verbatim: "stale revision must be rejected");
    /// the candidate's own gestures reach outside the run's permitted <see cref="LoopRun.Scope"/> model
    /// set or time window (<see cref="EditRejectionReason.OutOfScope"/> -- verbatim: "Out-of-scope...
    /// must be rejected"); the run's permitted scope reaches an already-locked effect in
    /// <paramref name="document"/> (<see cref="EditRejectionReason.Locked"/> -- verbatim: "locked...
    /// must be rejected"); or no free layer exists to place a gesture's own effect alongside
    /// <paramref name="document"/>'s own existing effects on a targeted prop
    /// (<see cref="EditRejectionReason.OutOfScope"/>).
    /// </exception>
    Task<EditApplyResult> AcceptAsync(
        IRefinementRunStore store,
        Guid runId,
        Guid candidateId,
        SequenceDocument document,
        CancellationToken ct = default);

    /// <summary>
    /// Declines every candidate in <paramref name="runId"/>, transitioning it to
    /// <see cref="RefinementStatus.Rejected"/> (terminal) without ever constructing or touching a
    /// <see cref="SequenceDocument"/> -- "Reject leaves state byte-equivalent" (the same required
    /// acceptance <see cref="XlightsAI.Studio.AiEdits.IEditApprovalService"/>'s own Step 076 sibling
    /// already established) holds trivially here because this method's own body never references one.
    /// </summary>
    /// <param name="feedbackLabel">
    /// An optional, free-text human reason recorded onto <see cref="LoopRun.StopReason"/> -- this step's
    /// own implementation guidance, verbatim: "optional feedback labels." <see langword="null"/> or blank
    /// records a generic decline reason instead of a fabricated one.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was already canceled.</exception>
    /// <exception cref="RefinementRunInvalidException">
    /// No run with <paramref name="runId"/> exists; the run's own <see cref="LoopRun.Status"/> is not
    /// <see cref="RefinementStatus.AwaitingApproval"/>; or the run could not be re-claimed to record the
    /// rejection (already terminal, or held by another live claim).
    /// </exception>
    Task<LoopRun> RejectAsync(
        IRefinementRunStore store,
        Guid runId,
        string? feedbackLabel,
        CancellationToken ct = default);
}
