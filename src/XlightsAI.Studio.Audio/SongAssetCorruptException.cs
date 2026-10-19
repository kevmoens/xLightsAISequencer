namespace XlightsAI.Studio.Audio;

/// <summary>Mirrors <c>SequenceDocumentCorruptException</c>: a persisted song-asset record file exists but failed to deserialize.</summary>
public sealed class SongAssetCorruptException : Exception
{
    public SongAssetCorruptException(string path, Exception inner)
        : base($"Song asset record at '{path}' is corrupt or not a valid serialized SongAsset.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
