namespace XlightsAI.Studio.Melody;

/// <summary>
/// Thrown by <see cref="MelodyTrackValidator.Validate"/> when a <see cref="MelodyTrack"/>/<see cref="NoteEvent"/>
/// cannot be accepted as-is and cannot be safely normalized -- this step's own named failure/edge cases:
/// invalid timing boundaries (a note whose <c>EndTicks &lt;= StartTicks</c>, or a note-to-note overlap
/// that cannot be trimmed without producing a zero-length note), an out-of-range unit
/// (<see cref="NoteEvent.MidiNote"/> outside <c>0-127</c>, or a <see cref="NoteEvent.Velocity"/>/
/// <see cref="NoteEvent.Confidence"/>/<see cref="MelodicGesture.Confidence"/> outside <c>[0, 1]</c>),
/// missing media (an empty <see cref="MelodyTrack.SourceAssetHash"/>), or a <see cref="MelodicGesture"/>
/// whose <see cref="MelodicGesture.NoteId"/> does not reference any note in the same track. Mirrors
/// <c>XlightsAI.Studio.Stems.StemActivityDetectionInvalidException</c>'s own "caught before any
/// persistence" precedent -- never thrown after a partial write.
/// </summary>
public sealed class MelodyTrackInvalidException : Exception
{
    public MelodyTrackInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
