using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 063: one prop's role for one choreography scope. Deliberately independent of
/// <see cref="SpatialPath"/> -- a role assignment names no path at all (implementation guidance: "Assign
/// roles independently of paths so same prop can play lead in one phrase and background later").
/// </summary>
/// <param name="NodeId">The <see cref="DisplayNode"/> this role is assigned to.</param>
/// <param name="Role">One of <see cref="RoleTag.All"/>.</param>
/// <param name="ScopeId">
/// The caller-defined choreography scope this assignment applies to -- e.g. one phrase's identity from a
/// later phase's own phrase segmentation. <see cref="Guid.Empty"/> is the default, phrase-independent
/// scope ("this prop's role whenever no more specific scope overrides it"). Deliberately an opaque
/// <see cref="Guid"/>, not a reference to Phase 08's own phrase contracts -- this project's own
/// <c>XlightsAI.Studio.Display</c> project has no dependency on <c>XlightsAI.Studio.Structure</c>, and this
/// step's own scope is role persistence/conflict policy, not phrase identity itself.
/// </param>
/// <param name="Version">Increments every time this exact <see cref="NodeId"/>/<see cref="ScopeId"/> pair's role is reassigned.</param>
public sealed record RoleAssignment(ModelId NodeId, string Role, Guid ScopeId, int Version);
