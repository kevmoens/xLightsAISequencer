using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="IDisplayGraphBuilder"/>. See the interface and <c>implementation-evidence/PHASE-09/STEP-059.md</c> for the full algorithm rationale.</summary>
public sealed class DisplayGraphBuilder : IDisplayGraphBuilder
{
    private const string NoGeometryTag = "NoGeometry";
    private const string GroupModelKind = "Group";

    public SpatialDisplayGraph Build(DisplayGraph imported, GeometryPolicy policy) =>
        Build(imported, policy, []);

    public SpatialDisplayGraph Build(DisplayGraph imported, GeometryPolicy policy, IReadOnlyList<NodeAlias> aliases)
    {
        ArgumentNullException.ThrowIfNull(imported);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(aliases);

        if (policy.MaxNeighborDistance < 0)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"GeometryPolicy.MaxNeighborDistance must be non-negative; was {policy.MaxNeighborDistance}.");
        }

        var duplicateModelName = imported.Models
            .GroupBy(m => m.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateModelName is not null)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Imported layout contains more than one model named '{duplicateModelName.Key}', which would produce an ambiguous stable node id.");
        }

        var aliasByXlightsName = new Dictionary<string, ModelId>(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            if (aliasByXlightsName.TryGetValue(alias.XlightsName, out var existing) && !existing.Equals(alias.NodeId))
            {
                throw new SpatialDisplayGraphInvalidException(
                    $"Alias set maps xLights name '{alias.XlightsName}' to more than one stable node id ('{existing.Value}' and '{alias.NodeId.Value}').");
            }

            aliasByXlightsName[alias.XlightsName] = alias.NodeId;
        }

        var (minX, maxX, minY, maxY) = ComputeBounds(imported.Models);

        var modelNodes = new List<DisplayNode>();
        var modelNodeByXlightsName = new Dictionary<string, DisplayNode>(StringComparer.Ordinal);
        var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in imported.Models)
        {
            var nodeId = aliasByXlightsName.TryGetValue(model.XlightsName, out var aliased) ? aliased : model.Id;
            if (!seenNodeIds.Add(nodeId.Value))
            {
                throw new SpatialDisplayGraphInvalidException(
                    $"Alias set assigns stable node id '{nodeId.Value}' to more than one imported model, which would produce an ambiguous node.");
            }

            var (x, y, hasGeometry) = NormalizePosition(model.PreviewPoints, minX, maxX, minY, maxY);
            var tags = hasGeometry ? new HashSet<string>() : new HashSet<string> { NoGeometryTag };
            var node = new DisplayNode(nodeId, model.XlightsName, x, y, model.Kind, tags);
            modelNodes.Add(node);
            modelNodeByXlightsName[model.XlightsName] = node;
        }

        var groupNodes = new List<DisplayNode>();
        var edges = new List<SpatialEdge>();
        if (policy.RespectGroups)
        {
            var groupNodeByName = new Dictionary<string, DisplayNode>(StringComparer.Ordinal);
            foreach (var group in imported.Groups)
            {
                var groupNodeId = new ModelId(group.Name);
                if (!seenNodeIds.Add(groupNodeId.Value))
                {
                    throw new SpatialDisplayGraphInvalidException(
                        $"Model group '{group.Name}' collides with an existing model or group node id.");
                }

                var resolvedMemberNodes = group.Members
                    .Where(m => m.Kind == DisplayGroupMemberKind.Model)
                    .Select(m => modelNodeByXlightsName.TryGetValue(m.RawName, out var n) ? n : null)
                    .Where(n => n is not null && !n.Tags.Contains(NoGeometryTag))
                    .Select(n => n!)
                    .ToList();

                var hasCentroid = resolvedMemberNodes.Count > 0;
                var groupX = hasCentroid ? resolvedMemberNodes.Average(n => n.X) : 0.5;
                var groupY = hasCentroid ? resolvedMemberNodes.Average(n => n.Y) : 0.5;
                var groupTags = hasCentroid ? new HashSet<string>() : new HashSet<string> { NoGeometryTag };

                var groupNode = new DisplayNode(groupNodeId, group.Name, groupX, groupY, GroupModelKind, groupTags);
                groupNodes.Add(groupNode);
                groupNodeByName[group.Name] = groupNode;
            }

            foreach (var group in imported.Groups)
            {
                var groupNode = groupNodeByName[group.Name];
                foreach (var member in group.Members)
                {
                    switch (member.Kind)
                    {
                        case DisplayGroupMemberKind.Model when modelNodeByXlightsName.TryGetValue(member.RawName, out var memberNode):
                            edges.Add(new SpatialEdge(memberNode.Id, groupNode.Id, "GroupMember",
                                Distance(memberNode, groupNode), UserDefined: false));
                            break;
                        case DisplayGroupMemberKind.Group when groupNodeByName.TryGetValue(member.RawName, out var nestedNode):
                            edges.Add(new SpatialEdge(nestedNode.Id, groupNode.Id, "NestedGroup",
                                Distance(nestedNode, groupNode), UserDefined: false));
                            break;
                    }
                }
            }
        }

        var allNodes = new List<DisplayNode>(modelNodes.Count + groupNodes.Count);
        allNodes.AddRange(modelNodes);
        allNodes.AddRange(groupNodes);

        return new SpatialDisplayGraph(imported.SourceLayoutHash, allNodes, edges, [], Guid.NewGuid());
    }

    private static double Distance(DisplayNode a, DisplayNode b)
    {
        if (a.Tags.Contains(NoGeometryTag) || b.Tags.Contains(NoGeometryTag))
        {
            return 0;
        }

        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static (double MinX, double MaxX, double MinY, double MaxY) ComputeBounds(IReadOnlyList<DisplayModel> models)
    {
        var points = models.Where(m => m.PreviewPoints.Count > 0).Select(m => m.PreviewPoints[0]).ToList();
        if (points.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        return (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y), points.Max(p => p.Y));
    }

    private static (double X, double Y, bool HasGeometry) NormalizePosition(
        IReadOnlyList<Point2> previewPoints, double minX, double maxX, double minY, double maxY)
    {
        if (previewPoints.Count == 0)
        {
            return (0.5, 0.5, false);
        }

        var point = previewPoints[0];
        var x = maxX > minX ? (point.X - minX) / (maxX - minX) : 0.5;
        var y = maxY > minY ? (point.Y - minY) / (maxY - minY) : 0.5;
        return (x, y, true);
    }
}
