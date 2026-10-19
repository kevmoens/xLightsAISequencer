namespace XlightsAI.Studio.Stems;

/// <summary>
/// Persists/reloads a <see cref="SeparationQualityReport"/> as a versioned, inspectable project output
/// (Step 044's own required user-visible outcome, verbatim: "Saved audio/analysis artifact with
/// timestamp provenance").
/// </summary>
public interface ISeparationQualityReportStore
{
    string GetPath(Guid reportId);

    Task<string> SaveAsync(SeparationQualityReport report, CancellationToken ct);

    Task<SeparationQualityReport> LoadAsync(string path, CancellationToken ct);
}
