namespace XlightsAI.Studio.Structure;

/// <summary>Mirrors <see cref="MotifClusteringCorruptException"/>: a persisted choreography-template-analysis file exists but failed to deserialize.</summary>
public sealed class ChoreographyTemplateCorruptException : Exception
{
    public ChoreographyTemplateCorruptException(string path, Exception inner)
        : base($"Choreography template cache at '{path}' is corrupt or not validly serialized.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
