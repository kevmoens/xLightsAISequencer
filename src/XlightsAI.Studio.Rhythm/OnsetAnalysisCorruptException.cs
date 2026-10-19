namespace XlightsAI.Studio.Rhythm;

/// <summary>Mirrors <see cref="EnergyEnvelopeCorruptException"/>: a persisted onset analysis file exists but failed to deserialize.</summary>
public sealed class OnsetAnalysisCorruptException : Exception
{
    public OnsetAnalysisCorruptException(string path, Exception inner)
        : base($"Onset analysis cache at '{path}' is corrupt or not a valid serialized OnsetAnalysis.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
