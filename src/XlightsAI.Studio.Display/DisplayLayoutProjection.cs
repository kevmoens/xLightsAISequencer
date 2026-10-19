using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// Pure function that projects a persisted <see cref="DisplayLayoutCorrections"/> on top of a freshly
/// built <see cref="SpatialDisplayGraph"/> -- the one place Step 061's own user-authored position
/// overrides and list-editor display order are actually applied to node data for rendering. Never
/// itself touches disk, and never mutates <paramref name="graph"/>'s own <see cref="DisplayNode.X"/>/
/// <see cref="DisplayNode.Y"/> in place; <see cref="IDisplayLayoutEditor"/> is the only caller that
/// also persists.
/// </summary>
public static class DisplayLayoutProjection
{
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="corrections"/> was saved against a different source layout than <paramref name="graph"/> -- applying it anyway would silently reorder/reposition nodes against stale coordinates (Section 4: "never silently reorder a saved path when source coordinates change").</exception>
    public static SpatialDisplayGraph Apply(SpatialDisplayGraph graph, DisplayLayoutCorrections corrections)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(corrections);

        if (!string.Equals(graph.SourceLayoutHash, corrections.SourceLayoutHash, StringComparison.Ordinal))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Display layout corrections for source hash '{corrections.SourceLayoutHash}' do not match graph hash '{graph.SourceLayoutHash}'.");
        }

        var overridesByNodeId = corrections.PositionOverrides.ToDictionary(o => o.NodeId);
        var correctedNodes = graph.Nodes
            .Select(n => overridesByNodeId.TryGetValue(n.Id, out var o) ? n with { X = o.X, Y = o.Y } : n)
            .ToList();

        var nodesById = correctedNodes.ToDictionary(n => n.Id);
        var ordered = new List<DisplayNode>(correctedNodes.Count);
        var used = new HashSet<ModelId>();
        foreach (var id in corrections.DisplayOrder)
        {
            if (nodesById.TryGetValue(id, out var node) && used.Add(id))
            {
                ordered.Add(node);
            }
        }

        foreach (var node in correctedNodes)
        {
            if (used.Add(node.Id))
            {
                ordered.Add(node);
            }
        }

        return graph with { Nodes = ordered };
    }
}
