namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 034's own analysis boundary: computes (or reopens a still-valid cached) <see cref="BeatGrid"/>
/// from an already built <see cref="OnsetAnalysis"/>/<see cref="TempoAnalysis"/> pair. A narrower,
/// concrete precursor to Section 3's proposed <c>IRhythmAnalyzer.AnalyzeAsync</c> (which returns a
/// full <c>BeatGrid</c> directly from a <c>SongAsset</c>) — the same deliberate narrowing precedent
/// Step 033's own <c>ITempoDetector</c> already set (see that step's own evidence, §3): this step
/// never re-decodes audio, never re-derives onsets, and never re-ranks tempo candidates. It always
/// selects the single top-ranked (highest-confidence) entry of <see cref="TempoAnalysis.Candidates"/>
/// to generate the grid from — a disclosed scope limit; letting a user pick a different, lower-ranked
/// candidate or supply a manual anchor is Step 035's own "beat-grid manual correction" scope, not
/// this step's.
/// </summary>
public interface IBeatGridBuilder
{
    /// <summary>
    /// Produces a <see cref="BeatGrid"/> for <paramref name="onsets"/>/<paramref name="tempo"/> using
    /// <paramref name="settings"/>. Validates every input explicitly before any computation or
    /// persistence occurs (Step 034's own "invalid timing boundaries... must be handled explicitly"
    /// failure/edge case) and never persists a partial/invalid result. A <paramref name="tempo"/> with
    /// zero candidates (e.g. built from silence) is an explicit, handled case: it returns a
    /// <see cref="BeatGrid"/> with a <see langword="null"/> <see cref="BeatGrid.SelectedTempo"/> and an
    /// empty <see cref="BeatGrid.Beats"/> list rather than throwing.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="onsets"/>, <paramref name="tempo"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeatGridInvalidException">
    /// <paramref name="settings"/>'s meter/tolerance is not usable, or <paramref name="tempo"/> was not
    /// built from <paramref name="onsets"/> (a mismatched <see cref="TempoAnalysis.SourceOnsetAnalysisVersionId"/>/<see cref="OnsetAnalysis.VersionId"/>
    /// pair, or a mismatched <see cref="OnsetAnalysis.SongId"/>/<see cref="TempoAnalysis.SongId"/> pair).
    /// </exception>
    Task<BeatGrid> BuildAsync(OnsetAnalysis onsets, TempoAnalysis tempo, BeatGridSettings settings, CancellationToken ct);
}
