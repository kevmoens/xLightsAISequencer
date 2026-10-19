namespace XlightsAI.Studio.AiEdits;

/// <summary>Mirrors <see cref="XlightsAI.Studio.Composition.CompositionCorruptException"/>'s own convention: a persisted <see cref="SelectionContext"/> file exists but failed to deserialize.</summary>
public sealed class SelectionContextCorruptException : Exception
{
    public SelectionContextCorruptException(string path, Exception inner)
        : base($"Selection context cache at '{path}' is corrupt or not a valid serialized SelectionContext.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
