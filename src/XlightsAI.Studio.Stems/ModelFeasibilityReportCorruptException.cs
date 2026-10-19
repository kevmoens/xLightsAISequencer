namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="IModelFeasibilityReportStore"/> when a persisted feasibility report file exists
/// but cannot be parsed back into a valid <see cref="ModelFeasibilityReport"/> — mirrors
/// <c>BeatGridCorruptException</c>'s own precedent in <c>XlightsAI.Studio.Rhythm</c> for a cache file
/// that is present but unreadable.
/// </summary>
public sealed class ModelFeasibilityReportCorruptException : Exception
{
    public ModelFeasibilityReportCorruptException(string path, Exception inner)
        : base($"Model feasibility report at '{path}' exists but could not be parsed.", inner)
    {
    }
}
