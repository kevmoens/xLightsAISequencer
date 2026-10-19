namespace XlightsAI.Studio.Display;

/// <summary>Mirrors <see cref="XlightsAI.Studio.Structure.BarFingerprintCorruptException"/>'s own convention: a persisted <see cref="SpatialDisplayGraph"/> file exists but failed to deserialize.</summary>
public sealed class SpatialDisplayGraphCorruptException : Exception
{
    public SpatialDisplayGraphCorruptException(string path, Exception inner)
        : base($"Spatial display graph cache at '{path}' is corrupt or not a valid serialized SpatialDisplayGraph.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
