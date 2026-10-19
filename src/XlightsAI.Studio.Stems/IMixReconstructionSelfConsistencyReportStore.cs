namespace XlightsAI.Studio.Stems;

/// <summary>
/// Persists/reloads a <see cref="MixReconstructionSelfConsistencyReport"/> as a versioned, inspectable
/// project output -- this step's own required user-visible outcome, verbatim: "Saved audio/analysis
/// artifact with timestamp provenance."
/// </summary>
public interface IMixReconstructionSelfConsistencyReportStore
{
    string GetPath(Guid reportId);

    Task<string> SaveAsync(MixReconstructionSelfConsistencyReport report, CancellationToken ct);

    Task<MixReconstructionSelfConsistencyReport> LoadAsync(string path, CancellationToken ct);
}
