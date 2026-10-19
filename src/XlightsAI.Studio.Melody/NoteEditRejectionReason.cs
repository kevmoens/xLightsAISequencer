namespace XlightsAI.Studio.Melody;

/// <summary>
/// Why a <see cref="CorrectNoteCommand"/>/<see cref="SetNoteLockedCommand"/> was rejected by
/// <see cref="INoteTrackEditor"/>. Step 049's own required failure/edge case, verbatim: "Out-of-scope,
/// locked or stale revision must be rejected without partial mutation" — the exact same three-category
/// cross-cutting rule PHASE-03's <c>EditRejectionReason</c> already names for
/// <c>SequenceDocument</c>/<c>EffectInstance</c> edits, applied here to <see cref="MelodyTrack"/>/
/// <see cref="NoteEvent"/> corrections. A distinct, phase-local enum rather than reusing
/// <c>XlightsAI.Studio.Editing.EditRejectionReason</c> directly: that enum's own members
/// (<c>EffectNotFound</c>, <c>UnsupportedParameter</c>, ...) describe effect/recipe concepts that do
/// not exist on a <see cref="NoteEvent"/>, and Melody must not take a dependency on
/// <c>XlightsAI.Studio.Editing</c> just to borrow three shared names.
/// </summary>
public enum NoteEditRejectionReason
{
    StaleRevision,
    NoteNotFound,
    OutOfScope,
    Locked,
}
