namespace XlightsAI.Studio.Display;

/// <summary>
/// Persistence boundary for a built <see cref="SpatialDisplayGraph"/>. Mirrors
/// <see cref="XlightsAI.Studio.Structure.IBarFingerprintStore"/>'s own save/load contract and atomic-write
/// guarantee, including <see cref="GetPath"/>'s deterministic naming keyed by the source layout hash
/// (Section 3's own "Inputs:... source file hash") rather than a song id -- this aggregate is layout-
/// scoped, not song-scoped.
/// </summary>
public interface ISpatialDisplayGraphStore
{
    /// <summary>The deterministic on-disk path a spatial graph for <paramref name="sourceLayoutHash"/> would be saved at, whether or not it has been built yet.</summary>
    string GetPath(string sourceLayoutHash);

    /// <summary>Persists <paramref name="graph"/> atomically at <see cref="GetPath"/> and returns that path. Never touches the original xLights show files.</summary>
    Task<string> SaveAsync(SpatialDisplayGraph graph, CancellationToken ct);

    /// <summary>Reads back a previously saved graph from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SpatialDisplayGraphCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="SpatialDisplayGraph"/>.</exception>
    Task<SpatialDisplayGraph> LoadAsync(string path, CancellationToken ct);
}
