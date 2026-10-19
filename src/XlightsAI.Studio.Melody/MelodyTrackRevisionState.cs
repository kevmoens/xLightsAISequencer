using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 049's own "Versioned, inspectable project output" (user-visible outcome, verbatim).
/// Section 4's own rule, verbatim: "Store original transcription version and corrected MelodyTrack as
/// separate revisions" -- <see cref="Original"/> is the exact track an analyzer (or Step 048's MIDI
/// importer) first produced, never mutated or overwritten by any later correction; <see cref="Current"/>
/// is the latest accepted, gesture-reclassified state after zero or more corrections.
/// <see cref="Revision"/> is the optimistic-concurrency token every <see cref="CorrectNoteCommand"/>/
/// <see cref="SetNoteLockedCommand"/> must present an exact match for (this step's own "stale revision"
/// failure/edge case).
///
/// <see cref="LockedNoteIds"/> tracks which notes a user has explicitly locked -- deliberately not a
/// field on <see cref="NoteEvent"/> itself (that Step 045 contract stays untouched; see
/// <see cref="NoteTrackEditor"/>'s own architecture-deviation note). Typed as
/// <see cref="IReadOnlyList{T}"/>, not <c>IReadOnlySet&lt;Guid&gt;</c>: disclosed, deliberate --
/// <c>System.Text.Json</c>'s built-in converters cannot deserialize into
/// <c>IReadOnlySet&lt;T&gt;</c> (confirmed by a real round-trip failure during this step's own
/// implementation: <c>NotSupportedException: The collection type
/// 'System.Collections.Generic.IReadOnlySet`1[System.Guid]' is abstract, an interface, or is read
/// only...</c>), while <see cref="IReadOnlyList{T}"/> already round-trips correctly (the same type
/// <see cref="MelodyTrack.Notes"/> itself uses). Uniqueness is still enforced at every write site
/// (<see cref="NoteTrackEditor"/> always builds this field from a <see cref="HashSet{T}"/> before
/// persisting).
///
/// <see cref="ClassificationSettings"/> is preserved so every correction reclassifies gestures with the
/// exact same tunable thresholds the original analysis used, rather than silently drifting to a
/// different default.
/// </summary>
public sealed record MelodyTrackRevisionState(
    Guid TrackId,
    MelodyTrack Original,
    MelodyTrack Current,
    DocumentRevision Revision,
    IReadOnlyList<Guid> LockedNoteIds,
    GestureClassificationSettings ClassificationSettings);
