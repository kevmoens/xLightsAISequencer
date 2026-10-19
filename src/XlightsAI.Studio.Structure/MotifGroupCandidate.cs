namespace XlightsAI.Studio.Structure;

/// <summary>
/// One cluster of two or more <see cref="PhraseCandidate"/>s that this step's own recipe
/// (<see cref="MotifClusterer"/>) judged to share the same underlying motif -- the connected-components
/// closure of every pairwise <see cref="MotifLinkCandidate"/> at or above
/// <see cref="MotifClusteringSettings.SimilarityThreshold"/>. A single, unrepeated phrase never produces a
/// group of its own ("repeated eight-bar fixture shares motif ID; distinct passage does not" -- this step's
/// own required acceptance, verbatim: a distinct passage has no motif group at all, not a group of one).
/// </summary>
/// <param name="Id">Stable identity for this motif group -- the "motif ID" every member <see cref="PhraseIds"/> entry shares.</param>
/// <param name="PhraseIds">Every <see cref="PhraseCandidate.Id"/> belonging to this motif, ordered by that phrase's own <see cref="PhraseCandidate.StartBarIndex"/>. Always two or more entries.</param>
/// <param name="RepresentativePhraseId">The earliest member (lowest <see cref="PhraseCandidate.StartBarIndex"/>) of <see cref="PhraseIds"/> -- a deterministic, inspectable default "first approved performance" (Section 12), not a claim of final choreography-template authority (Step 057's own later scope: "a user may reassign the canonical source").</param>
/// <param name="Confidence">The minimum pairwise <see cref="MotifLinkCandidate.Similarity"/> among every pair of members in this group -- a conservative, never-overstated group confidence (a group is only as strong as its weakest internal link).</param>
/// <param name="Evidence">Human-readable, non-empty, non-throwing reasons this cluster was formed.</param>
public sealed record MotifGroupCandidate(
    Guid Id,
    IReadOnlyList<Guid> PhraseIds,
    Guid RepresentativePhraseId,
    double Confidence,
    IReadOnlyList<string> Evidence);
