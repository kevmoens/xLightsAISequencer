namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 063: the full persisted set of role assignments for one imported layout (keyed by
/// <see cref="SourceLayoutHash"/>, the same layout-scoped identity <see cref="ISpatialPathStore"/>/
/// <see cref="ISpatialDisplayGraphStore"/>/<see cref="IDisplayLayoutCorrectionsStore"/> already use). Its
/// own separate persisted aggregate, at its own separate on-disk path, so saving/rebuilding role
/// assignments never touches the graph cache, the layout corrections or the named path set.
/// </summary>
/// <param name="SourceLayoutHash">The exact layout snapshot these role assignments were authored against.</param>
/// <param name="Assignments">The current, live role for every (<see cref="RoleAssignment.NodeId"/>, <see cref="RoleAssignment.ScopeId"/>) pair that has ever been assigned -- at most one entry per pair.</param>
/// <param name="History">
/// Every superseded assignment for a (<see cref="RoleAssignment.NodeId"/>, <see cref="RoleAssignment.ScopeId"/>)
/// pair -- appended to, never overwritten or trimmed, each time <see cref="IRoleAssignmentCatalog.AssignAsync"/>
/// replaces an existing assignment or <see cref="IRoleAssignmentCatalog.ClearAsync"/> removes one.
/// </param>
/// <param name="Version">Increments on every accepted edit to this set; <c>0</c> means nothing has ever been saved for this layout.</param>
public sealed record RoleAssignmentSet(
    string SourceLayoutHash,
    IReadOnlyList<RoleAssignment> Assignments,
    IReadOnlyList<RoleAssignment> History,
    int Version)
{
    /// <summary>An empty role assignment set for <paramref name="sourceLayoutHash"/> -- no assignments, no history, <see cref="Version"/> 0.</summary>
    public static RoleAssignmentSet Empty(string sourceLayoutHash) => new(sourceLayoutHash, [], [], 0);
}
