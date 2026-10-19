namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 022's own persistence boundary for a single document's undo/redo stack (Section 4:
/// "Undo/redo history survives restart within a retention limit"). Keyed by the owning
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument.Id"/> — not a SQLite audit table, because no
/// such table exists anywhere in this repository yet for edit transactions (only
/// <c>XlightsAI.Studio.Projects</c>'s own project-manifest schema does), and inventing one here would
/// be exactly the kind of "generic interface without a concrete boundary" Section 2 warns against.
/// An optional collaborator of <see cref="EditTransactionService"/>; when not supplied, undo/redo
/// history is in-memory-only for that service instance's lifetime (explicitly disclosed, not silently
/// claimed as durable — see implementation-evidence/PHASE-03/STEP-022.md).
/// </summary>
public interface IEditTransactionHistoryStore
{
    /// <summary>Persists <paramref name="snapshot"/> atomically for <paramref name="documentId"/>, replacing any prior snapshot for the same id.</summary>
    Task SaveAsync(Guid documentId, EditTransactionHistorySnapshot snapshot, CancellationToken ct);

    /// <summary>Returns the previously saved snapshot for <paramref name="documentId"/>, or <see langword="null"/> when none has ever been saved (never a thrown "not found" — a document with no edit history yet is a normal state, unlike loading an expected-to-exist <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/>).</summary>
    Task<EditTransactionHistorySnapshot?> LoadAsync(Guid documentId, CancellationToken ct);
}
