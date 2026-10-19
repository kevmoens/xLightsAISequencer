namespace XlightsAI.Studio.Display;

/// <summary>
/// Persistence boundary for a saved <see cref="LockedNodeSet"/>. Mirrors <see cref="IRoleAssignmentStore"/>'s
/// own save/load contract, atomic-write guarantee and deterministic-path-by-source-layout-hash convention --
/// a sibling aggregate at its own separate on-disk path, so saving/rebuilding one of the graph cache, the
/// layout corrections, the named path set, role assignments or locked nodes never touches any of the others.
/// </summary>
public interface ILockedNodeStore
{
    /// <summary>The deterministic on-disk path locked nodes for <paramref name="sourceLayoutHash"/> would be saved at, whether or not any have been saved yet.</summary>
    string GetPath(string sourceLayoutHash);

    /// <summary>Persists <paramref name="locked"/> atomically at <see cref="GetPath"/> and returns that path. Never touches the original xLights show files or any sibling cache file.</summary>
    Task<string> SaveAsync(LockedNodeSet locked, CancellationToken ct);

    /// <summary>Reads back a previously saved locked-node set from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SpatialDisplayGraphCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="LockedNodeSet"/>.</exception>
    Task<LockedNodeSet> LoadAsync(string path, CancellationToken ct);
}
