using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-11 Step 078's own bridge into PHASE-03 Step 022's already-accepted undo/redo persistence: appends
/// one externally-produced <see cref="EditTransactionHistoryEntry"/> onto an
/// <see cref="IEditTransactionHistoryStore"/>-backed document's undo stack, clearing its redo stack -- the
/// exact same "a new edit invalidates pending redo history" rule <see cref="EditTransactionService"/>'s own
/// (private) <c>RecordHistoryAsync</c> already enforces for commands applied through
/// <see cref="IEditTransactionService.ApplyAsync"/>.
///
/// <para>
/// <b>Why this exists as a public, standalone helper instead of exposing <c>EditTransactionService</c>'s own
/// private method.</b> <c>XlightsAI.Studio.AiEdits</c>'s own approval services (Step 076/077) never route an
/// accepted <see cref="EditProposal"/> through <see cref="IEditTransactionService.ApplyAsync"/> -- a disclosed,
/// already-accepted architecture decision (see <c>ProposalApplier</c>'s own remarks: the one registered xLights
/// recipe cannot yet express any of Step 073's five quick-action kinds). An AI-approved edit therefore produces
/// its own <see cref="EditTransaction"/> and before/after <see cref="SequenceDocument"/> pair directly, outside
/// <see cref="EditTransactionService"/>'s own in-memory stacks. This helper lets that externally-produced entry
/// land in the exact same persisted <see cref="IEditTransactionHistoryStore"/> file
/// <see cref="EditTransactionService"/> itself reads from -- so a later <see cref="EditTransactionService.UndoAsync"/>
/// call against the same document id (even from a freshly constructed service instance, i.e. "after restart")
/// transparently rehydrates and can undo an AI-approved edit exactly as it would undo one of its own commands.
/// Same-process live synchronization with an already-constructed <see cref="EditTransactionService"/> instance's
/// own in-memory stacks is not attempted here -- that instance only sees this entry the next time it hydrates
/// that document id from the store (its own already-disclosed "GetHydratedStackAsync" lazy-load behavior), which
/// is exactly the realistic restart/new-instance scenario this step's own required acceptance names.
/// </para>
/// </summary>
public static class EditTransactionHistoryRecorder
{
    /// <summary>
    /// Loads <paramref name="historyStore"/>'s current snapshot for <paramref name="before"/>'s own
    /// <see cref="SequenceDocument.Id"/> (or starts from an empty one when none exists yet), appends one new
    /// undo entry for <paramref name="transaction"/>, clears the redo stack, and persists the result
    /// atomically.
    /// </summary>
    public static async Task AppendAsync(
        IEditTransactionHistoryStore historyStore,
        SequenceDocument before,
        SequenceDocument after,
        EditTransaction transaction,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(historyStore);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(transaction);

        var snapshot = await historyStore.LoadAsync(before.Id, ct).ConfigureAwait(false);
        var undo = snapshot?.UndoStack.ToList() ?? [];
        undo.Add(new EditTransactionHistoryEntry(before, after, transaction));

        await historyStore.SaveAsync(before.Id, new EditTransactionHistorySnapshot(undo, []), ct).ConfigureAwait(false);
    }
}
