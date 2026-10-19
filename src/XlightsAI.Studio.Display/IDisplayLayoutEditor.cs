using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 061's own application-orchestration boundary: validates and persists manual position
/// corrections and list-editor reorders against an already-built <see cref="SpatialDisplayGraph"/>.
/// Every method rejects an invalid input (unknown node id, out-of-range position, an incomplete/
/// duplicate/foreign reorder set) <em>before</em> loading or writing anything -- this step's own
/// required failure/edge case, verbatim: "Invalid model/format/version must fail safely without
/// rewriting source files."
/// </summary>
public interface IDisplayLayoutEditor
{
    /// <summary>Loads previously saved corrections for <paramref name="sourceLayoutHash"/>, or <see cref="DisplayLayoutCorrections.Empty"/> if none have ever been saved.</summary>
    Task<DisplayLayoutCorrections> GetCorrectionsAsync(string sourceLayoutHash, CancellationToken ct);

    /// <summary>
    /// Records a manual position correction ("drag") for <paramref name="nodeId"/> on top of whatever
    /// corrections already exist for <paramref name="graph"/>'s own <see cref="SpatialDisplayGraph.SourceLayoutHash"/>.
    /// </summary>
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="nodeId"/> does not exist in <paramref name="graph"/>, or <paramref name="newX"/>/<paramref name="newY"/> is not a finite value in <c>[0, 1]</c>.</exception>
    Task<DisplayLayoutCorrections> CorrectPositionAsync(SpatialDisplayGraph graph, ModelId nodeId, double newX, double newY, CancellationToken ct);

    /// <summary>
    /// Records a manual list-editor reorder ("reorder") on top of whatever corrections already exist for
    /// <paramref name="graph"/>'s own <see cref="SpatialDisplayGraph.SourceLayoutHash"/>.
    /// </summary>
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="newOrder"/> is not an exact permutation of <paramref name="graph"/>'s own node ids (missing, duplicate or unknown id).</exception>
    Task<DisplayLayoutCorrections> ReorderAsync(SpatialDisplayGraph graph, IReadOnlyList<ModelId> newOrder, CancellationToken ct);
}
