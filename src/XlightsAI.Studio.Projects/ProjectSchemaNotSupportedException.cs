namespace XlightsAI.Studio.Projects;

/// <summary>
/// Thrown by <see cref="IProjectService.OpenAsync"/> when a project's persisted
/// <see cref="ProjectManifest.SchemaVersion"/> is newer than
/// <see cref="JsonProjectStore.CurrentSchemaVersion"/>. The manifest file on disk is never
/// mutated, deleted or "repaired" in response to this — PHASE-01 Section 4's read-only-newer-
/// schema rule ("do not downgrade") applies to project manifests exactly as it does to the
/// project database.
/// </summary>
public sealed class ProjectSchemaNotSupportedException : Exception
{
    public ProjectSchemaNotSupportedException(int foundSchemaVersion, int supportedSchemaVersion)
        : base(
            $"Project schema version {foundSchemaVersion} is newer than the supported version " +
            $"{supportedSchemaVersion}. Opening it read-only/by a newer app build is required; " +
            "this version will not downgrade or modify it.")
    {
        FoundSchemaVersion = foundSchemaVersion;
        SupportedSchemaVersion = supportedSchemaVersion;
    }

    public int FoundSchemaVersion { get; }

    public int SupportedSchemaVersion { get; }
}
