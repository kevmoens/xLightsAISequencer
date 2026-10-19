using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown by <see cref="IBeatGridQualityGate.Evaluate"/> when its own <see cref="BeatGridQualityGateSettings"/>
/// cannot produce a meaningful report — mirrors <see cref="BeatGridInvalidException"/>'s own "never
/// silently apply as if correct" rule.
/// </summary>
public sealed class BeatGridQualityGateInvalidException : Exception
{
    public BeatGridQualityGateInvalidException(SongId songId, string reason)
        : base($"Cannot evaluate beat grid quality for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
