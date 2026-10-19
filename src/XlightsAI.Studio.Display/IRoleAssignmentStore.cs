namespace XlightsAI.Studio.Display;

/// <summary>
/// Persistence boundary for a saved <see cref="RoleAssignmentSet"/>. Mirrors <see cref="ISpatialPathStore"/>'s
/// own save/load contract, atomic-write guarantee and deterministic-path-by-source-layout-hash convention
/// -- a sibling aggregate at its own separate on-disk path, so saving/rebuilding one of the graph cache,
/// the layout corrections, the named path set or role assignments never touches any of the others.
/// </summary>
public interface IRoleAssignmentStore
{
    /// <summary>The deterministic on-disk path role assignments for <paramref name="sourceLayoutHash"/> would be saved at, whether or not any have been saved yet.</summary>
    string GetPath(string sourceLayoutHash);

    /// <summary>Persists <paramref name="assignments"/> atomically at <see cref="GetPath"/> and returns that path. Never touches the original xLights show files or the separate graph/corrections/path caches.</summary>
    Task<string> SaveAsync(RoleAssignmentSet assignments, CancellationToken ct);

    /// <summary>Reads back a previously saved role assignment set from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SpatialDisplayGraphCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="RoleAssignmentSet"/>.</exception>
    Task<RoleAssignmentSet> LoadAsync(string path, CancellationToken ct);
}
