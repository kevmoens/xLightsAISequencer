namespace XlightsAI.Studio.Projects;

/// <summary>
/// One forward schema step for <see cref="SqliteMigrationRunner"/>: the schema version this
/// migration produces once applied, a human-readable description for diagnostics/evidence, and the
/// ordered DDL/DML statements executed inside the single migration transaction. There is
/// intentionally no "down" script — PHASE-01 Section 6, Step 007 only requires detecting and
/// rejecting a downgrade (a newer-than-supported on-disk version), never performing one.
/// </summary>
public sealed record SqliteMigration(int Version, string Description, IReadOnlyList<string> UpSql);
