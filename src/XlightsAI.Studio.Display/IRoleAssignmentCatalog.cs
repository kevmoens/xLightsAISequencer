using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 063's own application-orchestration boundary: assigns, persists, versions and clears
/// named <see cref="RoleTag"/> assignments for an already-built <see cref="SpatialDisplayGraph"/>, entirely
/// independent of any <see cref="SpatialPath"/>. Every method rejects an invalid input (unknown node id,
/// unrecognized role tag, a clear against a pair with no live assignment) <em>before</em> loading or
/// writing anything -- this step's own required failure/edge case, verbatim: "Invalid input, cancellation
/// or stale persisted state must not corrupt the project."
///
/// <para>
/// Conflict policy (this step's own required implementation guidance: "provide conflict policy"): at most
/// one role is live for a given (<see cref="RoleAssignment.NodeId"/>, <see cref="RoleAssignment.ScopeId"/>)
/// pair at a time. Assigning a new role to a pair that already has one does not stack silently -- it
/// visibly replaces the prior assignment, moving it into <see cref="RoleAssignmentSet.History"/> and
/// incrementing that pair's own <see cref="RoleAssignment.Version"/>. A different <see cref="RoleAssignment.ScopeId"/>
/// for the same node is an entirely independent pair, which is exactly how "same prop can play lead in one
/// phrase and background later" (implementation guidance, verbatim) is realized.
/// </para>
/// </summary>
public interface IRoleAssignmentCatalog
{
    /// <summary>Loads previously saved role assignments for <paramref name="sourceLayoutHash"/>, or <see cref="RoleAssignmentSet.Empty"/> if none have ever been saved.</summary>
    Task<RoleAssignmentSet> GetAssignmentsAsync(string sourceLayoutHash, CancellationToken ct);

    /// <summary>
    /// Assigns <paramref name="role"/> to <paramref name="nodeId"/> for <paramref name="scopeId"/>. When no
    /// assignment exists yet for this exact pair, it is added at <see cref="RoleAssignment.Version"/> <c>0</c>.
    /// When one already exists, per this interface's own conflict policy it is replaced: moved into
    /// <see cref="RoleAssignmentSet.History"/> and the new role saved at <c>Version + 1</c>.
    /// </summary>
    /// <exception cref="SpatialDisplayGraphInvalidException"><paramref name="nodeId"/> does not exist in <paramref name="graph"/>, or <paramref name="role"/> is not one of <see cref="RoleTag.All"/>.</exception>
    Task<RoleAssignmentSet> AssignAsync(SpatialDisplayGraph graph, ModelId nodeId, string role, Guid scopeId, CancellationToken ct);

    /// <summary>Removes the live role assignment for (<paramref name="nodeId"/>, <paramref name="scopeId"/>), moving it into <see cref="RoleAssignmentSet.History"/>.</summary>
    /// <exception cref="SpatialDisplayGraphInvalidException">No live assignment exists for this exact (<paramref name="nodeId"/>, <paramref name="scopeId"/>) pair.</exception>
    Task<RoleAssignmentSet> ClearAsync(SpatialDisplayGraph graph, ModelId nodeId, Guid scopeId, CancellationToken ct);
}
