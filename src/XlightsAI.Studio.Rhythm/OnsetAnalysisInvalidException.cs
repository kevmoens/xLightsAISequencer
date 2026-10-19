namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown when an <see cref="IOnsetDetector"/> run cannot proceed because its inputs are invalid —
/// either <see cref="RhythmSettings"/>/<see cref="OnsetDetectionSettings"/> that do not fit the source
/// song, or a decoded signal whose real dimensions do not match the song's own declared metadata.
/// Caught before any cache file is written, mirroring <see cref="EnergyEnvelopeInvalidException"/>'s
/// own "never silently cache as if correct" rule.
/// </summary>
public sealed class OnsetAnalysisInvalidException : Exception
{
    public OnsetAnalysisInvalidException(string path, string reason)
        : base($"Cannot build an onset analysis for '{path}': {reason}")
    {
        Path = path;
        Reason = reason;
    }

    public string Path { get; }

    public string Reason { get; }
}
