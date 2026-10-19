namespace XlightsAI.Studio.Composition;

/// <summary>One <see cref="ChoreographyCandidate"/>'s own resolved layer/priority outcome. See <see cref="ChoreographyResolutionDecision"/>.</summary>
/// <param name="Candidate">The candidate this result describes.</param>
/// <param name="Decision">The resolution outcome.</param>
/// <param name="ResolvedLayer">The layer the resulting effect was actually compiled onto; <see langword="null"/> when <see cref="Decision"/> is <see cref="ChoreographyResolutionDecision.Dropped"/>.</param>
/// <param name="Reason">A human-readable explanation of why this decision was made -- always populated for <see cref="ChoreographyResolutionDecision.Reassigned"/>/<see cref="ChoreographyResolutionDecision.Dropped"/>, <see langword="null"/> for a plain <see cref="ChoreographyResolutionDecision.Accepted"/> with nothing to explain.</param>
public sealed record ChoreographyCandidateResult(
    ChoreographyCandidate Candidate,
    ChoreographyResolutionDecision Decision,
    int? ResolvedLayer,
    string? Reason);
