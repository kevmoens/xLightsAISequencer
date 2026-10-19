namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Thrown by <see cref="ISequenceDocumentStore.LoadAsync"/> when a file at the given path exists but
/// does not deserialize into a valid <see cref="SequenceDocument"/> — mirrors
/// <c>ProjectManifestCorruptException</c>'s existing "corrupt JSON is reported, never crashes the
/// caller or is silently treated as an empty document" convention.
/// </summary>
public sealed class SequenceDocumentCorruptException : Exception
{
    public SequenceDocumentCorruptException(string path, Exception innerException)
        : base($"'{path}' does not contain a valid serialized SequenceDocument.", innerException)
    {
    }
}
