using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// Default <see cref="IRouteValidator"/>. Checks every <see cref="SpatialPath.OrderedModels"/> entry against
/// three independent fallback categories Section 2 names (missing, locked, incompatible recipe -- each
/// <see cref="RouteIssueSeverity.Error"/>, since none of the three can be silently worked around without
/// risking a mis-targeted XSQ effect), then checks the path's own normalized preview geometry for
/// self-intersection/coincident-node overlap (each <see cref="RouteIssueSeverity.Warning"/>, since a
/// geometrically crossing route -- e.g. a deliberate figure-eight -- is still artistically valid and is
/// surfaced for the user's own judgment, not rejected).
///
/// <para>
/// <b>Disclosed interpretation of "path intersections and overlaps" (this step's own implementation
/// guidance, verbatim):</b> Section 3 does not define these two words further. "Overlap" is read here as two
/// <em>different</em> node ids in the path occupying the exact same normalized preview position (e.g. a
/// layout/geometry quirk, not an intentional same-prop revisit -- a path legitimately revisiting the same
/// node id, such as an out-and-back route, is never itself flagged). "Intersection" is read as two
/// non-adjacent path segments (the straight line between two consecutive ordered nodes) crossing each other
/// in normalized 2D preview space. Both are computed only over nodes this validator can resolve a position
/// for (a node already reported <see cref="RouteIssueKind.MissingNode"/> contributes no geometry).
/// </para>
/// </summary>
public sealed class RouteValidator : IRouteValidator
{
    private const double CoincidenceEpsilon = 1e-9;

    public RouteValidationReport Validate(
        SpatialPath path,
        SpatialDisplayGraph display,
        LockedNodeSet lockedNodes,
        string? recipeId,
        EffectRecipeSupport recipeSupport)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(lockedNodes);
        ArgumentNullException.ThrowIfNull(recipeSupport);

