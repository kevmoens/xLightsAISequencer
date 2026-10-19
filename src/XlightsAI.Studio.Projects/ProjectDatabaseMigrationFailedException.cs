namespace XlightsAI.Studio.Projects;

/// <summary>
/// Thrown when a <c>studio.db</c> migration is interrupted, blocked by a concurrent writer (a
/// locked database), or otherwise fails after the migration transaction was opened. The underlying
/// transaction is always rolled back before this is thrown, so the database file is left at exactly
/// <see cref="FromVersion"/> — never partially mutated. When <see cref="BackupPath"/> is non-null, a
/// pre-migration backup copy exists on disk at that path and was itself never touched by the failed
/// attempt (PHASE-01 Section 6, Step 007 failure case: "locked ... revision must be rejected without
/// partial mutation").
/// </summary>
public sealed class ProjectDatabaseMigrationFailedException : Exception
{
    public int FromVersion { get; }

    public int TargetVersion { get; }

    public string DatabasePath { get; }

    public string? BackupPath { get; }

    public ProjectDatabaseMigrationFailedException(
        int fromVersion,
        int targetVersion,
        string databasePath,
        string? backupPath,
        Exception inner)
        : base(
            $"Migrating project database '{databasePath}' from version {fromVersion} to {targetVersion} " +
            $"failed and was rolled back: {inner.Message}",
            inner)
    {
        FromVersion = fromVersion;
        TargetVersion = targetVersion;
        DatabasePath = databasePath;
        BackupPath = backupPath;
    }
}
