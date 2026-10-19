namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3 proposed contract: the full, versioned, rebuildable projection of one imported
/// <c>DisplayGraph</c> into spatial-choreography terms -- stable node ids, normalized preview geometry,
/// group-membership edges and (from Step 062 onward) user-authored paths. Kept as its own aggregate,
/// separate from the imported <c>DisplayGraph</c> itself (Section 2's cross-phase ownership table:
/// "Artistic routes / normalized geometry separate from imported xLights identities").
/// </summary>
/// <param name="SourceLayoutHash">The exact <c>DisplayGraph.SourceLayoutHash</c> this graph was built from -- identifies which on-disk layout snapshot produced it, without re-reading the source file.</param>
/// <param name="Nodes">Every model and group node, in the same order <see cref="XlightsAI.Studio.Xlights.DisplayGraph.Models"/>/<see cref="XlightsAI.Studio.Xlights.DisplayGraph.Groups"/> themselves were enumerated in.</param>
/// <param name="Edges">Every relationship this step produces (Step 059: group-membership edges only).</param>
/// <param name="Paths">User-authored ordered routes; always empty before Step 062.</param>
/// <param name="VersionId">
/// Identity of this particular build run -- changes every time <see cref="IDisplayGraphBuilder.Build"/>
/// is called, even if the resulting nodes/edges are numerically identical (the same provenance
/// convention Phase 08's own <c>SongFingerprintSet.VersionId</c> already established). An additive field
/// beyond Section 3's bare four-field proposed signature; see
/// <c>implementation-evidence/PHASE-09/STEP-059.md</c> §3 for the disclosed rationale.
/// </param>
public sealed record SpatialDisplayGraph(string SourceLayoutHash,
    IReadOnlyList<DisplayNode> Nodes, IReadOnlyList<SpatialEdge> Edges,
    IReadOnlyList<SpatialPath> Paths, Guid VersionId);
