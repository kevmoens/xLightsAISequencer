namespace XlightsAI.Studio.Melody;

/// <summary>Mirrors <c>XlightsAI.Studio.Stems.StemActivityResultCorruptException</c>: a persisted melody-track file exists but failed to deserialize.</summary>
public sealed class MelodyTrackCorruptException : Exception
{
    public MelodyTrackCorruptException(string path, Exception inner)
        : base($"Melody track at '{path}' is corrupt or not a valid serialized MelodyTrack.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
