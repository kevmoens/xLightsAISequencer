using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 032's own analysis boundary: computes (or reopens a still-valid cached) <see cref="OnsetAnalysis"/>
/// for a probed <see cref="SongAsset"/>. A narrower, concrete precursor to Section 3's proposed
/// <c>IRhythmAnalyzer.AnalyzeAsync</c> (which returns a full <c>BeatGrid</c>) — this step's own scope
/// is only the onset/transient candidate list those later steps consume, per this step's own
/// "Implement onset/transient detection with configurable sensitivity" title.
/// </summary>
public interface IOnsetDetector
{
    /// <summary>
    /// Produces an <see cref="OnsetAnalysis"/> for <paramref name="song"/> using <paramref name="settings"/>'s
    /// windowing and <paramref name="onsetSettings"/>'s sensitivity. Validates every input explicitly
    /// before any decode or persistence occurs (Step 032's own "invalid input... must not corrupt the
    /// project" failure/edge case) and never persists a partial/invalid result.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="song"/>, <paramref name="settings"/> or <paramref name="onsetSettings"/> is <see langword="null"/>.</exception>
    /// <exception cref="OnsetAnalysisInvalidException"><paramref name="settings"/>/<paramref name="onsetSettings"/> are not valid for <paramref name="song"/>, or the decoded signal does not match <paramref name="song"/>'s declared dimensions.</exception>
    Task<OnsetAnalysis> DetectAsync(SongAsset song, RhythmSettings settings, OnsetDetectionSettings onsetSettings, CancellationToken ct);
}
