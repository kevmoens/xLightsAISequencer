using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown when an <see cref="IBeatGridEditor"/> operation cannot proceed because its inputs are
/// invalid — an unknown <see cref="BeatMarker.Id"/>, an out-of-range/negative time, or a grid that
/// cannot support the requested edit. Mirrors <see cref="BeatGridInvalidException"/>'s own "never
/// silently apply as if correct" rule — no mutated <see cref="BeatGrid"/> is ever returned or
/// persisted for an invalid edit.
/// </summary>
public sealed class BeatGridEditInvalidException : Exception
{
    public BeatGridEditInvalidException(SongId songId, string reason)
        : base($"Cannot edit the beat grid for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
