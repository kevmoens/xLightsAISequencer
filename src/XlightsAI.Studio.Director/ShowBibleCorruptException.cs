namespace XlightsAI.Studio.Director;

/// <summary>Mirrors the codebase-wide <c>*CorruptException(path, inner)</c> pattern: a persisted Show Bible file exists but failed to deserialize.</summary>
public sealed class ShowBibleCorruptException : Exception
{
    public ShowBibleCorruptException(string path, Exception inner)
        : base($"Show Bible cache at '{path}' is corrupt or not a valid serialized ShowBible.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
