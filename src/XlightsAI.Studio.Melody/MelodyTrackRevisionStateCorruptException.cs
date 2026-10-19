namespace XlightsAI.Studio.Melody;

/// <summary>Mirrors <see cref="MelodyTrackCorruptException"/>: a persisted <see cref="MelodyTrackRevisionState"/> file exists but failed to deserialize.</summary>
public sealed class MelodyTrackRevisionStateCorruptException : Exception
{
    public MelodyTrackRevisionStateCorruptException(string path, Exception inner)
        : base($"Melody track revision state at '{path}' is corrupt or not a valid serialized MelodyTrackRevisionState.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
