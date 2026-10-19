namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3 proposed contract: shared policy controlling how <see cref="IDisplayGraphBuilder"/>
/// interprets a layout's geometry and group structure.
/// </summary>
/// <param name="MaxNeighborDistance">
/// Not consumed by Step 059's own group-membership-only edge building (validated here only, because both
/// steps share this one policy type, per Section 3's own single proposed signature). Step 060's own
/// <see cref="ISpatialRelationInferrer"/> consumes it as the hard cutoff beyond which two adjacent nodes
/// within one inferred horizontal row are never claimed to be neighbors at all, regardless of confidence
/// (vertical row-to-row order is not gated by this value -- see
/// <c>implementation-evidence/PHASE-09/STEP-060.md</c> §3 for the disclosed rationale).
/// </param>
/// <param name="RespectGroups">
/// When <c>true</c> (the default/expected setting), <see cref="IDisplayGraphBuilder.Build"/> also
/// projects every <c>DisplayModelGroup</c> as its own <see cref="DisplayNode"/> plus
/// <c>"GroupMember"</c>/<c>"NestedGroup"</c> <see cref="SpatialEdge"/>s. When <c>false</c>, groups are
/// ignored entirely and the graph contains only model nodes with no edges -- Section 3: "Keep group/
/// submodel identities and nested grouping without assuming they are pixel-equivalent to simple prop
/// nodes," read here as: group projection is policy-controlled, not unconditional.
/// </param>
public sealed record GeometryPolicy(double MaxNeighborDistance, bool RespectGroups);
