using XlightsAI.Studio.Display;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 066's own application-orchestration boundary: assigns, persists, versions and clears
/// <see cref="MusicalRoleAssignment"/>s for one phrase. Every method rejects an invalid input (blank track
/// id, undefined <see cref="MusicalRole"/>, unrecognized <see cref="Composition.PrecedenceTier"/>, a
/// <see cref="MusicalRoleAssignment.SpatialPathId"/> absent from the supplied path set -- or an
/// unknown-pair clear) <em>before</em> loading or writing anything -- this step's
/// own required failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must
/// not corrupt the project."
///
/// <para>
/// Conflict policy (mirrors <see cref="IRoleAssignmentCatalog"/>'s own established policy exactly): at most
/// one role is live for a given (<see cref="MusicalRoleAssignment.SourceTrackId"/>, phrase) pair at a time.
/// Assigning a new role to a track that already has one for this phrase does not stack -- it visibly
/// replaces the prior assignment, moving it into <see cref="MusicalRoleAssignmentSet.History"/> and
/// incrementing that pair's own <see cref="MusicalRoleAssignment.Version"/>.
/// </para>
/// </summary>
public interface IMusicalRoleAssignmentCatalog
{
    /// <summary>Loads previously saved role assignments for <paramref name="phraseId"/>, or <see cref="MusicalRoleAssignmentSet.Empty"/> if none have ever been saved.</summary>
    Task<MusicalRoleAssignmentSet> GetAssignmentsAsync(Guid phraseId, CancellationToken ct);

    /// <summary>
    /// Assigns <paramref name="role"/> to <paramref name="sourceTrackId"/> for <paramref name="phraseId"/>,
    /// targeting <paramref name="spatialPathId"/> at <paramref name="precedenceTier"/>/<paramref name="priority"/>/<paramref name="weight"/>.
    /// </summary>
    /// <exception cref="CompositionInvalidException">
    /// <paramref name="sourceTrackId"/> is blank, <paramref name="role"/> is not a defined <see cref="MusicalRole"/>,
    /// <paramref name="precedenceTier"/> is not one of <see cref="Composition.PrecedenceTier.All"/>, or
    /// <paramref name="spatialPathId"/> does not exist in <paramref name="pathSet"/>.
    /// </exception>
    Task<MusicalRoleAssignmentSet> AssignAsync(
        SpatialPathSet pathSet,
        Guid phraseId,
        string sourceTrackId,
        MusicalRole role,
        Guid spatialPathId,
        string precedenceTier,
        int priority,
        double weight,
        CancellationToken ct);

    /// <summary>Removes the live role assignment for (<paramref name="sourceTrackId"/>, <paramref name="phraseId"/>), moving it into <see cref="MusicalRoleAssignmentSet.History"/>.</summary>
    /// <exception cref="CompositionInvalidException">No live assignment exists for this exact (<paramref name="sourceTrackId"/>, <paramref name="phraseId"/>) pair.</exception>
    Task<MusicalRoleAssignmentSet> ClearAsync(Guid phraseId, string sourceTrackId, CancellationToken ct);
}
