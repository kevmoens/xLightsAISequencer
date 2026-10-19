using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="ILockedNodeCatalog"/>. See the interface for the full rationale.</summary>
public sealed class LockedNodeCatalog : ILockedNodeCatalog
{
    private readonly ILockedNodeStore _store;

    public LockedNodeCatalog(ILockedNodeStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<LockedNodeSet> GetLockedNodesAsync(string sourceLayoutHash, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);

        var path = _store.GetPath(sourceLayoutHash);
        if (!File.Exists(path))
        {
            return LockedNodeSet.Empty(sourceLayoutHash);
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<LockedNodeSet> LockAsync(SpatialDisplayGraph graph, ModelId nodeId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (!graph.Nodes.Any(n => n.Id == nodeId))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Node '{nodeId.Value}' does not exist in the layout with source hash '{graph.SourceLayoutHash}'.");
        }

        var current = await GetLockedNodesAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        if (current.LockedNodeIds.Contains(nodeId))
        {
            throw new SpatialDisplayGraphInvalidException($"Node '{nodeId.Value}' is already locked.");
        }

        ct.ThrowIfCancellationRequested();

        var updated = current with { LockedNodeIds = [.. current.LockedNodeIds, nodeId], Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    public async Task<LockedNodeSet> UnlockAsync(SpatialDisplayGraph graph, ModelId nodeId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var current = await GetLockedNodesAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        if (!current.LockedNodeIds.Contains(nodeId))
        {
            throw new SpatialDisplayGraphInvalidException($"Node '{nodeId.Value}' is not currently locked.");
        }

        ct.ThrowIfCancellationRequested();

        var updated = current with { LockedNodeIds = current.LockedNodeIds.Where(id => id != nodeId).ToList(), Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }
}
