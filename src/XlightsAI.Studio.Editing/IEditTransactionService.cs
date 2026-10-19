using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Section 3 proposed contract for the document mutation pipeline (Section 3 "Architectural
/// design notes": "single immutable document mutation pipeline"). Step 019 implements
/// <see cref="ApplyAsync"/> for <see cref="MoveEffect"/> only.
///
/// <b>Reconciled, disclosed deviation from the phase spec's proposed signature.</b> Section 3 proposes
/// <c>Task&lt;EditTransaction&gt; ApplyAsync(EditCommand command, CancellationToken ct)</c> — no
/// document parameter, implying a project-session-scoped "current open document" the service resolves
/// internally (consistent with <c>UndoAsync(Guid projectId, ...)</c> taking a project id, not a
/// document). No such project-session/current-document infrastructure exists yet anywhere in this
/// repository (<see cref="ISequenceDocumentStore"/> is a plain path-keyed load/save, with no concept of
/// "the currently open document for project X" or a persisted undo stack) — building that is squarely
/// Step 022's own scope ("Undo/redo transactions... persisted as transactions"), not Step 019's. Rather
/// than fabricate that infrastructure now, <see cref="ApplyAsync"/> here takes the input
/// <see cref="SequenceDocument"/> explicitly and returns the updated one — a pure, revision-checked
/// function from (document, command) to (document, transaction) — and <see cref="UndoAsync"/>/
/// <see cref="RedoAsync"/> are intentionally not yet implemented. A caller (a future Shell ViewModel, or
/// Step 022's own persisted-session service) is responsible for loading/saving the document around this
/// call; that wiring is out of this step's own numbered scope (Step 018's effect-block UI, which this
/// would hang off of, has also not yet been implemented — see STEP-019.md §2).
/// </summary>
public interface IEditTransactionService
{
    /// <exception cref="EditCommandRejectedException">
    /// <paramref name="command"/> fails any validation gate (<see cref="EditRejectionReason"/>). No
    /// mutation of <paramref name="document"/> is observable in this case — <paramref name="document"/>
    /// is itself immutable, and no replacement document or transaction is ever constructed before every
    /// gate passes.
    /// </exception>
    Task<EditApplyResult> ApplyAsync(SequenceDocument document, EditCommand command, CancellationToken ct);

    /// <summary>
    /// PHASE-03 Step 022 implements this for real. <b>Reconciled, disclosed deviation</b>: Section 3
    /// proposes <c>UndoAsync(Guid projectId, ...)</c>, implying a project-session-scoped "current open
    /// document" this service resolves internally — no such infrastructure exists anywhere in this
    /// repository yet (see <see cref="ApplyAsync"/>'s own identical disclosed deviation). <paramref
    /// name="projectId"/> is therefore interpreted as the target document's own
    /// <see cref="XlightsAI.Studio.Xlights.SequenceDocument.Id"/> — the only identity this service
    /// already tracks an undo/redo stack by, via the <paramref name="document"/> argument every
    /// <see cref="ApplyAsync"/> call supplies explicitly.
    /// </summary>
    /// <exception cref="EditCommandRejectedException"><see cref="EditRejectionReason.NothingToUndo"/> when no applied edit exists to undo.</exception>
    Task<SequenceDocument> UndoAsync(Guid projectId, CancellationToken ct);

    /// <summary>Same <paramref name="projectId"/> interpretation as <see cref="UndoAsync"/>.</summary>
    /// <exception cref="EditCommandRejectedException"><see cref="EditRejectionReason.NothingToRedo"/> when no undone edit exists to redo.</exception>
    Task<SequenceDocument> RedoAsync(Guid projectId, CancellationToken ct);
}
