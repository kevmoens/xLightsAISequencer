namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 033's own analysis boundary: computes (or reopens a still-valid cached) ranked
/// <see cref="TempoAnalysis"/> for an already built <see cref="OnsetAnalysis"/>. A narrower, concrete
/// precursor to Section 3's proposed <c>IRhythmAnalyzer.AnalyzeAsync</c> (which returns a full
/// <c>BeatGrid</c>) — this step's own scope is only the ranked BPM candidate list those later steps
/// (034+) consume, per this step's own "Implement BPM candidate detection including half/double-time
/// alternatives" title. Deliberately takes an already-computed <see cref="OnsetAnalysis"/>, not a
/// <c>SongAsset</c> — Step 032's own onset candidates are this step's sole algorithmic input (Section 2:
/// "Separate raw audio features from human-approved beat grid"); this step never re-decodes audio.
/// </summary>
public interface ITempoDetector
{
    /// <summary>
    /// Produces a <see cref="TempoAnalysis"/> for <paramref name="onsets"/> using <paramref name="settings"/>'s
    /// scan range/resolution. Validates every input explicitly before any computation or persistence
    /// occurs (Step 033's own "invalid timing boundaries... must be handled explicitly" failure/edge
    /// case) and never persists a partial/invalid result. An <paramref name="onsets"/> with zero
    /// detected onsets (e.g. silence or missing/empty media) is an explicit, handled case: it returns a
    /// <see cref="TempoAnalysis"/> with an empty <see cref="TempoAnalysis.Candidates"/> list rather than
    /// throwing.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="onsets"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="TempoAnalysisInvalidException"><paramref name="settings"/>'s BPM range/resolution/candidate count is not usable.</exception>
    Task<TempoAnalysis> DetectAsync(OnsetAnalysis onsets, TempoDetectionSettings settings, CancellationToken ct);
}
