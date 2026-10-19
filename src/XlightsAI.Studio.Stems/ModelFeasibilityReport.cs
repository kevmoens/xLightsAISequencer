namespace XlightsAI.Studio.Stems;

/// <summary>
/// The persisted, versioned decision document Step 038 requires: every inspected candidate plus the
/// final decision and (when fewer than two candidates are proven feasible) the explicit blocker reason.
/// Only ever constructed by <see cref="ModelFeasibilityEvaluator.Evaluate"/>, which enforces the
/// acceptance rule below before this record can exist — never hand-built by a caller to bypass it.
/// </summary>
/// <param name="Id">Stable identity for this evaluation run, for cache/versioning per Section 4.</param>
/// <param name="CreatedAtTicks">Caller-supplied wall-clock ticks (<see cref="DateTime.UtcNow"/>-style,
/// <c>Ticks</c>) this report was produced — supplied explicitly rather than sampled internally, so the
/// evaluator stays a pure, deterministic, directly-testable function.</param>
/// <param name="Candidates">Every inspected candidate, feasible or not — a rejected candidate is kept,
/// not deleted, so the decision remains auditable.</param>
/// <param name="Decision">Free-text summary of the actual decision reached.</param>
/// <param name="IsBlocked">True when fewer than two candidates carry
/// <see cref="ModelFeasibilityVerdict.Feasible"/>, making <see cref="BlockerReason"/> load-bearing.</param>
/// <param name="BlockerReason">Non-null only when <see cref="IsBlocked"/> is true.</param>
public sealed record ModelFeasibilityReport(
    Guid Id,
    long CreatedAtTicks,
    IReadOnlyList<ModelFeasibilityCandidate> Candidates,
    string Decision,
    bool IsBlocked,
    string? BlockerReason)
{
    /// <summary>Candidates that independently proved both executable under .NET and redistributable.</summary>
    public int FeasibleCandidateCount => Candidates.Count(c => c.Verdict == ModelFeasibilityVerdict.Feasible);
}
