using XlightsAI.Studio.Display;

namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="IMusicalRoleAssignmentCatalog"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-066.md</c> for the full rationale.</summary>
public sealed class MusicalRoleAssignmentCatalog : IMusicalRoleAssignmentCatalog
{
    private readonly IMusicalRoleAssignmentStore _store;

    public MusicalRoleAssignmentCatalog(IMusicalRoleAssignmentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<MusicalRoleAssignmentSet> GetAssignmentsAsync(Guid phraseId, CancellationToken ct)
    {
        var path = _store.GetPath(phraseId);
        if (!File.Exists(path))
        {
            return MusicalRoleAssignmentSet.Empty(phraseId);
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<MusicalRoleAssignmentSet> AssignAsync(
        SpatialPathSet pathSet,
        Guid phraseId,
        string sourceTrackId,
        MusicalRole role,
        Guid spatialPathId,
        string precedenceTier,
        int priority,
        double weight,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pathSet);

        if (string.IsNullOrWhiteSpace(sourceTrackId))
        {
            throw new CompositionInvalidException("Source track id must not be blank.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new CompositionInvalidException($"Unknown musical role '{role}'.");
        }

        if (precedenceTier is null || !PrecedenceTier.All.Contains(precedenceTier, StringComparer.Ordinal))
        {
            throw new CompositionInvalidException(
                $"Unknown precedence tier '{precedenceTier}'; expected one of: {string.Join(", ", PrecedenceTier.All)}.");
        }

        if (!pathSet.Paths.Any(p => p.Id == spatialPathId))
        {
            throw new CompositionInvalidException(
                $"Spatial path '{spatialPathId}' does not exist in the supplied path set (source layout '{pathSet.SourceLayoutHash}').");
        }

        var current = await GetAssignmentsAsync(phraseId, ct).ConfigureAwait(false);
        var existing = current.Assignments.FirstOrDefault(a => a.SourceTrackId == sourceTrackId);

        ct.ThrowIfCancellationRequested();

        var replacement = new MusicalRoleAssignment(
            phraseId, sourceTrackId, role, spatialPathId, precedenceTier, priority, weight,
            existing is null ? 0 : existing.Version + 1);

        List<MusicalRoleAssignment> newAssignments;
        List<MusicalRoleAssignment> newHistory;
        if (existing is null)
        {
            newAssignments = [.. current.Assignments, replacement];
            newHistory = [.. current.History];
        }
        else
        {
            newAssignments = current.Assignments.Select(a => a.SourceTrackId == sourceTrackId ? replacement : a).ToList();
            newHistory = [.. current.History, existing];
        }

        var updated = current with { Assignments = newAssignments, History = newHistory, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    public async Task<MusicalRoleAssignmentSet> ClearAsync(Guid phraseId, string sourceTrackId, CancellationToken ct)
    {
        var current = await GetAssignmentsAsync(phraseId, ct).ConfigureAwait(false);
        var existing = current.Assignments.FirstOrDefault(a => a.SourceTrackId == sourceTrackId) ??
            throw new CompositionInvalidException(
                $"No role assignment exists for track '{sourceTrackId}' in phrase '{phraseId}'.");

        ct.ThrowIfCancellationRequested();

        var newAssignments = current.Assignments.Where(a => a.SourceTrackId != sourceTrackId).ToList();
        var newHistory = new List<MusicalRoleAssignment>(current.History) { existing };
        var updated = current with { Assignments = newAssignments, History = newHistory, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }
}
