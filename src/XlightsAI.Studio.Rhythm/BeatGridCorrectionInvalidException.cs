using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown by <see cref="IBeatGridAutoCorrectionLoop.TryApply"/> when its own request shape cannot run
/// at all (e.g. a non-positive retry budget) — distinct from a per-proposal "malformed proposal"
/// rejection, which is reported through <see cref="BeatGridCorrectionOutcome.RejectionReason"/> instead
/// of thrown, since a malformed individual proposal inside an otherwise valid batch is this step's own
/// explicit, handled failure/edge case, not a caller programming error.
/// </summary>
public sealed class BeatGridCorrectionInvalidException : Exception
{
    public BeatGridCorrectionInvalidException(SongId songId, string reason)
        : base($"Cannot run the beat-grid auto-correction loop for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
