namespace XlightsAI.Studio.Stems;

/// <summary>
/// Validates and assembles a <see cref="ModelFeasibilityReport"/> from inspected candidates, enforcing
/// Step 038's own required acceptance before a report can exist.
/// </summary>
public interface IModelFeasibilityEvaluator
{
    /// <summary>
    /// Builds a report. Throws <see cref="ModelFeasibilityCandidateInvalidException"/> for a
    /// structurally invalid or internally contradictory candidate, and
    /// <see cref="ModelFeasibilityReportInvalidException"/> when fewer than two candidates are
    /// <see cref="ModelFeasibilityVerdict.Feasible"/> and <paramref name="blockerReason"/> is null or
    /// whitespace — the report-level realization of "at least two feasible alternatives or explicit
    /// blocker" (required acceptance, verbatim).
    /// </summary>
    ModelFeasibilityReport Evaluate(
        Guid id,
        long createdAtTicks,
        IReadOnlyList<ModelFeasibilityCandidate> candidates,
        string decision,
        string? blockerReason);
}
