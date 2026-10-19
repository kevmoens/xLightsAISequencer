using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="ISpatialRelationInferrer"/>. See the interface and <c>implementation-evidence/PHASE-09/STEP-060.md</c> for the full algorithm rationale.</summary>
public sealed class SpatialRelationInferrer : ISpatialRelationInferrer
{
    /// <summary>
    /// An inferred edge's <see cref="SpatialEdge.Confidence"/> at or below this value is this step's own
    /// disclosed convention for "uncertain order flagged" (Step 060's own required acceptance text,
    /// verbatim) -- not a separate boolean field, since confidence itself already is the flag.
    /// </summary>
    public const double UncertainConfidenceThreshold = 0.5;

    private const string GroupModelKind = "Group";
    private const string NoGeometryTag = "NoGeometry";

    public SpatialRelationInference Infer(SpatialDisplayGraph graph, GeometryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.MaxNeighborDistance < 0)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"GeometryPolicy.MaxNeighborDistance must be non-negative; was {policy.MaxNeighborDistance}.");
        }

        // Only real, geometry-bearing props participate -- a Group node's position is a centroid, not an
        // independent physical neighbor, and a node with no source geometry ("NoGeometry") has nothing to
        // infer an order from (never guessed).
        var physicalNodes = graph.Nodes
            .Where(n => n.ModelKind != GroupModelKind && !n.Tags.Contains(NoGeometryTag))
            .ToList();

        // Rows: nodes sharing an identical normalized Y are one inferred horizontal row. Rows are then
        // ordered bottom-to-top by that shared Y for the later inter-row vertical pass.
        var rows = physicalNodes
            .GroupBy(n => n.Y)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(n => n.X).ThenBy(n => n.Id.Value, StringComparer.Ordinal).ToList())
            .ToList();

        var edges = new List<SpatialEdge>();
        var centers = new List<ModelId>();
        var rowRepresentatives = new List<DisplayNode>();

        foreach (var row in rows)
        {
            // Center: only an odd-sized row has a single, unambiguous median node -- an even-sized row has
            // two middle nodes and contributes no center rather than guessing between them.
            if (row.Count % 2 == 1)
            {
                var center = row[row.Count / 2];
                centers.Add(center.Id);
                rowRepresentatives.Add(center);
            }
            else
            {
                rowRepresentatives.Add(row[0]);
            }

            if (row.Count < 2)
            {
                continue;
            }

            var gaps = new double[row.Count - 1];
            for (var i = 0; i < gaps.Length; i++)
            {
                gaps[i] = row[i + 1].X - row[i].X;
            }

            var medianGap = Median(gaps);

            for (var i = 0; i < gaps.Length; i++)
            {
                var gap = gaps[i];
                if (gap > policy.MaxNeighborDistance)
                {
                    // Too far apart within this row to claim adjacency at all, regardless of confidence --
                    // GeometryPolicy.MaxNeighborDistance's own hard cutoff.
                    continue;
                }

                var confidence = Confidence(gap, medianGap);
                edges.Add(new SpatialEdge(row[i].Id, row[i + 1].Id, "Neighbor", gap, UserDefined: false, Confidence: confidence));
                edges.Add(new SpatialEdge(row[i].Id, row[i + 1].Id, "LeftOf", gap, UserDefined: false, Confidence: confidence));
            }
        }

        // Vertical order between already-structurally-distinct rows is not gated by MaxNeighborDistance --
        // unlike two props that may or may not be close enough to be neighbors, two rows already exist as
        // separate clusters by construction, so their relative order is asserted (with the same
        // outlier-tolerant confidence formula) rather than possibly omitted.
        if (rowRepresentatives.Count >= 2)
        {
            var rowGaps = new double[rowRepresentatives.Count - 1];
            for (var i = 0; i < rowGaps.Length; i++)
            {
                rowGaps[i] = rowRepresentatives[i + 1].Y - rowRepresentatives[i].Y;
            }

            var medianRowGap = Median(rowGaps);

            for (var i = 0; i < rowGaps.Length; i++)
            {
                var below = rowRepresentatives[i];
                var above = rowRepresentatives[i + 1];
                var confidence = Confidence(rowGaps[i], medianRowGap);
                edges.Add(new SpatialEdge(below.Id, above.Id, "Below", rowGaps[i], UserDefined: false, Confidence: confidence));
            }
        }

        return new SpatialRelationInference(edges, centers);
    }

    private static double Confidence(double gap, double medianGap)
    {
        if (medianGap <= 0 || gap <= 0)
        {
            return 1.0;
        }

        var ratio = Math.Min(gap, medianGap) / Math.Max(gap, medianGap);
        return Math.Clamp(ratio, 0.0, 1.0);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
