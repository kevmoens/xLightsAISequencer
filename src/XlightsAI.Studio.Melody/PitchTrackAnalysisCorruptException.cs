namespace XlightsAI.Studio.Melody;

/// <summary>Mirrors <see cref="MelodyTrackCorruptException"/>: a persisted pitch-track analysis file exists but failed to deserialize.</summary>
public sealed class PitchTrackAnalysisCorruptException : Exception
{
    public PitchTrackAnalysisCorruptException(string path, Exception inner)
        : base($"Pitch-track analysis cache at '{path}' is corrupt or not a valid serialized PitchTrackAnalysis.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
