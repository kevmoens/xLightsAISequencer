namespace XlightsAI.Studio.Reference;

/// <summary>Adapter boundary for reading the Step 002 module audit matrix from disk.</summary>
public interface IModuleAuditMatrixReader
{
    Task<ModuleAuditMatrix> ReadAsync(string path, CancellationToken ct);
}
