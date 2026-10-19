namespace XlightsAI.Studio.Reference;

/// <summary>Porting disposition for an upstream source boundary, in the spec's own vocabulary.</summary>
public enum ModuleDecision
{
    Reuse,
    Port,
    Replace,
    Omit,
}

public enum ModuleRiskLevel
{
    Low,
    Medium,
    High,
}

/// <summary>One row of the Step 002 module-by-module reuse/port/replace/omit audit.</summary>
public sealed record ModuleAuditEntry(
    string ModulePath,
    string Description,
    ModuleDecision Decision,
    ModuleRiskLevel RiskLevel,
    string Rationale,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> NativeDependencies,
    bool SourceOverwriteRisk,
    string? OverwriteMitigation,
    string? MixedStrategyNote);

/// <summary>
/// Deserialized form of <c>reference/MODULE-AUDIT-MATRIX.json</c>: the Step 002 deliverable
/// ("a module-by-module reuse/port/replace/omit matrix with implementation risks"), tied back
/// to the exact pinned commit audited so the matrix cannot silently drift from what Step 001
/// actually pinned.
/// </summary>
public sealed record ModuleAuditMatrix(
    int SchemaVersion,
    string SourceCommitSha,
    DateTimeOffset AuditedUtc,
    IReadOnlyList<ModuleAuditEntry> Modules);
