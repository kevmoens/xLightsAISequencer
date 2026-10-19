namespace XlightsAI.Studio.Audio;

/// <summary>
/// Thrown when <see cref="WaveformPyramidBuilder"/> decodes a song's real audio and finds its actual
/// dimensions (decoded sample count) do not match the <see cref="SongAsset"/>'s own declared
/// metadata — an "invalid timing boundary" (Step 026 failure/edge case) caught before any cache file
/// is written, never silently cached as if it were correct.
/// </summary>
public sealed class WaveformPyramidInvalidException : Exception
{
    public WaveformPyramidInvalidException(string path, string reason)
        : base($"Cannot build a waveform pyramid for '{path}': {reason}")
    {
        Path = path;
        Reason = reason;
    }

    public string Path { get; }

    public string Reason { get; }
}
