using System.Text.Json;

namespace XlightsAI.Studio.Projects;

/// <summary>
/// JSON-file-backed <see cref="IProjectStore"/>: <c>project.json</c> in the project's root
/// directory, written atomically (same-directory temp file + <see cref="File.Move(string, string, bool)"/>
/// replace) exactly like <c>SettingsStore</c> from Step 005 — the same pattern, a different
/// contract, per the architecture's manifest-write guidance (PHASE-01 Section 4).
/// <see cref="MigrateDatabaseAsync"/> delegates to a real <see cref="ISqliteMigrationRunner"/>
/// (Step 007) against the project's own <c>studio.db</c>; Step 006 shipped this as a disclosed
/// <see cref="NotSupportedException"/> stub, superseded here per that step's own evidence (§9/§10:
/// "real SQLite migration is Step 007's scope").
/// </summary>
public sealed class JsonProjectStore : IProjectStore
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The <c>studio.db</c> schema version this build creates/migrates projects to. Kept as its own
    /// constant (distinct from <see cref="CurrentSchemaVersion"/>, which versions <c>project.json</c>)
    /// because the manifest and the SQLite database are independently versioned artifacts per
    /// PHASE-01 Section 4.
    /// </summary>
    public const int CurrentDatabaseSchemaVersion = SqliteMigrationRunner.CurrentSchemaVersion;

    public const string ManifestFileName = "project.json";

    private readonly ISqliteMigrationRunner _migrationRunner;

    public JsonProjectStore(ISqliteMigrationRunner? migrationRunner = null)
    {
        _migrationRunner = migrationRunner ?? new SqliteMigrationRunner();
    }

    public async Task<ProjectManifest?> ReadManifestAsync(string directory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var path = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        try
        {
            return await JsonSerializer.DeserializeAsync<ProjectManifest>(stream, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new ProjectManifestCorruptException(path, ex);
        }
    }

    public async Task WriteManifestAtomicallyAsync(ProjectManifest manifest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(manifest.RootDirectory);
        var finalPath = Path.Combine(manifest.RootDirectory, ManifestFileName);
        // Unique per call (not just per path): two concurrent writers to the same directory must
        // never share a temp file, or one's in-flight write/cleanup can clobber the other's.
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(manifest);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            // Checked again after the (cancelable) write completes, before the file becomes
            // visible under its real name: a cancellation that lands here must leave either the
            // old manifest (if any) or nothing at all under project.json, never a half-written
            // file promoted into place.
            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public async Task MigrateDatabaseAsync(string directory, int targetSchema, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        await _migrationRunner.MigrateAsync(directory, targetSchema, ct).ConfigureAwait(false);
    }
}
