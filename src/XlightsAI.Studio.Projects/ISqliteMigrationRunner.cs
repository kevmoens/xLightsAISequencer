namespace XlightsAI.Studio.Projects;

/// <summary>
/// Transactional, backed-up schema migration for a single project's <c>studio.db</c>
/// (PHASE-01 Section 6, Step 007). Implementations must never leave the database partially
/// mutated: every migration either fully commits at the requested target schema version, or the
/// database is left exactly as it was found.
/// </summary>
public interface ISqliteMigrationRunner
{
    /// <summary>
    /// Brings the <c>studio.db</c> file under <paramref name="projectDirectory"/> to
    /// <paramref name="targetSchemaVersion"/>, creating it if it does not yet exist. A pre-migration
    /// backup is written before any existing database is mutated. Throws
    /// <see cref="ProjectDatabaseSchemaNotSupportedException"/> without mutating the file if the
    /// on-disk version is newer than <paramref name="targetSchemaVersion"/>, and
    /// <see cref="ProjectDatabaseMigrationFailedException"/> if migration is interrupted, locked by
    /// another writer, or otherwise fails after a backup (if any) was taken.
    /// </summary>
    Task<SqliteMigrationResult> MigrateAsync(string projectDirectory, int targetSchemaVersion, CancellationToken ct);
}
