namespace XlightsAI.Studio.Reference;

/// <summary>Outcome of a module-audit run: the parsed matrix (if any) plus validation errors.</summary>
public sealed record ModuleAuditReport(
    ModuleAuditMatrix? Matrix,
    ModuleAuditValidationResult Validation)
{
    public bool IsAccepted => Validation.IsValid;

    /// <summary>Human-readable summary: the "public output" an architecture/diagnostics screen can display verbatim.</summary>
    public string ToDisplayText()
    {
        if (!Validation.IsValid || Matrix is null)
        {
            return "Module audit matrix is INVALID:" + Environment.NewLine
                + string.Join(Environment.NewLine, Validation.Errors.Select(e => $"  - {e}"));
        }

        var byDecision = Matrix.Modules
            .GroupBy(m => m.Decision)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}");

        var overwriteRisks = Matrix.Modules.Where(m => m.SourceOverwriteRisk).ToList();

        var lines = new List<string>
        {
            $"Module audit for commit {Matrix.SourceCommitSha} ({Matrix.Modules.Count} modules):",
            "  " + string.Join(", ", byDecision),
        };

        if (overwriteRisks.Count > 0)
        {
            lines.Add("  Source-overwrite risks found and mitigated:");
            lines.AddRange(overwriteRisks.Select(m => $"    - {m.ModulePath}: {m.OverwriteMitigation}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Application-layer orchestration for Step 002: reads the module audit matrix and validates it
/// both against this Studio's own rules and against Step 001's immutable pinned-commit contract.
/// Owns no UI and no persistence writes.
/// </summary>
public sealed class ModuleAuditService(IModuleAuditMatrixReader reader)
{
    public async Task<ModuleAuditReport> AuditAsync(string matrixPath, string expectedPinnedCommitSha, CancellationToken ct)
    {
        ModuleAuditMatrix matrix;
        try
        {
            matrix = await reader.ReadAsync(matrixPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or ModuleAuditMatrixFormatException)
        {
            return new ModuleAuditReport(
                Matrix: null,
                Validation: ModuleAuditValidationResult.Failure([ex.Message]));
        }

        var validation = ModuleAuditValidator.Validate(matrix, expectedPinnedCommitSha);
        return new ModuleAuditReport(matrix, validation);
    }
}
