namespace XlightsAI.Studio.Refinement;

/// <summary>Mirrors <see cref="XlightsAI.Studio.Composition.CompositionCorruptException"/>'s own convention: a persisted run file exists but failed to deserialize.</summary>
public sealed class RefinementRunCorruptException : Exception
{
    public RefinementRunCorruptException(string path, Exception inner)
        : base($"Refinement run file at '{path}' is corrupt or not a valid serialized RefinementRunRecord.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
