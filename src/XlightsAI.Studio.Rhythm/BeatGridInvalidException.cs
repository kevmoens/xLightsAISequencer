using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown when an <see cref="IBeatGridBuilder"/> run cannot proceed because its inputs are invalid —
/// a <see cref="BeatGridSettings"/> meter/tolerance that is not usable, or a supplied
/// <see cref="TempoAnalysis"/>/<see cref="OnsetAnalysis"/> pair that does not actually belong
/// together. Caught before any cache file is written, mirroring
/// <see cref="TempoAnalysisInvalidException"/>'s own "never silently cache as if correct" rule.
/// </summary>
public sealed class BeatGridInvalidException : Exception
{
    public BeatGridInvalidException(SongId songId, string reason)
        : base($"Cannot build a beat grid for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
