namespace XlightsAI.Studio.Shell;

/// <summary>
/// One routing warning surfaced by <see cref="RoleRoutingEditorViewModel.Warnings"/> for SCREEN 12
/// CHOREOGRAPHY's routing table (PHASE-10 Step 067's own implementation guidance, verbatim: "... with role
/// priority and empty/unavailable warnings"). Never itself a validation failure -- the routing table stays
/// fully editable while warnings are shown; a warning only means the currently saved routing is
/// incomplete or stale, not corrupt or rejected.
/// </summary>
/// <param name="Kind">One of <see cref="RoleRoutingWarningKind"/>.</param>
/// <param name="Message">A human-readable description of the specific gap.</param>
public sealed record RoleRoutingWarningView(RoleRoutingWarningKind Kind, string Message);

/// <summary>The specific kind of gap a <see cref="RoleRoutingWarningView"/> reports.</summary>
public enum RoleRoutingWarningKind
{
    /// <summary>No source track is currently routed to this <see cref="Composition.MusicalRole"/> for the open section.</summary>
    UnmappedRole,

    /// <summary>A saved routing's <see cref="Composition.MusicalRoleAssignment.SourceTrackId"/> is not in the currently supplied set of available instrument/stem tracks.</summary>
    UnavailableSource,

    /// <summary>A saved routing's <see cref="Composition.MusicalRoleAssignment.SpatialPathId"/> no longer exists in the currently open layout's named path set.</summary>
    UnavailablePath,
}
