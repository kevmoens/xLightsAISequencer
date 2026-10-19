namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 056's analysis boundary: computes (or reopens a still-valid cached) <see cref="MotifClusteringAnalysis"/>
/// from an already-built <see cref="MotifClusteringContext"/>. A phrase-local, concrete precursor to
/// PHASE-08 Section 3's proposed broader <c>IMotifMatcher</c> -- this step never builds choreography
/// inheritance or lets a human approve/reject a proposed link (Step 057/058's own later scope); it only
/// proposes acoustic-similarity-scored groups from already-approved <see cref="PhraseCandidate"/>s.
/// </summary>
public interface IMotifClusterer
{
    /// <summary>
    /// Produces a <see cref="MotifClusteringAnalysis"/> for <paramref name="context"/> using
    /// <paramref name="settings"/>. Validates every input explicitly before any computation or persistence
    /// occurs, and never persists a malformed result (Step 056's own "Invalid input... must not corrupt the
    /// project" failure/edge case, verbatim). A <paramref name="context"/> whose
    /// <see cref="MotifClusteringContext.PhraseAnalysis"/> carries fewer than two phrases is an explicit,
    /// handled case: it returns a <see cref="MotifClusteringAnalysis"/> with empty
    /// <see cref="MotifClusteringAnalysis.Groups"/>/<see cref="MotifClusteringAnalysis.Links"/> rather than
    /// throwing (no pair exists to compare).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="MotifClusteringInvalidException">
    /// Missing media, a mismatched/stale provenance chain, an unusable <paramref name="settings"/>, or a
    /// phrase whose bar range references a bar that does not exist in <paramref name="context"/>'s own
    /// <see cref="MotifClusteringContext.FingerprintSet"/>.
    /// </exception>
    Task<MotifClusteringAnalysis> ClusterAsync(MotifClusteringContext context, MotifClusteringSettings settings, CancellationToken ct);
}
