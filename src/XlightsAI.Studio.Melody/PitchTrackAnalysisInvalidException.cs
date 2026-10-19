namespace XlightsAI.Studio.Melody;

/// <summary>
/// Thrown when an <see cref="IPitchTracker"/> run cannot proceed because its inputs are invalid --
/// either a <see cref="PitchTrackingSettings"/> that does not fit the source stem, or a source file
/// whose real on-disk content no longer matches its declared <c>StemAsset</c> metadata (stale persisted
/// state). Always thrown before any cache file is written, mirroring
/// <c>XlightsAI.Studio.Rhythm.OnsetAnalysisInvalidException</c>'s own "never silently cache as if
/// correct" rule.
/// </summary>
public sealed class PitchTrackAnalysisInvalidException : Exception
{
    public PitchTrackAnalysisInvalidException(string path, string reason)
        : base($"Cannot build a pitch-track analysis for '{path}': {reason}")
    {
        Path = path;
        Reason = reason;
    }

    public string Path { get; }

    public string Reason { get; }
}
