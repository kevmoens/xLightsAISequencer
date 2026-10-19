namespace XlightsAI.Studio.Melody;

/// <summary>
/// Thrown by <see cref="INoteTrackEditor"/> when a <see cref="CorrectNoteCommand"/>/
/// <see cref="SetNoteLockedCommand"/> fails any validation gate (<see cref="NoteEditRejectionReason"/>).
/// Always thrown before any <see cref="MelodyTrackRevisionState"/> mutation is constructed or
/// persisted — the same "no partial mutation" rule <c>XlightsAI.Studio.Editing.EditCommandRejectedException</c>
/// already enforces for PHASE-03's command pipeline, applied here to Step 049's own.
/// </summary>
public sealed class NoteCorrectionRejectedException : Exception
{
    public NoteCorrectionRejectedException(NoteEditRejectionReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public NoteEditRejectionReason Reason { get; }
}
