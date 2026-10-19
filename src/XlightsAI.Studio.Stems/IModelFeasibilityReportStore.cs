namespace XlightsAI.Studio.Stems;

/// <summary>
/// Persists/reloads a <see cref="ModelFeasibilityReport"/> as a versioned, inspectable project output
/// (Step 038's own required user-visible outcome, verbatim: "Versioned, inspectable project output plus
/// a reproducible test report").
/// </summary>
public interface IModelFeasibilityReportStore
{
    string GetPath(Guid reportId);

    Task<string> SaveAsync(ModelFeasibilityReport report, CancellationToken ct);

    Task<ModelFeasibilityReport> LoadAsync(string path, CancellationToken ct);
}
