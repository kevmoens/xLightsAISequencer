using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// Step 046's own analysis boundary: computes (or reopens a still-valid cached) <see cref="PitchTrackAnalysis"/>
/// for an isolated-lead <see cref="StemAsset"/>. A narrower, concrete precursor to Section 3's proposed
/// <c>IMelodyAnalyzer.AnalyzeAsync</c> (which returns a full note/gesture-segmented <c>MelodyTrack</c>) --
/// this step's own scope is only the raw per-hop fundamental-frequency contour those later steps
/// (048-049) consume, per this step's own "Implement fundamental-frequency/pitch tracking for isolated
/// lead source" title.
/// </summary>
public interface IPitchTracker
{
    /// <summary>
    /// Produces a <see cref="PitchTrackAnalysis"/> for <paramref name="source"/> using <paramref name="settings"/>.
    /// Validates every input explicitly before any decode or persistence occurs (Step 046's own "invalid
    /// input... must not corrupt the project" failure/edge case) and never persists a partial/invalid result.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">The stem's declared audio file no longer exists on disk.</exception>
    /// <exception cref="PitchTrackAnalysisInvalidException"><paramref name="settings"/> is not valid for <paramref name="source"/>, or the source file's real content no longer matches <paramref name="source"/>'s declared metadata (stale/tampered state).</exception>
    Task<PitchTrackAnalysis> TrackAsync(StemAsset source, PitchTrackingSettings settings, CancellationToken ct);
}
