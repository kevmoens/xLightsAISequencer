namespace XlightsAI.Studio.Structure;

/// <summary>Mirrors <see cref="PhraseSegmentationCorruptException"/>: a persisted motif-clustering-analysis file exists but failed to deserialize.</summary>
public sealed class MotifClusteringCorruptException : Exception
{
    public MotifClusteringCorruptException(string path, Exception inner)
        : base($"Motif clustering cache at '{path}' is corrupt or not validly serialized.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
