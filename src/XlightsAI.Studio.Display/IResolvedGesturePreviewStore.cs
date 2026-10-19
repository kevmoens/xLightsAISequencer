namespace XlightsAI.Studio.Display;

/// <summary>
/// Persistence boundary for a resolved <see cref="ResolvedGesturePreview"/> cache entry. Mirrors
/// <see cref="ISpatialDisplayGraphStore"/>'s own save/load contract and atomic-write guarantee, with a
/// composite deterministic cache key (<see cref="GetPath"/>) covering every input that changes the
/// resolver's own output: layout, path identity/version, melody track identity and boundary policy.
/// </summary>
public interface IResolvedGesturePreviewStore
{
    /// <summary>The deterministic on-disk path a preview for this exact input combination would be saved at, whether or not it has been resolved yet.</summary>
    string GetPath(string sourceLayoutHash, Guid pathId, int pathVersion, Guid melodyTrackId, string boundaryPolicy);

    /// <summary>Persists <paramref name="preview"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(ResolvedGesturePreview preview, CancellationToken ct);

    /// <summary>Reads back a previously saved preview from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SpatialDisplayGraphCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="ResolvedGesturePreview"/>.</exception>
    Task<ResolvedGesturePreview> LoadAsync(string path, CancellationToken ct);
}
