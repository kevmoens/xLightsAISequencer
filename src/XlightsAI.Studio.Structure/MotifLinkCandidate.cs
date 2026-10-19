namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 056's own title, verbatim: "Cluster exact and similar motifs with tolerance for
/// variation." A phrase-local, concrete narrowing of PHASE-08 Section 3's proposed
/// <c>MotifLink(Guid SourcePhraseId, Guid TargetPhraseId, double Similarity, bool UserApproved, string
/// InheritanceMask)</c> -- same identity/score fields, but this step's own narrower scope never proposes
/// <c>UserApproved</c>/<c>InheritanceMask</c>: approval and choreography inheritance belong to Steps
/// 057/058's own later scope (parameterized templates, propagation inspector). This record is a pure,
/// acoustic-similarity-only pairwise proposal between two already-segmented <see cref="PhraseCandidate"/>s.
/// </summary>
/// <param name="SourcePhraseId">The earlier (lower <see cref="PhraseCandidate.StartBarIndex"/>) of the two linked phrases -- a stable, arbitrary-but-deterministic orientation, not an assertion that this phrase is the "canonical" motif source (Section 3: "The first approved performance of a motif may be parent but a user may reassign the canonical source" -- that reassignment is Step 057/058's own later scope).</param>
/// <param name="TargetPhraseId">The later of the two linked phrases.</param>
/// <param name="Similarity">Cosine similarity in <c>[-1, 1]</c> between the two phrases' own time-scale-normalized embeddings (<see cref="MotifClusterer"/>) -- "score separately from section label" (this step's own implementation guidance, verbatim): computed purely from acoustic/melodic features, never from <see cref="SectionLabelCandidate.Kind"/>.</param>
/// <param name="Evidence">Human-readable, non-empty, non-throwing reasons this specific similarity score was computed.</param>
public sealed record MotifLinkCandidate(
    Guid SourcePhraseId,
    Guid TargetPhraseId,
    double Similarity,
    IReadOnlyList<string> Evidence);
