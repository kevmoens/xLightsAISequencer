namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown when a single <see cref="ModelFeasibilityCandidate"/> is structurally invalid or internally
/// contradictory (e.g. marked <see cref="ModelFeasibilityVerdict.Feasible"/> while not actually proven
/// executable or not actually redistributable) — a caller programming error, distinct from the
/// report-level "fewer than two feasible alternatives and no blocker" acceptance failure, which is
/// <see cref="ModelFeasibilityReportInvalidException"/> instead.
/// </summary>
public sealed class ModelFeasibilityCandidateInvalidException : Exception
{
    public ModelFeasibilityCandidateInvalidException(string message) : base(message)
    {
    }
}
