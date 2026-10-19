namespace XlightsAI.Studio.Structure;

/// <summary>Mirrors <see cref="BarFingerprintCorruptException"/>: a persisted section-boundary-analysis file exists but failed to deserialize.</summary>
public sealed class SectionBoundaryCorruptException : Exception
{
    public SectionBoundaryCorruptException(string path, Exception inner)
        : base($"Section boundary analysis cache at '{path}' is corrupt or not a valid serialized SectionBoundaryAnalysis.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
