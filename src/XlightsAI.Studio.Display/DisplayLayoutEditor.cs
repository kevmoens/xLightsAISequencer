using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="IDisplayLayoutEditor"/>, backed by a real <see cref="IDisplayLayoutCorrectionsStore"/>. See the interface and <c>implementation-evidence/PHASE-09/STEP-061.md</c> for the full rationale.</summary>
public sealed class DisplayLayoutEditor : IDisplayLayoutEditor
{
    private readonly IDisplayLayoutCorrectionsStore _store;

    public DisplayLayoutEditor(IDisplayLayoutCorrectionsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<DisplayLayoutCorrections> GetCorrectionsAsync(string sourceLayoutHash, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);

        var path = _store.GetPath(sourceLayoutHash);
        if (!File.Exists(path))
        {
            return DisplayLayoutCorrections.Empty(sourceLayoutHash);
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<DisplayLayoutCorrections> CorrectPositionAsync(
        SpatialDisplayGraph graph, ModelId nodeId, double newX, double newY, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (!graph.Nodes.Any(n => n.Id == nodeId))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Node '{nodeId.Value}' does not exist in the layout with source hash '{graph.SourceLayoutHash}'.");
        }

        if (!IsValidNormalizedCoordinate(newX) || !IsValidNormalizedCoordinate(newY))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Corrected position ({newX}, {newY}) must be finite and within [0, 1].");
        }

        ct.ThrowIfCancellationRequested();

        var current = await GetCorrectionsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        var newOverrides = current.PositionOverrides
            .Where(o => o.NodeId != nodeId)
            .Append(new NodePositionOverride(nodeId, newX, newY))
            .ToList();

        var updated = current with { PositionOverrides = newOverrides, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    public async Task<DisplayLayoutCorrections> ReorderAsync(
        SpatialDisplayGraph graph, IReadOnlyList<ModelId> newOrder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(newOrder);

        var graphNodeIds = new HashSet<ModelId>(graph.Nodes.Select(n => n.Id));
        var orderedIds = new HashSet<ModelId>();
        foreach (var id in newOrder)
        {
            if (!graphNodeIds.Contains(id))
            {
                throw new SpatialDisplayGraphInvalidException(
                    $"Reorder references node '{id.Value}', which does not exist in the layout with source hash '{graph.SourceLayoutHash}'.");
            }

            if (!orderedIds.Add(id))
            {
                throw new SpatialDisplayGraphInvalidException($"Reorder lists node '{id.Value}' more than once.");
            }
        }

        if (orderedIds.Count != graphNodeIds.Count)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Reorder must list every node exactly once ({graphNodeIds.Count} expected, {orderedIds.Count} given).");
        }

        ct.ThrowIfCancellationRequested();

        var current = await GetCorrectionsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        var updated = current with { DisplayOrder = newOrder, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    private static bool IsValidNormalizedCoordinate(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}
