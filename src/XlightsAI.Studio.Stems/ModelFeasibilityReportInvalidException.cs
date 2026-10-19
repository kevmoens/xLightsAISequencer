namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown when a <see cref="ModelFeasibilityReport"/> would not satisfy Step 038's own required
/// acceptance, verbatim: "Decision document includes at least two feasible alternatives or explicit
/// blocker." Raised by <see cref="ModelFeasibilityEvaluator.Evaluate"/> before a report is ever
/// constructed or persisted — an invalid report is never silently accepted or auto-corrected.
/// </summary>
public sealed class ModelFeasibilityReportInvalidException : Exception
{
    public ModelFeasibilityReportInvalidException(string message) : base(message)
    {
    }
}
