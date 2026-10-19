namespace XlightsAI.Studio.WholeSong;

/// <summary>Mirrors <see cref="XlightsAI.Studio.Composition.CompositionCorruptException"/>'s own convention: a persisted <see cref="SongDirectionPlanRecord"/> file exists but failed to deserialize.</summary>
public sealed class SongDirectionCorruptException : Exception
{
    public SongDirectionCorruptException(string path, Exception inner)
        : base($"Song direction plan cache at '{path}' is corrupt or not a valid serialized SongDirectionPlanRecord.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
