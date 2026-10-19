namespace XlightsAI.Studio.Rhythm;

/// <summary>Mirrors <see cref="TempoAnalysisCorruptException"/>: a persisted beat grid file exists but failed to deserialize.</summary>
public sealed class BeatGridCorruptException : Exception
{
    public BeatGridCorruptException(string path, Exception inner)
        : base($"Beat grid cache at '{path}' is corrupt or not a valid serialized BeatGrid.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
