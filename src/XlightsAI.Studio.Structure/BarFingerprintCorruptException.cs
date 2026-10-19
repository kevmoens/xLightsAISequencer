namespace XlightsAI.Studio.Structure;

/// <summary>Mirrors <see cref="XlightsAI.Studio.Rhythm.BeatGridCorruptException"/>: a persisted fingerprint-set file exists but failed to deserialize.</summary>
public sealed class BarFingerprintCorruptException : Exception
{
    public BarFingerprintCorruptException(string path, Exception inner)
        : base($"Bar fingerprint cache at '{path}' is corrupt or not a valid serialized SongFingerprintSet.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
