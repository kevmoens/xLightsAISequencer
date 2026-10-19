namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 022's own record: one document's complete undo/redo stack state, as persisted by
/// <see cref="IEditTransactionHistoryStore"/> (Section 4: "Undo/redo history survives restart within
/// a retention limit"). Both lists are ordered oldest-to-newest; the last element of
/// <see cref="UndoStack"/> is "the next undo," and the last element of <see cref="RedoStack"/> is
/// "the next redo" — the same convention a <c>Stack&lt;T&gt;</c> enumerated in push order would give.
/// </summary>
public sealed record EditTransactionHistorySnapshot(
    IReadOnlyList<EditTransactionHistoryEntry> UndoStack,
    IReadOnlyList<EditTransactionHistoryEntry> RedoStack);
