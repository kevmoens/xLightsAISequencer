using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 062's own application-orchestration boundary: suggests, validates, persists and
/// versions named <see cref="SpatialPath"/>s for an already-built <see cref="SpatialDisplayGraph"/>.
/// Every method rejects an invalid input (unknown node id, unsupported direction policy, a duplicate
/// path name without explicit replace consent, a reorder/reverse against an unknown path id) <em>before</em>
/// loading or writing anything -- this step's own required failure/edge case, verbatim: "Invalid input,
/// cancellation or stale persisted state must not corrupt the project."
/// </summary>
public interface ISpatialPathCatalog
{
    /// <summary>Loads previously saved named paths for <paramref name="sourceLayoutHash"/>, or <see cref="SpatialPathSet.Empty"/> if none have ever been saved.</summary>
    Task<SpatialPathSet> GetPathsAsync(string sourceLayoutHash, CancellationToken ct);

    /// <summary>
    /// Builds a new named path via <see cref="ISpatialPathSuggester"/> and saves it. When no existing
    /// path named <paramref name="name"/> exists yet, it is added as a brand-new path at
    /// <see cref="SpatialPath.Version"/> <c>0</c>. When one already exists and <paramref name="allowReplaceExisting"/>
    /// is <see langword="false"/> (the default-safe choice -- implementation guidance: "reject duplicate
    /// IDs unless repeat intentionally allowed"), the call is rejected outright. When one already exists
    /// and <paramref name="allowReplaceExisting"/> is <see langword="true"/>, the existing path's own
    /// identity (<see cref="SpatialPath.Id"/>) is kept, its prior version is moved into
    /// <see cref="SpatialPathSet.History"/>, and the new ordering is saved at <c>Version + 1</c>.
    /// </summary>
    /// <exception cref="SpatialDisplayGraphInvalidException">
    /// <paramref name="candidateNodeIds"/>/<paramref name="directionPolicy"/> are rejected by
    /// <see cref="ISpatialPathSuggester.Suggest"/>, or a path named <paramref name="name"/> already
    /// exists and <paramref name="allowReplaceExisting"/> is <see langword="false"/>.
    /// </exception>
    Task<SpatialPathSet> CreateAsync(SpatialDisplayGraph graph, string name, IReadOnlyList<ModelId> candidateNodeIds,
        string directionPolicy, bool isClosed, bool allowReplaceExisting, CancellationToken ct);

    /// <summary>Reorders the live path identified by <paramref name="pathId"/>, moving its prior version into <see cref="SpatialPathSet.History"/>.</summary>
    /// <exception cref="SpatialDisplayGraphInvalidException">No live path with <paramref name="pathId"/> exists, or <paramref name="newOrder"/> is not an exact permutation of that path's own nodes.</exception>
    Task<SpatialPathSet> ReorderAsync(SpatialDisplayGraph graph, Guid pathId, IReadOnlyList<ModelId> newOrder, CancellationToken ct);

    /// <summary>Reverses the live path identified by <paramref name="pathId"/>, moving its prior version into <see cref="SpatialPathSet.History"/>.</summary>
    /// <exception cref="SpatialDisplayGraphInvalidException">No live path with <paramref name="pathId"/> exists.</exception>
    Task<SpatialPathSet> ReverseAsync(SpatialDisplayGraph graph, Guid pathId, CancellationToken ct);
}
