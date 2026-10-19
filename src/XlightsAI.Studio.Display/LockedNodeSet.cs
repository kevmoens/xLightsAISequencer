using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065's own implementation guidance, verbatim: "Validate missing/locked prop..." -- the full
/// persisted set of explicitly locked/excluded nodes for one imported layout (keyed by
/// <see cref="SourceLayoutHash"/>, the same layout-scoped identity <see cref="ISpatialPathStore"/>/
/// <see cref="IRoleAssignmentStore"/> already use). Deliberately separate from <see cref="ResolutionSettings.SkipUnavailable"/>
/// (Step 064's own disclosed scope boundary): that flag only ever covers "the node no longer exists in the
/// graph at all"; this set covers "the node still exists, but a user has explicitly excluded it from route
/// targeting" (Section 2: "locked/excluded props").
/// </summary>
/// <param name="SourceLayoutHash">The exact layout snapshot these locks were authored against.</param>
/// <param name="LockedNodeIds">Every node id currently locked/excluded; at most one entry per node.</param>
/// <param name="Version">Increments on every accepted edit to this set; <c>0</c> means nothing has ever been saved for this layout.</param>
public sealed record LockedNodeSet(
    string SourceLayoutHash,
    IReadOnlyList<ModelId> LockedNodeIds,
    int Version)
{
    /// <summary>An empty locked-node set for <paramref name="sourceLayoutHash"/> -- no node locked, <see cref="Version"/> 0.</summary>
    public static LockedNodeSet Empty(string sourceLayoutHash) => new(sourceLayoutHash, [], 0);
}
