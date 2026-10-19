namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 060: infers neighbor/left-right/center/vertical relations from an already-built
/// <see cref="SpatialDisplayGraph"/>'s own normalized node geometry, surfacing a confidence rather than
/// asserting physical truth (Section 3: "surface confidence rather than assert physical truth"). Pure and
/// side-effect-free; never touches any file on disk or mutates <paramref name="graph"/>.
/// </summary>
public interface ISpatialRelationInferrer
{
    /// <exception cref="ArgumentNullException"><paramref name="graph"/> or <paramref name="policy"/> is null.</exception>
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="policy"/> is unusable (<see cref="GeometryPolicy.MaxNeighborDistance"/> is negative).</exception>
    SpatialRelationInference Infer(SpatialDisplayGraph graph, GeometryPolicy policy);
}
