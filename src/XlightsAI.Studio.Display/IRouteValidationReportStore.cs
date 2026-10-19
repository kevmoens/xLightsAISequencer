namespace XlightsAI.Studio.Display;

/// <summary>
/// Persistence boundary for a resolved <see cref="RouteValidationReport"/> cache entry. Mirrors
/// <see cref="IResolvedGesturePreviewStore"/>'s own save/load contract and atomic-write guarantee, with a
/// composite deterministic cache key (<see cref="GetPath"/>) covering every input that changes the
/// validator's own output: layout, path identity/version and recipe id.
/// </summary>
public interface IRouteValidationReportStore
{
    /// <summary>The deterministic on-disk path a report for this exact input combination would be saved at, whether or not it has been validated yet.</summary>
    string GetPath(string sourceLayoutHash, Guid pathId, int pathVersion, string? recipeId);

    /// <summary>Persists <paramref name="report"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(RouteValidationReport report, string? recipeId, CancellationToken ct);

    /// <summary>Reads back a previously saved report from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SpatialDisplayGraphCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="RouteValidationReport"/>.</exception>
    Task<RouteValidationReport> LoadAsync(string path, CancellationToken ct);
}
