namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 066: one source track's semantic role for one phrase, targeting one named spatial path.
/// Named <c>MusicalRoleAssignment</c> (not the bare <c>RoleAssignment</c> PHASE-10.md Section 3's own
/// proposed C# signature uses) to avoid a confusing same-shape-different-meaning collision with the
/// already-accepted <see cref="XlightsAI.Studio.Display.RoleAssignment"/> (a *prop's* artistic role,
/// Phase 09's own contract) now that this project references <c>XlightsAI.Studio.Display</c> directly --
/// disclosed as an architecture decision in <c>implementation-evidence/PHASE-10/STEP-066.md</c>.
///
/// <para>
/// Implementation guidance, verbatim: "Represent lead/accompaniment/accent and instrument track linkage
/// per phrase rather than hardcode one stem per display model" -- realized here as an explicit
/// <see cref="SourceTrackId"/> (an arbitrary caller-defined stem/track identity, not a display model id)
/// linked to a <see cref="MusicalRole"/> and a target <see cref="SpatialPathId"/> for one <see cref="PhraseId"/>
/// scope, so the same track can be re-linked to a different role/path in a different phrase.
/// </para>
/// </summary>
/// <param name="PhraseId">The caller-defined phrase/scope identity this assignment applies to.</param>
/// <param name="SourceTrackId">The arbitrary, caller-defined stem/instrument track identity (e.g. a <c>StemAsset</c>/<c>MelodyTrack</c> id) this role is assigned to. Never a <c>ModelId</c>.</param>
/// <param name="Role">The semantic musical role this track plays for <see cref="PhraseId"/>.</param>
/// <param name="SpatialPathId">The <c>SpatialPath.Id</c> this role's gestures should target.</param>
/// <param name="PrecedenceTier">One of <see cref="Composition.PrecedenceTier.All"/> -- this assignment's own event-precedence tier.</param>
/// <param name="Priority">A caller-assigned fine-grained priority, higher wins, used only to break ties within the same <see cref="PrecedenceTier"/>.</param>
/// <param name="Weight">A caller-assigned relative prominence weight (e.g. for budget/brightness allocation in a later step); carries no precedence meaning of its own.</param>
/// <param name="Version">Increments every time this exact (<see cref="SourceTrackId"/>, <see cref="PhraseId"/>) pair's role is reassigned.</param>
public sealed record MusicalRoleAssignment(
    Guid PhraseId,
    string SourceTrackId,
    MusicalRole Role,
    Guid SpatialPathId,
    string PrecedenceTier,
    int Priority,
    double Weight,
    int Version);
