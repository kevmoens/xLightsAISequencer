using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 022's own record: one entry on a document's undo or redo stack — the full document
/// snapshot immediately before and after one successfully applied <see cref="EditCommand"/>, plus the
/// audit <see cref="EditTransaction"/> that was produced. Unlike <see cref="EditTransaction"/> itself
/// (which only serializes the single affected <see cref="EffectInstance"/>, Step 019's own documented
/// design — the whole document is reconstructible from <see cref="ISequenceDocumentStore"/> by
/// revision), this entry carries the complete <see cref="Before"/>/<see cref="After"/> documents
/// directly, because <see cref="EditTransactionService"/> owns the undo/redo stack itself rather than
/// a persisted-project document store keyed by revision — "Undo restores exact snapshot" (this step's
/// own required acceptance) is proven directly from <see cref="Before"/>, not reconstructed.
/// </summary>
public sealed record EditTransactionHistoryEntry(
    SequenceDocument Before,
    SequenceDocument After,
    EditTransaction Transaction);
