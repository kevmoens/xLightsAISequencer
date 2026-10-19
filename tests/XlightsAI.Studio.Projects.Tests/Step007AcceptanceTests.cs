using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Projects.Tests;

/// <summary>
/// PHASE-01 Step 007: "Add SQLite persistence with transactional migrations and backups."
/// Implementation guidance: "Use SQLite PRAGMA user_version or migration table, explicit
/// transaction and a pre-migration file backup. Unit test create, up/down mismatch, interruption
/// and reopen." Required acceptance: "Migration tests on fresh and prior-version databases
/// succeed." Failure/edge case: "Out-of-scope, locked or stale revision must be rejected without
/// partial mutation."
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step007AcceptanceTests : IDisposable
{
    private readonly string _rootDirectory;

    public Step007AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "xlai-step007-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        // SQLite connections are pooling: false and disposed before each test method returns, but
        // give the OS a moment on this environment's filesystem before attempting delete.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private string NewProjectDir(string name)
    {
        var dir = Path.Combine(_rootDirectory, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static int ReadUserVersion(string dbPath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool TableHasColumn(string dbPath, string table, string column)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void InsertProjectStateRow(string dbPath, string key, string value)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO project_state (key, value) VALUES ($key, $value);";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static string? ReadProjectStateValue(string dbPath, string key)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM project_state WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static byte[] HashFile(string path) => SHA256.HashData(File.ReadAllBytes(path));

    [Fact]
    public async Task Step007_Acceptance_Passes()
    {
        // --- Fresh database: migration tests on a brand-new studio.db succeed. ---
        var freshDir = NewProjectDir("Fresh Demo Song");
        var runner = new SqliteMigrationRunner();

        var freshResult = await runner.MigrateAsync(freshDir, SqliteMigrationRunner.CurrentSchemaVersion, CancellationToken.None);

        var freshDbPath = Path.Combine(freshDir, SqliteMigrationRunner.DatabaseFileName);
        Assert.True(File.Exists(freshDbPath));
        Assert.Equal(0, freshResult.PreviousVersion);
        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, freshResult.NewVersion);
        Assert.Null(freshResult.BackupPath);
        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, ReadUserVersion(freshDbPath));
        Assert.True(TableHasColumn(freshDbPath, "project_state", "updated_utc"));

        // --- Prior-version database: migration tests on an existing v1 studio.db succeed. ---
        var priorDir = NewProjectDir("Prior Version Song");
        var v1OnlyRunner = new SqliteMigrationRunner(SqliteMigrationRunner.DefaultMigrations.Where(m => m.Version <= 1).ToList());
        await v1OnlyRunner.MigrateAsync(priorDir, 1, CancellationToken.None);

        var priorDbPath = Path.Combine(priorDir, SqliteMigrationRunner.DatabaseFileName);
        Assert.Equal(1, ReadUserVersion(priorDbPath));
        Assert.False(TableHasColumn(priorDbPath, "project_state", "updated_utc"));

        // An accepted user edit already exists in the v1 database before migrating forward.
        InsertProjectStateRow(priorDbPath, "demo-key", "demo-value");

        var upgradeResult = await runner.MigrateAsync(priorDir, SqliteMigrationRunner.CurrentSchemaVersion, CancellationToken.None);

        Assert.Equal(1, upgradeResult.PreviousVersion);
        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, upgradeResult.NewVersion);
        Assert.NotNull(upgradeResult.BackupPath);
        Assert.True(File.Exists(upgradeResult.BackupPath));
        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, ReadUserVersion(priorDbPath));
        Assert.True(TableHasColumn(priorDbPath, "project_state", "updated_utc"));

        // The migration is additive to existing mutable data, not destructive of it (PHASE-01
        // Section 4: "accepted manual edits are never disposable cache").
        Assert.Equal("demo-value", ReadProjectStateValue(priorDbPath, "demo-key"));
    }

    [Fact]
    public async Task MigrateAsync_WhenAlreadyAtTargetVersion_IsNoOpReopen()
    {
        var dir = NewProjectDir("Reopen Song");
        var runner = new SqliteMigrationRunner();
        await runner.MigrateAsync(dir, SqliteMigrationRunner.CurrentSchemaVersion, CancellationToken.None);
        var dbPath = Path.Combine(dir, SqliteMigrationRunner.DatabaseFileName);
        var hashBeforeReopen = HashFile(dbPath);

        var reopenResult = await runner.MigrateAsync(dir, SqliteMigrationRunner.CurrentSchemaVersion, CancellationToken.None);

        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, reopenResult.PreviousVersion);
        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, reopenResult.NewVersion);
        Assert.Null(reopenResult.BackupPath);
        Assert.Equal(hashBeforeReopen, HashFile(dbPath));
        Assert.Empty(Directory.GetFiles(dir, "*.bak-*"));
    }

    [Fact]
    public async Task MigrateAsync_WhenCurrentVersionNewerThanTarget_RejectsWithoutMutating_NegativePath()
    {
        var dir = NewProjectDir("Newer Than Code Song");
        var runner = new SqliteMigrationRunner();
        await runner.MigrateAsync(dir, SqliteMigrationRunner.CurrentSchemaVersion, CancellationToken.None);
        var dbPath = Path.Combine(dir, SqliteMigrationRunner.DatabaseFileName);
        var hashBefore = HashFile(dbPath);

        var ex = await Assert.ThrowsAsync<ProjectDatabaseSchemaNotSupportedException>(
            () => runner.MigrateAsync(dir, SqliteMigrationRunner.CurrentSchemaVersion - 1, CancellationToken.None));

        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, ex.FoundVersion);
        Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion - 1, ex.SupportedVersion);
        Assert.Equal(hashBefore, HashFile(dbPath));
        Assert.Empty(Directory.GetFiles(dir, "*.bak-*"));
    }

    [Fact]
    public async Task MigrateAsync_WhenMigrationStepFails_RollsBackAndPreservesBackup_NegativePath()
    {
        var dir = NewProjectDir("Interrupted Migration Song");
        var v1OnlyRunner = new SqliteMigrationRunner(SqliteMigrationRunner.DefaultMigrations.Where(m => m.Version <= 1).ToList());
        await v1OnlyRunner.MigrateAsync(dir, 1, CancellationToken.None);
        var dbPath = Path.Combine(dir, SqliteMigrationRunner.DatabaseFileName);
        var hashBeforeAttempt = HashFile(dbPath);

        // A deliberately broken v2 migration: the first statement succeeds, the second is invalid
        // SQL, forcing a failure partway through the migration transaction.
        var brokenMigrations = new List<SqliteMigration>
        {
            SqliteMigrationRunner.DefaultMigrations.Single(m => m.Version == 1),
            new SqliteMigration(
                Version: 2,
                Description: "Deliberately broken migration for interruption testing.",
                UpSql:
                [
                    "ALTER TABLE project_state ADD COLUMN updated_utc TEXT NULL;",
                    "THIS IS NOT VALID SQL;",
                ]),
        };
        var brokenRunner = new SqliteMigrationRunner(brokenMigrations);

        var ex = await Assert.ThrowsAsync<ProjectDatabaseMigrationFailedException>(
            () => brokenRunner.MigrateAsync(dir, 2, CancellationToken.None));

        Assert.Equal(1, ex.FromVersion);
        Assert.Equal(2, ex.TargetVersion);
        Assert.NotNull(ex.BackupPath);
        Assert.True(File.Exists(ex.BackupPath));
        Assert.Equal(hashBeforeAttempt, HashFile(ex.BackupPath!));

        // The live database is left exactly as found: still version 1, no half-applied column.
        Assert.Equal(1, ReadUserVersion(dbPath));
        Assert.False(TableHasColumn(dbPath, "project_state", "updated_utc"));
        Assert.Equal(hashBeforeAttempt, HashFile(dbPath));
    }

    [Fact]
    public async Task MigrateAsync_WhenDatabaseIsLockedByAnotherWriter_RejectsWithoutPartialMutation_NegativePath()
    {
        var dir = NewProjectDir("Locked Song");
        var runner = new SqliteMigrationRunner(SqliteMigrationRunner.DefaultMigrations.Where(m => m.Version <= 1).ToList());
        await runner.MigrateAsync(dir, 1, CancellationToken.None);
        var dbPath = Path.Combine(dir, SqliteMigrationRunner.DatabaseFileName);
        var hashBeforeAttempt = HashFile(dbPath);

        var lockingConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false,
        }.ToString());
        lockingConnection.Open();
        using (var lockCommand = lockingConnection.CreateCommand())
        {
            lockCommand.CommandText = "BEGIN IMMEDIATE TRANSACTION;";
            lockCommand.ExecuteNonQuery();
        }

        try
        {
            var fullRunner = new SqliteMigrationRunner();

            var ex = await Assert.ThrowsAsync<ProjectDatabaseMigrationFailedException>(
                () => fullRunner.MigrateAsync(dir, SqliteMigrationRunner.CurrentSchemaVersion, CancellationToken.None));

            Assert.Equal(1, ex.FromVersion);
            Assert.Equal(SqliteMigrationRunner.CurrentSchemaVersion, ex.TargetVersion);
        }
        finally
        {
            using (var rollback = lockingConnection.CreateCommand())
            {
                rollback.CommandText = "ROLLBACK;";
                rollback.ExecuteNonQuery();
            }

            // Fully release the OS-level file handle (not just the SQLite-level lock) before
            // re-reading the file directly below — a live connection can keep the file exclusively
            // handled on Windows even after its transaction is rolled back.
            lockingConnection.Dispose();
            SqliteConnection.ClearAllPools();
        }

        // Database is unchanged: still version 1, byte-identical to before the rejected attempt.
        Assert.Equal(1, ReadUserVersion(dbPath));
        Assert.Equal(hashBeforeAttempt, HashFile(dbPath));
    }

    [Fact]
    public async Task MigrateAsync_WithAlreadyCancelledToken_ThrowsAndWritesNothing_NegativePath()
    {
        var dir = NewProjectDir("Cancelled Song");
        var runner = new SqliteMigrationRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.MigrateAsync(dir, SqliteMigrationRunner.CurrentSchemaVersion, cts.Token));

        Assert.False(File.Exists(Path.Combine(dir, SqliteMigrationRunner.DatabaseFileName)));
    }

    [Fact]
    public async Task MigrateAsync_WithInvalidDirectory_Throws_NegativePath()
    {
        var runner = new SqliteMigrationRunner();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => runner.MigrateAsync(null!, SqliteMigrationRunner.CurrentSchemaVersion, CancellationToken.None));
    }

    [Fact]
    public async Task ProjectService_CreateAsync_CreatesStudioDbAtCurrentSchema()
    {
        var appDir = Path.Combine(_rootDirectory, "app-" + Guid.NewGuid().ToString("N"));
        var service = new ProjectService(new JsonProjectStore(), new RecentProjectsStore(appDir));
        var projectDir = Path.Combine(_rootDirectory, "Demo Song Db");

        await service.CreateAsync(projectDir, "Demo Song Db", CancellationToken.None);

        var dbPath = Path.Combine(projectDir, SqliteMigrationRunner.DatabaseFileName);
        Assert.True(File.Exists(dbPath));
        Assert.Equal(JsonProjectStore.CurrentDatabaseSchemaVersion, ReadUserVersion(dbPath));
    }

    [Fact]
    public async Task ProjectService_OpenAsync_OnPriorVersionProject_MigratesDatabaseTransactionallyWithBackup()
    {
        var appDir = Path.Combine(_rootDirectory, "app-" + Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(_rootDirectory, "Prior Version Project");
        Directory.CreateDirectory(projectDir);

        // Simulate a project created by an older build: manifest at the current schema, but
        // studio.db only migrated to schema version 1.
        var manifest = new ProjectManifest(
            new ProjectId(Guid.NewGuid()),
            JsonProjectStore.CurrentSchemaVersion,
            "Prior Version Project",
            projectDir,
            DateTimeOffset.UtcNow);
        await new JsonProjectStore().WriteManifestAtomicallyAsync(manifest, CancellationToken.None);
        var v1OnlyRunner = new SqliteMigrationRunner(SqliteMigrationRunner.DefaultMigrations.Where(m => m.Version <= 1).ToList());
        await v1OnlyRunner.MigrateAsync(projectDir, 1, CancellationToken.None);
        var dbPath = Path.Combine(projectDir, SqliteMigrationRunner.DatabaseFileName);
        Assert.Equal(1, ReadUserVersion(dbPath));

        var service = new ProjectService(new JsonProjectStore(), new RecentProjectsStore(appDir));

        var opened = await service.OpenAsync(projectDir, CancellationToken.None);

        Assert.Equal(manifest.Id, opened.Id);
        Assert.Equal(JsonProjectStore.CurrentDatabaseSchemaVersion, ReadUserVersion(dbPath));
        Assert.NotEmpty(Directory.GetFiles(projectDir, "*.bak-*"));
    }
}
