using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition;

/// <summary>PHASE-10 Step 068: one <see cref="VisualBudgetCandidate"/>'s own resolved admission outcome.</summary>
/// <param name="Candidate">The original candidate this result was computed for.</param>
/// <param name="Decision">Whether it was admitted unmodified, trimmed, or dropped entirely.</param>
/// <param name="AdmittedWindow">The actual window admitted (equal to <see cref="VisualBudgetCandidate.Window"/> when <see cref="Decision"/> is <see cref="VisualBudgetDecision.Accepted"/>, a sub-interval when <see cref="VisualBudgetDecision.Trimmed"/>, or <see langword="null"/> when <see cref="VisualBudgetDecision.Dropped"/>).</param>
/// <param name="Reason">A human-readable explanation, populated exactly when <see cref="Decision"/> is not <see cref="VisualBudgetDecision.Accepted"/>.</param>
public sealed record VisualBudgetCandidateResult(
    VisualBudgetCandidate Candidate,
    VisualBudgetDecision Decision,
    TimeWindow? AdmittedWindow,
    string? Reason);
