using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 049's own revision-aware lock-toggle request, mirroring
/// <c>XlightsAI.Studio.Editing.SetEffectLocked</c>'s own relationship to <c>EditCommand</c>: toggling a
/// note's own locked bit is itself revision-checked (<see cref="ExpectedRevision"/>), but — same
/// "the lock toggle itself must not be gated by the very lock it is setting" precedent
/// <c>EditTransactionService.ApplySetLocked</c> already establishes — is never itself rejected as
/// <see cref="NoteEditRejectionReason.Locked"/> by <see cref="INoteTrackEditor.SetNoteLockedAsync"/>.
/// </summary>
public sealed record SetNoteLockedCommand(
    DocumentRevision ExpectedRevision,
    Guid NoteId,
    bool Locked);
