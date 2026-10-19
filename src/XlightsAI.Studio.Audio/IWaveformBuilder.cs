namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Section 3's proposed waveform-pyramid boundary, verbatim shape. Step 026 ships the
/// first (and, per this step's own scope, only) implementation (<see cref="WaveformPyramidBuilder"/>).
/// </summary>
public interface IWaveformBuilder
{
    /// <summary>
    /// Builds (or, if an up-to-date cache already exists for <paramref name="song"/>, simply
    /// reopens) the zoom-independent multiresolution waveform peaks cache for <paramref name="song"/>,
    /// and returns the real on-disk path it is persisted at. Never writes a cache file until the
    /// full decode has completed and its dimensions have been verified against <paramref name="song"/>
    /// (Step 026 implementation guidance: "write cache only after complete decode and verified
    /// dimensions").
    /// </summary>
    /// <exception cref="FileNotFoundException"><paramref name="song"/>'s source file no longer exists.</exception>
    /// <exception cref="WaveformPyramidInvalidException">The decoded audio's real dimensions do not match <paramref name="song"/>'s own declared metadata.</exception>
    Task<string> BuildPyramidAsync(SongAsset song, CancellationToken ct);
}
