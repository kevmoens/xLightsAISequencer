namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 052's own analysis boundary: computes (or reopens a still-valid cached) <see cref="SongFingerprintSet"/>
/// from an already built <see cref="BarFingerprintContext"/>. A phase-local, concrete precursor to
/// PHASE-08 Section 3's proposed broader structure-analysis pipeline -- this step never detects section
/// boundaries or motif links (Steps 053+); it only computes the per-bar feature vectors those later
/// steps will compare.
/// </summary>
public interface IBarFingerprintBuilder
{
    /// <summary>
    /// Produces a <see cref="SongFingerprintSet"/> for <paramref name="context"/> using
    /// <paramref name="settings"/>. Validates every input explicitly before any computation or
    /// persistence occurs (Step 052's own "invalid timing boundaries... must be handled explicitly"
    /// failure/edge case) and never persists a partial/invalid result. A <paramref name="context"/>
    /// whose <see cref="BarFingerprintContext.BeatGrid"/> carries no beats (e.g. built from silence) is
    /// an explicit, handled case: it returns a <see cref="SongFingerprintSet"/> with an empty
    /// <see cref="SongFingerprintSet.Bars"/> list rather than throwing.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="BarFingerprintInvalidException">
    /// Missing media, a mismatched/stale provenance chain, an unusable <paramref name="settings"/>, or a
    /// computed bar window that collapses to a non-positive span.
    /// </exception>
    Task<SongFingerprintSet> BuildAsync(BarFingerprintContext context, BarFingerprintSettings settings, CancellationToken ct);
}
