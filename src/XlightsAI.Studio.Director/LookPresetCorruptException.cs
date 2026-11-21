namespace XlightsAI.Studio.Director;

/// <summary>Mirrors the codebase-wide <c>*CorruptException(path, inner)</c> pattern (see <see cref="ShowBibleCorruptException"/>): a persisted <c>LookPreset</c> file exists but failed to deserialize.</summary>
public sealed class LookPresetCorruptException : Exception
{
    public LookPresetCorruptException(string path, Exception inner)
        : base($"Look preset file at '{path}' is corrupt or not a valid serialized LookPreset.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
