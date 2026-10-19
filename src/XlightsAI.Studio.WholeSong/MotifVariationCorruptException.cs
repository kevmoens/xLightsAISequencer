namespace XlightsAI.Studio.WholeSong;

/// <summary>Mirrors <see cref="SongDirectionCorruptException"/>'s own convention: a persisted <see cref="MotifVariationPlanRecord"/> file exists but failed to deserialize.</summary>
public sealed class MotifVariationCorruptException : Exception
{
    public MotifVariationCorruptException(string path, Exception inner)
        : base($"Motif variation plan cache at '{path}' is corrupt or not a valid serialized MotifVariationPlanRecord.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
