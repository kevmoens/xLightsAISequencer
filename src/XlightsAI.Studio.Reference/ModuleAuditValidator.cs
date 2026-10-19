namespace XlightsAI.Studio.Reference;

/// <summary>Outcome of validating a <see cref="ModuleAuditMatrix"/>. Never throws for bad data.</summary>
public sealed record ModuleAuditValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ModuleAuditValidationResult Success { get; } = new(true, []);

    public static ModuleAuditValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Pure domain rules for the Step 002 module audit matrix. Two rules exist specifically because
/// of what the audit itself found in the pinned upstream source:
/// <list type="bullet">
/// <item>Step 001's pinned commit is an immutable prior-step contract: a module audit for a
/// different commit is stale and must be rejected, not silently accepted.</item>
/// <item><c>src/grouper/writer.py</c> overwrites the user's real <c>xlights_rgbeffects.xml</c> in
/// place by default. Any entry that flags this kind of source-overwrite risk must record how the
/// .NET port avoids it — an unexamined overwrite risk is exactly the failure mode Step 002's
/// "must fail safely without rewriting source files" edge case exists to catch before any code
/// is written against that module.</item>
/// </list>
/// </summary>
public static class ModuleAuditValidator
{
    private const int SupportedSchemaVersion = 1;

    public static ModuleAuditValidationResult Validate(ModuleAuditMatrix? matrix, string expectedPinnedCommitSha)
    {
        if (matrix is null)
        {
            return ModuleAuditValidationResult.Failure(["Matrix is null."]);
        }

        var errors = new List<string>();

        if (matrix.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add($"Unsupported SchemaVersion {matrix.SchemaVersion}; this Studio supports version {SupportedSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(expectedPinnedCommitSha))
        {
            errors.Add("expectedPinnedCommitSha must be supplied by the caller from Step 001's manifest.");
        }
        else if (!string.Equals(matrix.SourceCommitSha, expectedPinnedCommitSha, StringComparison.Ordinal))
        {
            errors.Add($"Matrix.SourceCommitSha '{matrix.SourceCommitSha}' does not match the Step 001 pinned commit " +
                       $"'{expectedPinnedCommitSha}'. The audit is stale relative to the immutable prior-step contract.");
        }

        if (matrix.Modules.Count == 0)
        {
            errors.Add("Modules must list at least one audited source boundary.");
        }

        foreach (var module in matrix.Modules)
        {
            var label = string.IsNullOrWhiteSpace(module.ModulePath) ? "<missing path>" : module.ModulePath;

            if (string.IsNullOrWhiteSpace(module.ModulePath))
            {
                errors.Add("Every module entry must have a non-empty ModulePath.");
            }

            if (string.IsNullOrWhiteSpace(module.Rationale))
            {
                errors.Add($"[{label}] Rationale must be recorded for every decision.");
            }

            if (module.RiskLevel == ModuleRiskLevel.High && module.Risks.Count == 0)
            {
                errors.Add($"[{label}] RiskLevel is High but no concrete Risks are recorded.");
            }

            if (module.SourceOverwriteRisk && string.IsNullOrWhiteSpace(module.OverwriteMitigation))
            {
                errors.Add($"[{label}] SourceOverwriteRisk is flagged but no OverwriteMitigation is recorded — " +
                           "an identified source-overwrite hazard must not pass through unexamined.");
            }
        }

        return errors.Count == 0
            ? ModuleAuditValidationResult.Success
            : ModuleAuditValidationResult.Failure(errors);
    }
}
