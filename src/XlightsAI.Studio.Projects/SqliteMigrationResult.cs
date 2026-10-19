namespace XlightsAI.Studio.Projects;

/// <summary>
/// Outcome of a successful <see cref="ISqliteMigrationRunner.MigrateAsync"/> call.
/// <see cref="BackupPath"/> is <see langword="null"/> when no pre-migration backup was needed
/// (brand-new database, or the database was already at <see cref="NewVersion"/>).
/// </summary>
public sealed record SqliteMigrationResult(
    int PreviousVersion,
    int NewVersion,
    string DatabasePath,
    string? BackupPath);
