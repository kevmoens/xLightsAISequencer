using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="ISpatialPathSuggester"/>. See the interface and <c>implementation-evidence/PHASE-09/STEP-062.md</c> for the full algorithm rationale.</summary>
public sealed class SpatialPathSuggester : ISpatialPathSuggester
{
    private const string NoGeometryTag = "NoGeometry";

    public SpatialPath Suggest(SpatialDisplayGraph graph, string name, IReadOnlyList<ModelId> candidateNodeIds,
        string directionPolicy, bool isClosed)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(candidateNodeIds);
        ArgumentNullException.ThrowIfNull(directionPolicy);

        if (candidateNodeIds.Count == 0)
        {
            throw new SpatialDisplayGraphInvalidException("A spatial path needs at least one node; none were supplied.");
        }

        if (!DirectionPolicy.All.Contains(directionPolicy, StringComparer.Ordinal))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Unknown direction policy '{directionPolicy}'; expected one of: {string.Join(", ", DirectionPolicy.All)}.");
        }

        var nodesById = graph.Nodes.ToDictionary(n => n.Id);
        var seen = new HashSet<ModelId>();
        var candidateNodes = new List<DisplayNode>(candidateNodeIds.Count);
        foreach (var id in candidateNodeIds)
        {
            if (!nodesById.TryGetValue(id, out var node))
            {
                throw new SpatialDisplayGraphInvalidException(
                    $"Node '{id.Value}' does not exist in the layout with source hash '{graph.SourceLayoutHash}'.");
            }

            if (!seen.Add(id))
            {
                throw new SpatialDisplayGraphInvalidException($"Candidate node '{id.Value}' is listed more than once.");
            }

            if (node.Tags.Contains(NoGeometryTag))
            {
                throw new SpatialDisplayGraphInvalidException(
                    $"Node '{id.Value}' has no resolvable geometry; it cannot be ordered by direction policy '{directionPolicy}'.");
            }

            candidateNodes.Add(node);
        }

        var ordered = directionPolicy switch
        {
            DirectionPolicy.LeftToRight => candidateNodes.OrderBy(n => n.X).ThenBy(n => n.Id.Value, StringComparer.Ordinal).ToList(),
            DirectionPolicy.RightToLeft => candidateNodes.OrderByDescending(n => n.X).ThenBy(n => n.Id.Value, StringComparer.Ordinal).ToList(),
            DirectionPolicy.TopToBottom => candidateNodes.OrderBy(n => n.Y).ThenBy(n => n.Id.Value, StringComparer.Ordinal).ToList(),
            DirectionPolicy.BottomToTop => candidateNodes.OrderByDescending(n => n.Y).ThenBy(n => n.Id.Value, StringComparer.Ordinal).ToList(),
            DirectionPolicy.OutsideIn => OutsideIn(candidateNodes),
            _ => throw new SpatialDisplayGraphInvalidException($"Unknown direction policy '{directionPolicy}'."),
        };

        return new SpatialPath(Guid.NewGuid(), name, ordered.Select(n => n.Id).ToList(), isClosed, Version: 0);
    }

    /// <summary>Leftmost, rightmost, second-leftmost, second-rightmost... working inward; any single middle node (odd count) is appended once, never doubled.</summary>
    private static List<DisplayNode> OutsideIn(IReadOnlyList<DisplayNode> candidateNodes)
    {
        var sorted = candidateNodes.OrderBy(n => n.X).ThenBy(n => n.Id.Value, StringComparer.Ordinal).ToList();
        var result = new List<DisplayNode>(sorted.Count);
        var left = 0;
        var right = sorted.Count - 1;
        var takeLeft = true;
        while (left <= right)
        {
            if (left == right)
            {
                result.Add(sorted[left]);
                break;
            }

            if (takeLeft)
            {
                result.Add(sorted[left]);
                left++;
            }
            else
            {
                result.Add(sorted[right]);
                right--;
            }

            takeLeft = !takeLeft;
        }

        return result;
    }
}
