namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 055's analysis boundary: computes (or reopens a still-valid cached) automatic
/// <see cref="PhraseSegmentationAnalysis"/> from an already-built <see cref="PhraseSegmentationContext"/>,
/// merged with any persisted manual boundary corrections, and lets a caller record a new correction. A
/// phrase-local, concrete precursor to PHASE-08 Section 3's proposed broader <c>ISongStructureAnalyzer</c>
/// -- this step never clusters motifs or builds inheritance links (Step 056/057's own later scope); it
/// only partitions each already-approved <see cref="SectionLabelCandidate"/> segment into one or more
/// contiguous, bar-aligned <see cref="PhraseCandidate"/>s.
/// </summary>
public interface IPhraseSegmenter
{
    /// <summary>
    /// Produces a <see cref="PhraseSegmentationAnalysis"/> for <paramref name="context"/> using
    /// <paramref name="settings"/>, with every persisted <see cref="IPhraseBoundaryOverrideStore"/> entry
    /// for this song already applied. Validates every input explicitly before any computation or
    /// persistence occurs, and never persists a malformed automatic result (Step 055's own "Invalid
    /// input... must not corrupt the project" failure/edge case, verbatim). A <paramref name="context"/>
    /// whose <see cref="PhraseSegmentationContext.LabelAnalysis"/> carries no labels (e.g. built from
    /// silence) is an explicit, handled case: it returns a <see cref="PhraseSegmentationAnalysis"/> with an
    /// empty <see cref="PhraseSegmentationAnalysis.Phrases"/> list rather than throwing.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="PhraseSegmentationInvalidException">
    /// Missing media, a mismatched/stale provenance chain, an unusable <paramref name="settings"/>, or a
    /// computed phrase list that fails structural validation.
    /// </exception>
    Task<PhraseSegmentationAnalysis> SegmentAsync(PhraseSegmentationContext context, PhraseSegmentationSettings settings, CancellationToken ct);

    /// <summary>
    /// Records a human correction of <paramref name="sectionLabelId"/>'s internal phrase split points to
    /// exactly <paramref name="boundaryBarIndices"/> (strictly increasing bar indices strictly between that
    /// section's own start and end bars; an empty list means "no manual split points -- one phrase spans
    /// the whole section") in the separately versioned <see cref="IPhraseBoundaryOverrideStore"/>, then
    /// returns the resulting merged <see cref="PhraseSegmentationAnalysis"/> (Step 055's own required
    /// acceptance, verbatim: "...with manual corrections"). <paramref name="sectionLabelId"/> must
    /// reference a section present in <paramref name="context"/>'s current
    /// <see cref="PhraseSegmentationContext.LabelAnalysis"/>, and every supplied bar index must reference a
    /// bar that actually exists in <paramref name="context"/>'s own
    /// <see cref="PhraseSegmentationContext.FingerprintSet"/> and falls strictly inside that section's own
    /// bar range -- any of these failing is this step's own "invalid input... must not corrupt the
    /// project" failure/edge case and is rejected before anything is persisted.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="settings"/> or <paramref name="boundaryBarIndices"/> is <see langword="null"/>.</exception>
    /// <exception cref="PhraseSegmentationInvalidException">Inputs are invalid, <paramref name="sectionLabelId"/> does not reference a current section, or <paramref name="boundaryBarIndices"/> is malformed.</exception>
    Task<PhraseSegmentationAnalysis> SetManualBoundariesAsync(
        PhraseSegmentationContext context, PhraseSegmentationSettings settings, Guid sectionLabelId, IReadOnlyList<int> boundaryBarIndices, CancellationToken ct);
}
