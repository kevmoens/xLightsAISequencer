using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="IRoleAssignmentCatalog"/>. See the interface and <c>implementation-evidence/PHASE-09/STEP-063.md</c> for the full rationale.</summary>
public sealed class RoleAssignmentCatalog : IRoleAssignmentCatalog
{
    private readonly IRoleAssignmentStore _store;

    public RoleAssignmentCatalog(IRoleAssignmentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<RoleAssignmentSet> GetAssignmentsAsync(string sourceLayoutHash, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);

        var path = _store.GetPath(sourceLayoutHash);
        if (!File.Exists(path))
        {
            return RoleAssignmentSet.Empty(sourceLayoutHash);
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<RoleAssignmentSet> AssignAsync(SpatialDisplayGraph graph, ModelId nodeId, string role, Guid scopeId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(role);

        if (!graph.Nodes.Any(n => n.Id == nodeId))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Node '{nodeId.Value}' does not exist in the layout with source hash '{graph.SourceLayoutHash}'.");
        }

        if (!RoleTag.All.Contains(role, StringComparer.Ordinal))
        {
            throw new SpatialDisplayGraphInvalidException(
                $"Unknown role tag '{role}'; expected one of: {string.Join(", ", RoleTag.All)}.");
        }

        var current = await GetAssignmentsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        var existing = current.Assignments.FirstOrDefault(a => a.NodeId == nodeId && a.ScopeId == scopeId);

        ct.ThrowIfCancellationRequested();

        var replacement = new RoleAssignment(nodeId, role, scopeId, existing is null ? 0 : existing.Version + 1);

        List<RoleAssignment> newAssignments;
        List<RoleAssignment> newHistory;
        if (existing is null)
        {
            newAssignments = [.. current.Assignments, replacement];
            newHistory = [.. current.History];
        }
        else
        {
            newAssignments = current.Assignments.Select(a => a.NodeId == nodeId && a.ScopeId == scopeId ? replacement : a).ToList();
            newHistory = [.. current.History, existing];
        }

        var updated = current with { Assignments = newAssignments, History = newHistory, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    public async Task<RoleAssignmentSet> ClearAsync(SpatialDisplayGraph graph, ModelId nodeId, Guid scopeId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var current = await GetAssignmentsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        var existing = current.Assignments.FirstOrDefault(a => a.NodeId == nodeId && a.ScopeId == scopeId) ??
            throw new SpatialDisplayGraphInvalidException(
                $"No role assignment exists for node '{nodeId.Value}' and scope '{scopeId}'.");

        ct.ThrowIfCancellationRequested();

        var newAssignments = current.Assignments.Where(a => !(a.NodeId == nodeId && a.ScopeId == scopeId)).ToList();
        var newHistory = new List<RoleAssignment>(current.History) { existing };
        var updated = current with { Assignments = newAssignments, History = newHistory, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }
}
