using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown when an <see cref="ITempoDetector"/> run cannot proceed because its inputs are invalid — a
/// <see cref="TempoDetectionSettings"/> scan range/resolution that is not usable. Caught before any
/// cache file is written, mirroring <see cref="OnsetAnalysisInvalidException"/>'s own "never silently
/// cache as if correct" rule.
/// </summary>
public sealed class TempoAnalysisInvalidException : Exception
{
    public TempoAnalysisInvalidException(SongId songId, string reason)
        : base($"Cannot build a tempo analysis for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
