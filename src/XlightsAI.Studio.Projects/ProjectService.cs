using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Projects;

/// <summary>
/// Default <see cref="IProjectService"/>: create/open/save a project's versioned manifest via
/// <see cref="IProjectStore"/>, and track a cross-project "recent projects" list via
/// <see cref="IRecentProjectsStore"/>. Never writes anywhere the caller did not explicitly name as
/// a project's own <see cref="ProjectManifest.RootDirectory"/> — in particular never touches an
/// xLights show folder, which this step never receives a path to at all.
/// </summary>
public sealed class ProjectService : IProjectService
{
    private const int MaxRecentEntries = 20;

    private readonly IProjectStore _store;
    private readonly IRecentProjectsStore _recents;

    /// <summary>
    /// Manifests this process currently has open, keyed by <see cref="ProjectId"/>. Required for
    /// <see cref="SaveAsync"/>, which is ID-addressed per PHASE-01 Section 3's proposed signature
    /// even though the underlying store is directory-addressed.
    /// </summary>
    private readonly Dictionary<ProjectId, ProjectManifest> _open = new();

    public ProjectService(IProjectStore store, IRecentProjectsStore recents)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _recents = recents ?? throw new ArgumentNullException(nameof(recents));
    }

    public async Task<ProjectManifest> CreateAsync(string directory, string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ct.ThrowIfCancellationRequested();

        var fullDirectory = Path.GetFullPath(directory);

        // Non-destructive create (PHASE-01 Section 4): never silently overwrite a directory that
        // is already a different project's workspace.
        var existing = await _store.ReadManifestAsync(fullDirectory, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new ProjectDirectoryInUseException(fullDirectory);
        }

        var manifest = new ProjectManifest(
            new ProjectId(Guid.NewGuid()),
            JsonProjectStore.CurrentSchemaVersion,
            name,
            fullDirectory,
            DateTimeOffset.UtcNow);

        await _store.WriteManifestAtomicallyAsync(manifest, ct).ConfigureAwait(false);

        // Best-effort workspace skeleton (PHASE-01 Section 4: cache/, logs/, exports/). Not
        // required for the manifest round-trip itself; a failure here would be a secondary,
        // non-corrupting concern, so it is allowed to throw rather than being silently swallowed —
        // there is no partial/inconsistent *manifest* state possible once this line is reached.
        Directory.CreateDirectory(Path.Combine(fullDirectory, "cache"));
        Directory.CreateDirectory(Path.Combine(fullDirectory, "logs"));
        Directory.CreateDirectory(Path.Combine(fullDirectory, "exports"));

        // studio.db (PHASE-01 Section 4: "migration-versioned mutable data") is created here,
        // transactionally, at the current schema version (Step 007).
        await _store.MigrateDatabaseAsync(fullDirectory, JsonProjectStore.CurrentDatabaseSchemaVersion, ct).ConfigureAwait(false);

        _open[manifest.Id] = manifest;
        TouchRecent(manifest);

        return manifest;
    }

    public async Task<ProjectManifest> OpenAsync(string directory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        ct.ThrowIfCancellationRequested();

        var fullDirectory = Path.GetFullPath(directory);
        var manifest = await _store.ReadManifestAsync(fullDirectory, ct).ConfigureAwait(false)
            ?? throw new DirectoryNotFoundException($"No project manifest found at '{fullDirectory}'.");

        if (manifest.SchemaVersion > JsonProjectStore.CurrentSchemaVersion)
        {
            // Required acceptance: "reject newer unsupported schema safely" — the manifest file
            // itself is left completely untouched; only the in-memory open/recents tracking is
            // skipped.
            throw new ProjectSchemaNotSupportedException(manifest.SchemaVersion, JsonProjectStore.CurrentSchemaVersion);
        }

        // Bring this project's studio.db up to the current schema, transactionally and with a
        // pre-migration backup (Step 007). A failed migration (locked file, interrupted write,
        // unsupported future schema) throws here, before the project is tracked as open or touched
        // in the recents list, and leaves both the manifest and the database exactly as found.
        await _store.MigrateDatabaseAsync(fullDirectory, JsonProjectStore.CurrentDatabaseSchemaVersion, ct).ConfigureAwait(false);

        _open[manifest.Id] = manifest;
        TouchRecent(manifest);

        return manifest;
    }

    public async Task SaveAsync(ProjectId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_open.TryGetValue(id, out var tracked))
        {
            throw new InvalidOperationException($"Project '{id.Value}' is not open; call OpenAsync or CreateAsync first.");
        }

        // Stale-persisted-state guard (PHASE-01 Section 6, Step 006 failure case): re-read what is
        // actually on disk right now and refuse to blindly overwrite it if it no longer matches
        // the project this process believes it has open.
        var onDisk = await _store.ReadManifestAsync(tracked.RootDirectory, ct).ConfigureAwait(false);
        if (onDisk is null)
        {
            throw new ProjectStateStaleException(
                $"Project manifest at '{tracked.RootDirectory}' no longer exists; refusing to recreate it via Save.");
        }

        if (onDisk.Id != tracked.Id)
        {
            throw new ProjectStateStaleException(
                $"Project manifest at '{tracked.RootDirectory}' now belongs to project '{onDisk.Id.Value}', " +
                $"not the open project '{tracked.Id.Value}'; refusing to overwrite it.");
        }

        await _store.WriteManifestAtomicallyAsync(tracked, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ProjectSummary>> RecentAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Prune entries whose project no longer resolves on disk (moved/deleted since last
        // recorded) rather than surfacing a crash or a dead entry the user cannot act on.
        var resolvable = _recents.Load()
            .Where(entry => File.Exists(Path.Combine(entry.RootDirectory, JsonProjectStore.ManifestFileName)))
            .OrderByDescending(entry => entry.LastOpenedUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<ProjectSummary>>(resolvable);
    }

    private void TouchRecent(ProjectManifest manifest)
    {
        var existing = _recents.Load()
            .Where(entry => entry.Id != manifest.Id)
            .ToList();

        existing.Insert(0, new ProjectSummary(manifest.Id, manifest.Name, DateTimeOffset.UtcNow, manifest.RootDirectory));

        _recents.Save(existing.Take(MaxRecentEntries).ToList());
    }
}
