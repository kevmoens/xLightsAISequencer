namespace XlightsAI.Studio.Rhythm;

/// <summary>Mirrors <see cref="OnsetAnalysisCorruptException"/>: a persisted tempo analysis file exists but failed to deserialize.</summary>
public sealed class TempoAnalysisCorruptException : Exception
{
    public TempoAnalysisCorruptException(string path, Exception inner)
        : base($"Tempo analysis cache at '{path}' is corrupt or not a valid serialized TempoAnalysis.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
