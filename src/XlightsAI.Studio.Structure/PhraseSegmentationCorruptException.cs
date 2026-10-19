namespace XlightsAI.Studio.Structure;

/// <summary>Mirrors <see cref="SectionLabelCorruptException"/>: a persisted phrase-segmentation-analysis (or boundary-override) file exists but failed to deserialize.</summary>
public sealed class PhraseSegmentationCorruptException : Exception
{
    public PhraseSegmentationCorruptException(string path, Exception inner)
        : base($"Phrase segmentation cache at '{path}' is corrupt or not validly serialized.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
