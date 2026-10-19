namespace XlightsAI.Studio.Stems;

/// <summary>Thrown when a persisted <see cref="SeparationQualityReport"/> JSON file cannot be deserialized -- the same "stale/corrupt cache is never silently accepted" precedent <see cref="ModelFeasibilityReportCorruptException"/> already established.</summary>
public sealed class SeparationQualityReportCorruptException : Exception
{
    public SeparationQualityReportCorruptException(string path, Exception innerException)
        : base($"Separation quality report at '{path}' is corrupt and could not be loaded.", innerException)
    {
    }
}
