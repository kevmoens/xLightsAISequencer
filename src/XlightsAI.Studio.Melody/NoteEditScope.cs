using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 049's own narrow, Melody-local sibling of <c>XlightsAI.Studio.Editing.EditScope</c>
/// (PHASE-03 Section 3): the exact time window (and, optionally, exact note identities) a single
/// <see cref="CorrectNoteCommand"/>/<see cref="SetNoteLockedCommand"/> is permitted to touch. A
/// <see cref="MelodyTrack"/> has no "target model" concept the way a <c>SequenceDocument</c>'s
/// effects do, so this scope omits <c>EditScope.Models</c> entirely rather than inventing a
/// meaningless always-empty field. <see cref="RespectLocks"/> defaults to <see langword="true"/> —
/// same "explicit, auditable opt-out, never a silent default" precedent as <c>EditScope.RespectLocks</c>.
/// </summary>
public sealed record NoteEditScope(
    TimeWindow Time,
    IReadOnlySet<Guid>? NoteIds = null,
    bool RespectLocks = true);
