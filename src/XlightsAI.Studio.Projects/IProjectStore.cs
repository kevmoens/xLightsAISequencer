namespace XlightsAI.Studio.Projects;

/// <summary>
/// Persistence boundary for a single project's manifest, matching PHASE-01 Section 3's proposed
/// signature. <see cref="MigrateDatabaseAsync"/> is declared here (so the interface shape matches
/// the spec and later callers can depend on it) but this step does not implement SQLite migration
/// — that is explicitly Step 007's scope ("Add SQLite persistence with transactional migrations
/// and backups"). <see cref="JsonProjectStore"/> implements it as a documented not-yet-implemented
/// stub rather than silently pretending migration happened.
/// </summary>
public interface IProjectStore
{
    Task<ProjectManifest?> ReadManifestAsync(string directory, CancellationToken ct);

    Task WriteManifestAtomicallyAsync(ProjectManifest manifest, CancellationToken ct);

    Task MigrateDatabaseAsync(string directory, int targetSchema, CancellationToken ct);
}
