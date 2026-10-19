using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 049's own revision-aware note-correction request — see
/// <see cref="INoteTrackEditor"/>'s own doc comment for why this, not Section 3's bare
/// <c>CorrectAsync(Guid trackId, NoteEvent corrected, CancellationToken ct)</c> proposed signature, is
/// this step's real command shape. <see cref="ExpectedRevision"/> is the optimistic-concurrency check
/// (this step's own failure/edge case, verbatim: "...stale revision must be rejected") — never applied
/// when it does not match the track revision state's current <see cref="MelodyTrackRevisionState.Revision"/>,
/// no matter how well-formed <see cref="Corrected"/> is. <see cref="Corrected"/>'s own
/// <see cref="NoteEvent.Id"/> identifies which existing note is being replaced; every other field on it
/// is the new, corrected value (pitch and/or timing) — "drag/resize" (implementation guidance) is
/// exactly "move/resize this note's own <c>StartTicks</c>/<c>EndTicks</c>/<c>MidiNote</c>."
/// </summary>
public sealed record CorrectNoteCommand(
    DocumentRevision ExpectedRevision,
    NoteEditScope Scope,
    NoteEvent Corrected);
