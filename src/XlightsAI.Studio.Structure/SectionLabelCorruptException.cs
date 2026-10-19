namespace XlightsAI.Studio.Structure;

/// <summary>Mirrors <see cref="SectionBoundaryCorruptException"/>: a persisted section-label-analysis (or override) file exists but failed to deserialize.</summary>
public sealed class SectionLabelCorruptException : Exception
{
    public SectionLabelCorruptException(string path, Exception inner)
        : base($"Section label cache at '{path}' is corrupt or not validly serialized.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
