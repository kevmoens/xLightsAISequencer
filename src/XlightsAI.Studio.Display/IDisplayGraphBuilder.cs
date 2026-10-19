using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 059: projects a read-only imported <see cref="DisplayGraph"/> into a
/// <see cref="SpatialDisplayGraph"/> -- stable node ids, normalized preview geometry and
/// group-membership edges. Pure and side-effect-free; never touches any file on disk (persistence is a
/// separate concern -- see <see cref="ISpatialDisplayGraphStore"/>).
/// </summary>
public interface IDisplayGraphBuilder
{
    /// <summary>Equivalent to <see cref="Build(DisplayGraph, GeometryPolicy, IReadOnlyList{NodeAlias})"/> with no aliases.</summary>
    SpatialDisplayGraph Build(DisplayGraph imported, GeometryPolicy policy);

    /// <summary>
    /// Builds a graph, pinning any node whose current xLights name matches a supplied
    /// <see cref="NodeAlias.XlightsName"/> to that alias's own stable <see cref="NodeAlias.NodeId"/>
    /// instead of deriving a fresh id from the current name (Step 059's own "controlled alias
    /// resolution for changing xLights model names"). An additive overload beyond Section 3's bare
    /// two-parameter proposed signature; see <c>implementation-evidence/PHASE-09/STEP-059.md</c> §3.
    /// </summary>
    /// <exception cref="SpatialDisplayGraphInvalidException">
    /// <paramref name="policy"/> is unusable, <paramref name="aliases"/> is internally ambiguous, or the
    /// imported graph's own model/group names collide into an ambiguous stable node id.
    /// </exception>
    SpatialDisplayGraph Build(DisplayGraph imported, GeometryPolicy policy, IReadOnlyList<NodeAlias> aliases);
}
