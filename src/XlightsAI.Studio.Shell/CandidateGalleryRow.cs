using XlightsAI.Studio.Refinement;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-12 Step 084 ("Add candidate gallery, score explanations, preview and explicit selection") --
/// one bindable row of <see cref="CandidateCompareViewModel.Candidates"/>: a single
/// <see cref="CandidateAttempt"/> from a <see cref="LoopRun"/>, paired with the
/// <see cref="QualityReport"/> this step's own implementation guidance names, verbatim: "scores and
/// synchronized preview." Contains no Avalonia types, the same convention every other screen view
/// model in this project already follows.
/// </summary>
/// <param name="CandidateId">The underlying <see cref="CandidateAttempt.Id"/>.</param>
/// <param name="Attempt">The underlying <see cref="CandidateAttempt.Attempt"/> ordinal -- a stable, human-readable row label.</param>
/// <param name="Status">The underlying <see cref="CandidateAttempt.Status"/> at the moment it was last saved.</param>
/// <param name="AggregateScore">
/// <see cref="BestCandidateSelector.AggregateScore"/> computed from <see cref="Report"/> -- a single
/// weighted-average score in <c>[0, 1]</c> a gallery row can sort/display by, without the caller
/// re-deriving it from <see cref="Report"/>'s own <see cref="QualityReport.Metrics"/> itself.
/// </param>
/// <param name="HasViolations">
/// <see langword="true"/> when either the underlying <see cref="CandidateAttempt.Violations"/> or
/// <see cref="Report"/>'s own <see cref="QualityReport.Violations"/> is non-empty -- this row can never
/// be accepted (<see cref="IRefinementCandidateCommitService.AcceptAsync"/> rejects it) when this is
/// <see langword="true"/>, the same "must not auto-commit" rule Steps 079-083 already established.
/// </param>
/// <param name="Report">The full, already-computed <see cref="QualityReport"/> -- "score explanations" (this step's own heading, verbatim) are <see cref="MetricValue.Explanation"/> on each of its own <see cref="QualityReport.Metrics"/> entries, never re-summarized or lost here.</param>
/// <param name="PreviewSummary">A human-readable "which gestures, on which props, over which window" summary of this candidate's own gestures -- this step's own implementation guidance, verbatim: "synchronized preview." Never a rendered image (no real xLights/Avalonia preview surface exists yet for this phase); a textual, inspectable stand-in, the same convention <see cref="AiEditProposalReviewViewModel.DiffSummary"/> already established for PHASE-11's sibling review screen.</param>
/// <param name="IsBest">
/// <see langword="true"/> when this row's own <see cref="CandidateId"/> equals the owning
/// <see cref="LoopRun.BestCandidateId"/> -- the gallery's own "best-vs-current" comparison anchor
/// (this step's own implementation guidance, verbatim: "Compare best-vs-current with scores").
/// </param>
public sealed record CandidateGalleryRow(
    Guid CandidateId,
    int Attempt,
    RefinementStatus Status,
    double AggregateScore,
    bool HasViolations,
    QualityReport Report,
    string PreviewSummary,
    bool IsBest);
