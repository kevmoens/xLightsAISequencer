using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Thrown when an <see cref="IBarFingerprintBuilder"/> run cannot proceed because its inputs are invalid
/// -- missing media, a mismatched/stale provenance chain, an unusable <see cref="BarFingerprintSettings"/>,
/// or a computed bar window that collapses to a non-positive span. Caught before any cache file is
/// written, mirroring <see cref="XlightsAI.Studio.Rhythm.BeatGridInvalidException"/>'s own "never
/// silently cache as if correct" rule.
/// </summary>
public sealed class BarFingerprintInvalidException : Exception
{
    public BarFingerprintInvalidException(SongId songId, string reason)
        : base($"Cannot build bar fingerprints for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
