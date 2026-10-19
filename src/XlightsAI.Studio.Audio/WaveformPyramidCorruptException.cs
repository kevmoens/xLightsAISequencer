namespace XlightsAI.Studio.Audio;

/// <summary>Mirrors <see cref="SongAssetCorruptException"/>: a persisted waveform-pyramid cache file exists but failed to deserialize.</summary>
public sealed class WaveformPyramidCorruptException : Exception
{
    public WaveformPyramidCorruptException(string path, Exception inner)
        : base($"Waveform pyramid cache at '{path}' is corrupt or not a valid serialized WaveformPyramidCache.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
