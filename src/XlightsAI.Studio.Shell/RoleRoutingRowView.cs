using XlightsAI.Studio.Composition;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of one live <see cref="MusicalRoleAssignment"/> for SCREEN 12 CHOREOGRAPHY's
/// instrument-to-role / role-to-prop routing table (PHASE-10 Step 067). Same "Avalonia-free Shell layer
/// projects pure domain data into something directly bindable" convention
/// <see cref="DisplayNodeRowView"/>/<see cref="PianoRollNoteRowView"/> already established.
/// </summary>
/// <param name="SourceTrackId">The instrument/stem track this row routes.</param>
/// <param name="Role">The semantic role this track plays for the currently open section.</param>
/// <param name="SpatialPathId">The target named path id this role's gestures should route to.</param>
/// <param name="SpatialPathName">
/// The resolved display name of <see cref="SpatialPathId"/> within the currently open layout's path set, or
/// <see langword="null"/> when the path no longer exists (a stale routing left behind by a deleted path --
/// see <see cref="IsPathUnavailable"/>).
/// </param>
/// <param name="PrecedenceTier">This row's own event-precedence tier.</param>
/// <param name="Priority">This row's own fine-grained priority (higher wins within the same tier).</param>
/// <param name="Weight">This row's own relative prominence weight.</param>
/// <param name="IsSourceUnavailable">
/// <see langword="true"/> when <see cref="SourceTrackId"/> is not present in the currently supplied set of
/// available instrument/stem tracks (e.g. the stem was removed or renamed after this routing was saved).
/// </param>
/// <param name="IsPathUnavailable">
/// <see langword="true"/> when <see cref="SpatialPathId"/> no longer matches any entry in the currently open
/// layout's named path set (e.g. the path was deleted after this routing was saved).
/// </param>
/// <remarks>
/// Deliberately carries no command of its own (unlike <see cref="DisplayNodeRowView.SelectCommand"/>, which
/// needs no catalog to mutate local selection state): clearing a routing requires
/// <see cref="RoleRoutingEditorViewModel.ClearAsync"/>'s own catalog parameter, so a real screen calls it
/// directly with <see cref="SourceTrackId"/>, the same way <see cref="DisplayPathEditorViewModel.ClearRoleAsync"/>
/// is already called directly rather than through a row-captured command.
/// </remarks>
public sealed record RoleRoutingRowView(
    string SourceTrackId,
    MusicalRole Role,
    Guid SpatialPathId,
    string? SpatialPathName,
    string PrecedenceTier,
    int Priority,
    double Weight,
    bool IsSourceUnavailable,
    bool IsPathUnavailable);
