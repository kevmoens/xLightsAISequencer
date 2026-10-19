using Microsoft.Data.Sqlite;

namespace XlightsAI.Studio.Projects;

/// <summary>
/// Default <see cref="ISqliteMigrationRunner"/>: a single <c>studio.db</c> SQLite file per project
/// directory (PHASE-01 Section 4), versioned with <c>PRAGMA user_version</c>, migrated forward
/// inside one explicit transaction per call with a pre-migration file backup taken before any
/// existing database is mutated (PHASE-01 Section 6, Step 007 implementation guidance).
///
/// <see cref="_migrations"/> is injectable (not just <see cref="DefaultMigrations"/>) purely as a
/// test seam — unit tests use it to register a deliberately-failing migration to prove the
/// interruption/rollback contract without depending on fragile timing or real disk I/O failures.
/// </summary>
public sealed class SqliteMigrationRunner : ISqliteMigrationRunner
{
    public const string DatabaseFileName = "studio.db";

    /// <summary>
    /// The schema version a brand-new or freshly-migrated <c>studio.db</c> should be at. Bump this
    /// and append to <see cref="DefaultMigrations"/> when a later phase needs a new table/column;
    /// never edit a previously-shipped migration's SQL in place.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Ordered, append-only migration history. Version 1 establishes the generic
    /// migration-versioned mutable project store PHASE-01 Section 4 calls for; version 2 adds an
    /// update-tracking column purely to give Step 007's "fresh vs. prior-version" acceptance test a
    /// genuine second forward step to exercise.
    /// </summary>
    public static readonly IReadOnlyList<SqliteMigration> DefaultMigrations =
    [
        new SqliteMigration(
            Version: 1,
            Description: "Create project_state key/value table for migration-versioned mutable project data.",
            UpSql:
            [
                "CREATE TABLE project_state (" +
                "  key TEXT NOT NULL PRIMARY KEY," +
                "  value TEXT NOT NULL" +
                ");",
            ]),
        new SqliteMigration(
            Version: 2,
            Description: "Track last-updated timestamp per project_state entry.",
            UpSql:
            [
                "ALTER TABLE project_state ADD COLUMN updated_utc TEXT NULL;",
            ]),
    ];

    private readonly IReadOnlyList<SqliteMigration> _migrations;

    public SqliteMigrationRunner(IReadOnlyList<SqliteMigration>? migrations = null)
    {
        _migrations = migrations ?? DefaultMigrations;
    }

    public async Task<SqliteMigrationResult> MigrateAsync(string projectDirectory, int targetSchemaVersion, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ct.ThrowIfCancellationRequested();

        var dbPath = Path.Combine(projectDirectory, DatabaseFileName);
        var fileExistedBefore = File.Exists(dbPath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            // Microsoft.Data.Sqlite's default busy timeout is 30s; a locked project database
            // should be reported to the user promptly rather than hanging the UI thread for half a
            // minute before surfacing the failure.
            DefaultTimeout = 2,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);

        int currentVersion;
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            currentVersion = ReadUserVersion(connection);
        }
        catch (SqliteException ex)
        {
            // Failed before any backup or transaction was ever opened: any pre-existing file is
            // untouched by this attempt. But SqliteOpenMode.ReadWriteCreate can create the file as a
            // side effect of OpenAsync itself before failing, so if there was no pre-existing file,
            // delete whatever stray file the failed open may have left behind — a failed open on a
            // brand-new project must leave no database file at all, not a new empty/partial one.
            if (!fileExistedBefore && File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // Best effort: the original SqliteException is still the one that matters.
                }
            }

            throw new ProjectDatabaseMigrationFailedException(0, targetSchemaVersion, dbPath, backupPath: null, ex);
        }

        if (currentVersion == targetSchemaVersion)
        {
            return new SqliteMigrationResult(currentVersion, currentVersion, dbPath, BackupPath: null);
        }

        if (currentVersion > targetSchemaVersion)
        {
            // Required failure case: newer-than-supported ("down mismatch") is rejected without any
            // mutation — no backup is even taken, since nothing is about to be written.
            throw new ProjectDatabaseSchemaNotSupportedException(currentVersion, targetSchemaVersion, dbPath);
        }

        string? backupPath = null;
        if (fileExistedBefore)
        {
            backupPath = CreateBackup(dbPath, currentVersion);
        }

        var pending = _migrations
            .Where(m => m.Version > currentVersion && m.Version <= targetSchemaVersion)
            .OrderBy(m => m.Version)
            .ToList();

        if (pending.Count == 0 || pending[^1].Version != targetSchemaVersion)
        {
            throw new InvalidOperationException(
                $"No registered migration path from schema version {currentVersion} to {targetSchemaVersion} for '{dbPath}'.");
        }

        SqliteTransaction? transaction = null;
        try
        {
            // Microsoft.Data.Sqlite's default BeginTransaction() issues BEGIN IMMEDIATE, acquiring
            // the write lock up front — this is deliberately inside the same try/catch as the
            // migration statements themselves, so a database locked by another writer is rejected
            // and wrapped exactly like a failure partway through the migration itself.
            transaction = connection.BeginTransaction();

            foreach (var migration in pending)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var sql in migration.UpSql)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }

            // PRAGMA user_version writes to the database header and participates in the current
            // transaction exactly like any other page write, so a rollback below also reverts it.
            using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.Transaction = transaction;
                versionCommand.CommandText = $"PRAGMA user_version = {targetSchemaVersion};";
                versionCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            try
            {
                transaction?.Rollback();
            }
            catch (SqliteException)
            {
                // The connection may already be broken by whatever caused the failure (e.g. a
                // locked database mid-write); the original exception is the one that matters.
            }

            throw new ProjectDatabaseMigrationFailedException(currentVersion, targetSchemaVersion, dbPath, backupPath, ex);
        }
        finally
        {
            transaction?.Dispose();
        }

        return new SqliteMigrationResult(currentVersion, targetSchemaVersion, dbPath, backupPath);
    }

    private static int ReadUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string CreateBackup(string dbPath, int fromVersion)
    {
        var backupPath = $"{dbPath}.bak-v{fromVersion}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        File.Copy(dbPath, backupPath, overwrite: false);
        return backupPath;
    }
}
