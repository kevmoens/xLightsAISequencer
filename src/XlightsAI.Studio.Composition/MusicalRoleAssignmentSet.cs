namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 066: the full persisted set of <see cref="MusicalRoleAssignment"/>s for one phrase (keyed
/// by <see cref="PhraseId"/>), mirroring <see cref="XlightsAI.Studio.Display.RoleAssignmentSet"/>'s own
/// shape exactly (live assignments plus an append-only, never-trimmed <see cref="History"/>).
/// </summary>
/// <param name="PhraseId">The exact phrase/scope these assignments were authored against.</param>
/// <param name="Assignments">The current, live role for every <see cref="MusicalRoleAssignment.SourceTrackId"/> that has ever been assigned in this phrase -- at most one entry per track.</param>
/// <param name="History">Every superseded assignment for a track in this phrase -- appended to, never overwritten or trimmed.</param>
/// <param name="Version">Increments on every accepted edit to this set; <c>0</c> means nothing has ever been saved for this phrase.</param>
public sealed record MusicalRoleAssignmentSet(
    Guid PhraseId,
    IReadOnlyList<MusicalRoleAssignment> Assignments,
    IReadOnlyList<MusicalRoleAssignment> History,
    int Version)
{
    /// <summary>An empty assignment set for <paramref name="phraseId"/> -- no assignments, no history, <see cref="Version"/> 0.</summary>
    public static MusicalRoleAssignmentSet Empty(Guid phraseId) => new(phraseId, [], [], 0);
}
