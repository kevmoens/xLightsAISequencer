using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 061: the full set of user-authored, persisted corrections for one imported layout
/// (keyed by <see cref="SourceLayoutHash"/>, the same layout-scoped identity
/// <see cref="ISpatialDisplayGraphStore"/> already uses) -- manual position corrections
/// (<see cref="PositionOverrides"/>) plus the list editor's own drag-reordered row order
/// (<see cref="DisplayOrder"/>). Deliberately its own aggregate, not a field on
/// <see cref="SpatialDisplayGraph"/> itself: a <see cref="SpatialDisplayGraph"/> is a rebuildable
/// projection of the imported layout (Step 059), while this is user-authored state that must survive a
/// rebuild unchanged (Section 4: "never silently reorder a saved path when source coordinates change").
/// </summary>
/// <param name="SourceLayoutHash">The exact layout snapshot these corrections apply to.</param>
/// <param name="PositionOverrides">At most one entry per <see cref="NodePositionOverride.NodeId"/> (enforced by <see cref="DisplayLayoutEditor"/>, never by this record itself).</param>
/// <param name="DisplayOrder">
/// The list editor's own user-chosen row order -- deliberately NOT a <see cref="SpatialPath"/> (Step
/// 062's own later, artistic-route scope; <see cref="SpatialDisplayGraph.Paths"/> stays untouched by
/// this step). Empty means "no manual reorder yet; use the graph's own node order."
/// </param>
/// <param name="Version">Increments on every accepted edit; exists so a caller can detect "nothing persisted yet" (<c>Version == 0</c>) without a separate empty/default sentinel.</param>
public sealed record DisplayLayoutCorrections(
    string SourceLayoutHash,
    IReadOnlyList<NodePositionOverride> PositionOverrides,
    IReadOnlyList<ModelId> DisplayOrder,
    int Version)
{
    /// <summary>An empty correction set for <paramref name="sourceLayoutHash"/> -- no overrides, no manual reorder, <see cref="Version"/> 0.</summary>
    public static DisplayLayoutCorrections Empty(string sourceLayoutHash) => new(sourceLayoutHash, [], [], 0);
}
