namespace XlightsAI.Studio.Pipeline;

/// <summary>PHASE-2b (build plan §2b.10-11): the persisted result of one induction+verification run — every promoted <see cref="CraftRule"/>, plus the full list of candidates considered (including ones that did not clear the promotion threshold) so the process is auditable, never a silent "trust me."</summary>
public sealed record CraftRules(
    DateTimeOffset GeneratedAtUtc,
    double MinConfidenceThreshold,
    int MinSongCountThreshold,
    IReadOnlyList<CraftRule> Promoted,
    IReadOnlyList<CraftRule> RejectedCandidates);
