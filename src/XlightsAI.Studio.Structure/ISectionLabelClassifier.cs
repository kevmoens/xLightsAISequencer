namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 054's analysis boundary: computes (or reopens a still-valid cached) automatic
/// <see cref="SectionLabelAnalysis"/> from an already-built <see cref="SectionLabelContext"/>, merged with
/// any persisted user overrides, and lets a caller record a new override. A phase-local, concrete
/// precursor to PHASE-08 Section 3's proposed broader <c>ISongStructureAnalyzer</c> -- this step never
/// assembles phrases or motif links (Step 055/056's own later scope); it only proposes a
/// <see cref="SectionKind"/> per already-approved segment, with confidence and evidence.
/// </summary>
public interface ISectionLabelClassifier
{
    /// <summary>
    /// Produces a <see cref="SectionLabelAnalysis"/> for <paramref name="context"/> using
    /// <paramref name="settings"/>, with every persisted <see cref="ISectionLabelOverrideStore"/> entry
    /// for this song already applied. Validates every input explicitly before any computation or
    /// persistence occurs, and never persists a malformed automatic result (Step 054's own "Malformed
    /// proposal... must not auto-commit" failure/edge case, verbatim). A <paramref name="context"/> whose
    /// <see cref="SectionLabelContext.BoundaryAnalysis"/> carries no boundaries (e.g. built from silence)
    /// is an explicit, handled case: it returns a <see cref="SectionLabelAnalysis"/> with an empty
    /// <see cref="SectionLabelAnalysis.Labels"/> list rather than throwing.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="SectionLabelInvalidException">
    /// Missing media, a mismatched/stale provenance chain, an unusable <paramref name="settings"/>, or a
    /// computed segment list that fails structural validation.
    /// </exception>
    Task<SectionLabelAnalysis> ClassifyAsync(SectionLabelContext context, SectionLabelSettings settings, CancellationToken ct);

    /// <summary>
    /// Records a human correction of <paramref name="boundaryId"/>'s classification to <paramref name="kind"/>
    /// in the separately versioned <see cref="ISectionLabelOverrideStore"/>, then returns the resulting
    /// merged <see cref="SectionLabelAnalysis"/> (Step 054's own required acceptance, verbatim: "support
    /// overrides"). <paramref name="boundaryId"/> must reference a segment present in the current automatic
    /// classification of <paramref name="context"/> -- addressing a boundary id that does not currently
    /// exist is this step's own "malformed proposal... must not auto-commit" failure/edge case and is
    /// rejected before anything is persisted.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="SectionLabelInvalidException">Inputs are invalid, or <paramref name="boundaryId"/> does not reference a segment in the current classification.</exception>
    Task<SectionLabelAnalysis> SetOverrideAsync(SectionLabelContext context, SectionLabelSettings settings, Guid boundaryId, SectionKind kind, CancellationToken ct);
}
