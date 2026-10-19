namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 062: the full persisted set of named <see cref="SpatialPath"/>s for one imported layout
/// (keyed by <see cref="SourceLayoutHash"/>, the same layout-scoped identity <see cref="ISpatialDisplayGraphStore"/>/
/// <see cref="IDisplayLayoutCorrectionsStore"/> already use). Deliberately its own persisted aggregate,
/// not a field written directly onto <see cref="SpatialDisplayGraph.Paths"/> on disk -- a
/// <see cref="SpatialDisplayGraph"/> stays <see cref="IDisplayGraphBuilder"/>'s own rebuildable, never-
/// persisted-by-that-builder projection (Step 059's own already-accepted contract); <see cref="ISpatialPathCatalog"/>
/// loads a <see cref="SpatialPathSet"/> and a caller attaches its <see cref="Paths"/> onto an already-built
/// graph (<c>graph with { Paths = set.Paths }</c>) wherever it needs to be rendered or resolved.
/// </summary>
/// <param name="SourceLayoutHash">The exact layout snapshot these paths were authored against.</param>
/// <param name="Paths">The current, live version of every named path -- at most one entry per <see cref="SpatialPath.Id"/>.</param>
/// <param name="History">
/// Every superseded version of every path (this step's own implementation guidance: "path history is
/// versioned") -- appended to, never overwritten or trimmed, each time <see cref="ISpatialPathCatalog.CreateAsync"/>
/// replaces an existing path or <see cref="ISpatialPathCatalog.ReorderAsync"/>/<see cref="ISpatialPathCatalog.ReverseAsync"/>
/// supersedes the version currently in <see cref="Paths"/>.
/// </param>
/// <param name="Version">Increments on every accepted edit to this set; <c>0</c> means nothing has ever been saved for this layout.</param>
public sealed record SpatialPathSet(
    string SourceLayoutHash,
    IReadOnlyList<SpatialPath> Paths,
    IReadOnlyList<SpatialPath> History,
    int Version)
{
    /// <summary>An empty path set for <paramref name="sourceLayoutHash"/> -- no named paths, no history, <see cref="Version"/> 0.</summary>
    public static SpatialPathSet Empty(string sourceLayoutHash) => new(sourceLayoutHash, [], [], 0);
}
