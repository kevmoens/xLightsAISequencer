using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065's own application-orchestration boundary: locks/unlocks/persists
/// <see cref="LockedNodeSet"/> entries for an already-built <see cref="SpatialDisplayGraph"/>. Every method
/// rejects an invalid input (unknown node id, locking an already-locked node, unlocking a node that is not
/// locked) <em>before</em> loading or writing anything -- this step's own required failure/edge case,
/// verbatim: "Invalid input, cancellation or stale persisted state must not corrupt the project."
/// </summary>
public interface ILockedNodeCatalog
{
    /// <summary>Loads previously saved locked nodes for <paramref name="sourceLayoutHash"/>, or <see cref="LockedNodeSet.Empty"/> if none have ever been saved.</summary>
    Task<LockedNodeSet> GetLockedNodesAsync(string sourceLayoutHash, CancellationToken ct);

    /// <summary>Locks <paramref name="nodeId"/>, excluding it from future route validation.</summary>
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="nodeId"/> does not exist in <paramref name="graph"/>, or is already locked.</exception>
    Task<LockedNodeSet> LockAsync(SpatialDisplayGraph graph, ModelId nodeId, CancellationToken ct);

    /// <summary>Unlocks <paramref name="nodeId"/>, making it eligible for route validation again.</summary>
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="nodeId"/> is not currently locked.</exception>
    Task<LockedNodeSet> UnlockAsync(SpatialDisplayGraph graph, ModelId nodeId, CancellationToken ct);
}
