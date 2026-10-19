using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 060 ("Infer neighbors, left/right, center and vertical relations with confidence") own
/// output: additional <see cref="SpatialEdge"/>s describing inferred spatial order, plus the node(s)
/// identified as the "center" of each inferred horizontal row. Never merged automatically into a
/// <see cref="SpatialDisplayGraph"/> -- the caller explicitly appends <see cref="Edges"/> (e.g. via
/// <c>graph with { Edges = graph.Edges.Concat(result.Edges).ToList() }</c>) before persisting, so
/// <see cref="IDisplayGraphBuilder.Build"/>'s own already-accepted Step 059 output and edge count are never
/// silently changed by this step.
/// </summary>
/// <param name="Edges">
/// Inferred <c>"Neighbor"</c> (adjacency within <see cref="GeometryPolicy.MaxNeighborDistance"/>),
/// <c>"LeftOf"</c> (horizontal order within one inferred row) and <c>"Below"</c> (vertical order between
/// rows) edges. Every edge's <see cref="SpatialEdge.UserDefined"/> is <c>false</c> and
/// <see cref="SpatialEdge.Confidence"/> reflects how close the underlying geometry is to that row's own
/// typical spacing -- never a bare assertion of physical truth.
/// </param>
/// <param name="CenterNodeIds">
/// The node at the midpoint of each inferred horizontal row that has a single, unambiguous median position
/// (an odd-sized row only -- an even-sized row has no unique center and contributes nothing here, rather
/// than guessing between its two middle nodes).
/// </param>
public sealed record SpatialRelationInference(IReadOnlyList<SpatialEdge> Edges, IReadOnlyList<ModelId> CenterNodeIds);
