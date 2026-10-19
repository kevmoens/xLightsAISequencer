namespace XlightsAI.Studio.Display;

/// <summary>
/// Persistence boundary for a saved <see cref="DisplayLayoutCorrections"/> set. Mirrors
/// <see cref="ISpatialDisplayGraphStore"/>'s own save/load contract, atomic-write guarantee and
/// deterministic-path-by-source-layout-hash convention -- the two aggregates are siblings (one is the
/// rebuildable graph, the other the user-authored corrections on top of it) and deliberately live at
/// separate on-disk paths so rebuilding/overwriting one never touches the other.
/// </summary>
public interface IDisplayLayoutCorrectionsStore
{
    /// <summary>The deterministic on-disk path corrections for <paramref name="sourceLayoutHash"/> would be saved at, whether or not any have been saved yet.</summary>
    string GetPath(string sourceLayoutHash);

    /// <summary>Persists <paramref name="corrections"/> atomically at <see cref="GetPath"/> and returns that path. Never touches the original xLights show files or the separate <see cref="ISpatialDisplayGraphStore"/> cache.</summary>
    Task<string> SaveAsync(DisplayLayoutCorrections corrections, CancellationToken ct);

    /// <summary>Reads back previously saved corrections from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SpatialDisplayGraphCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="DisplayLayoutCorrections"/>.</exception>
    Task<DisplayLayoutCorrections> LoadAsync(string path, CancellationToken ct);
}
