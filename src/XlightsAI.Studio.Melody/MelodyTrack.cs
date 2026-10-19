namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Section 3's own proposed signature, verbatim: the versioned, persisted output of melody
/// analysis -- every <see cref="NoteEvent"/> plus the <see cref="MelodicGesture"/>s derived from them,
/// tied back to the exact source audio via <see cref="SourceAssetHash"/> (Section 4: "source stem hash"
/// is part of every cache key). Never constructed directly with unvalidated data -- always pass through
/// <see cref="MelodyTrackValidator.Validate"/> first (see that type for the normalization/rejection
/// rules this step's own acceptance text requires).
/// </summary>
/// <param name="Id">Stable identity for this one analyzed/corrected track revision.</param>
/// <param name="SourceAssetHash">SHA-256 (lower-case hex) of the source audio (trusted MIDI file or isolated-stem WAV) this track was derived from. Never null/empty -- an empty value means "missing media," this step's own named failure/edge case.</param>
/// <param name="Notes">Every note, ordered by <see cref="NoteEvent.StartTicks"/>, non-overlapping (half-open). May be empty -- a fully silent source is a valid, inspectable track, not an error.</param>
/// <param name="Gestures">Every derived gesture; each <see cref="MelodicGesture.NoteId"/> must reference an entry in <see cref="Notes"/>.</param>
public sealed record MelodyTrack(
    Guid Id,
    string SourceAssetHash,
    IReadOnlyList<NoteEvent> Notes,
    IReadOnlyList<MelodicGesture> Gestures);
