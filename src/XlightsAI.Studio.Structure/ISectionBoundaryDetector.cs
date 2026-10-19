namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 053's analysis boundary: computes (or reopens a still-valid cached)
/// <see cref="SectionBoundaryAnalysis"/> from an already built <see cref="SectionBoundaryContext"/>. A
/// phase-local, concrete precursor to PHASE-08 Section 3's proposed broader structure-analysis pipeline
/// -- this step never assigns section labels or phrase membership (Step 054/055's own later scope); it
/// only proposes where a segment boundary candidate exists, with confidence and an error report.
/// </summary>
public interface ISectionBoundaryDetector
{
    /// <summary>
    /// Produces a <see cref="SectionBoundaryAnalysis"/> for <paramref name="context"/> using
    /// <paramref name="settings"/>. Validates every input explicitly before any computation or
    /// persistence occurs, and never persists a malformed result (Step 053's own "Malformed proposal...
    /// must not auto-commit" failure/edge case, verbatim). A <paramref name="context"/> whose
    /// <see cref="SectionBoundaryContext.FingerprintSet"/> carries no bars (e.g. built from silence) is
    /// an explicit, handled case: it returns a <see cref="SectionBoundaryAnalysis"/> with an empty
    /// <see cref="SectionBoundaryAnalysis.Boundaries"/> list and a trivially converged
    /// <see cref="SectionBoundaryErrorReport"/> rather than throwing.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="SectionBoundaryInvalidException">
    /// Missing media, a mismatched/stale provenance chain, an unusable <paramref name="settings"/>, or a
    /// computed candidate list that fails structural validation.
    /// </exception>
    Task<SectionBoundaryAnalysis> DetectAsync(SectionBoundaryContext context, SectionBoundarySettings settings, CancellationToken ct);
}