        if (path.OrderedModels.Count == 0)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Validate requires path '{path.Name}' to have at least one ordered model; the supplied path was empty.");
        }

        if (!string.Equals(lockedNodes.SourceLayoutHash, display.SourceLayoutHash, StringComparison.Ordinal))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Locked-node set was authored against layout '{lockedNodes.SourceLayoutHash}' but display graph is for layout '{display.SourceLayoutHash}'; reload locked nodes for the current layout before validating.");
        }

        var nodesById = display.Nodes.ToDictionary(n => n.Id);
        var lockedSet = new HashSet<ModelId>(lockedNodes.LockedNodeIds);
        var issues = new List<RouteIssue>();

        foreach (var id in path.OrderedModels.Distinct())
        {
            if (!nodesById.TryGetValue(id, out var node))
            {
                issues.Add(new RouteIssue(id, RouteIssueKind.MissingNode, RouteIssueSeverity.Error,
                    $"Node '{id.Value}' referenced by path '{path.Name}' does not exist in the display graph."));
                continue;
            }

            if (lockedSet.Contains(id))
            {
                issues.Add(new RouteIssue(id, RouteIssueKind.LockedNode, RouteIssueSeverity.Error,
                    $"Node '{id.Value}' is locked/excluded and cannot be targeted by path '{path.Name}'."));
            }

            if (recipeId is not null &&
                recipeSupport.SupportedRecipeIdsByModelKind.TryGetValue(node.ModelKind, out var supported) &&
                !supported.Contains(recipeId))
            {
                issues.Add(new RouteIssue(id, RouteIssueKind.IncompatibleRecipe, RouteIssueSeverity.Error,
                    $"Node '{id.Value}' (model kind '{node.ModelKind}') does not declare support for recipe '{recipeId}'."));
            }
        }

        issues.AddRange(FindGeometryIssues(path, nodesById));

        return new RouteValidationReport(display.SourceLayoutHash, path.Id, path.Version, issues, Guid.NewGuid());
    }

    private static IEnumerable<RouteIssue> FindGeometryIssues(SpatialPath path, IReadOnlyDictionary<ModelId, DisplayNode> nodesById)
    {
        var ordered = path.OrderedModels;
        var points = new (ModelId Id, double X, double Y)?[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            points[i] = nodesById.TryGetValue(ordered[i], out var node) ? (ordered[i], node.X, node.Y) : null;
        }

        // Overlap: two *different* node ids occupying the exact same resolved position.
        for (var i = 0; i < points.Length; i++)
        {
            if (points[i] is not { } a)
            {
                continue;
            }

            for (var j = i + 1; j < points.Length; j++)
            {
                if (points[j] is not { } b || a.Id.Equals(b.Id))
                {
                    continue;
                }

                if (Math.Abs(a.X - b.X) < CoincidenceEpsilon && Math.Abs(a.Y - b.Y) < CoincidenceEpsilon)
                {
                    yield return new RouteIssue(b.Id, RouteIssueKind.PathOverlap, RouteIssueSeverity.Warning,
                        $"Node '{b.Id.Value}' occupies the exact same preview position as node '{a.Id.Value}' in path '{path.Name}'.");
                }
            }
        }

        // Intersection: non-adjacent segments of the ordered route crossing each other.
        var segmentCount = path.IsClosed ? ordered.Count : ordered.Count - 1;
        if (segmentCount < 2)
        {
            yield break;
        }

        for (var i = 0; i < segmentCount; i++)
        {
            var aStart = points[i];
            var aEnd = points[(i + 1) % points.Length];
            if (aStart is null || aEnd is null)
            {
                continue;
            }

            for (var j = i + 1; j < segmentCount; j++)
            {
                // Skip segments that are adjacent (share an endpoint), including the wrap-around pair for a closed path.
                var isAdjacent = j == i + 1 || (path.IsClosed && i == 0 && j == segmentCount - 1);
                if (isAdjacent)
                {
                    continue;
                }

                var bStart = points[j];
                var bEnd = points[(j + 1) % points.Length];
                if (bStart is null || bEnd is null)
                {
                    continue;
                }

                if (SegmentsIntersect(aStart.Value, aEnd.Value, bStart.Value, bEnd.Value))
                {
                    yield return new RouteIssue(null, RouteIssueKind.PathIntersection, RouteIssueSeverity.Warning,
                        $"Path '{path.Name}' crosses itself between '{aStart.Value.Id.Value}'->'{aEnd.Value.Id.Value}' and '{bStart.Value.Id.Value}'->'{bEnd.Value.Id.Value}'.");
                }
            }
        }
    }

    private static bool SegmentsIntersect(
        (ModelId Id, double X, double Y) p1, (ModelId Id, double X, double Y) p2,
        (ModelId Id, double X, double Y) p3, (ModelId Id, double X, double Y) p4)
    {
        var o1 = Orientation(p1, p2, p3);
        var o2 = Orientation(p1, p2, p4);
        var o3 = Orientation(p3, p4, p1);
        var o4 = Orientation(p3, p4, p2);

        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        if (o1 == 0 && OnSegment(p1, p3, p2))
        {
            return true;
        }

        if (o2 == 0 && OnSegment(p1, p4, p2))
        {
            return true;
        }

        if (o3 == 0 && OnSegment(p3, p1, p4))
        {
            return true;
        }

        return o4 == 0 && OnSegment(p3, p2, p4);
    }

    private static int Orientation((ModelId Id, double X, double Y) p, (ModelId Id, double X, double Y) q, (ModelId Id, double X, double Y) r)
    {
        var value = (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);
        if (Math.Abs(value) < CoincidenceEpsilon)
        {
            return 0;
        }

        return value > 0 ? 1 : -1;
    }

    private static bool OnSegment((ModelId Id, double X, double Y) p, (ModelId Id, double X, double Y) q, (ModelId Id, double X, double Y) r) =>
        q.X <= Math.Max(p.X, r.X) + CoincidenceEpsilon && q.X >= Math.Min(p.X, r.X) - CoincidenceEpsilon &&
        q.Y <= Math.Max(p.Y, r.Y) + CoincidenceEpsilon && q.Y >= Math.Min(p.Y, r.Y) - CoincidenceEpsilon;
}
