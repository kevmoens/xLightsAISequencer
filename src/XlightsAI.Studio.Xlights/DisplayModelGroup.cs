namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 011: one <c>&lt;modelGroup&gt;</c> as its own targetable entity (a group is itself a
/// valid effect target in xLights, distinct from its members). <see cref="Members"/> is the group's
/// own declared member list, in declaration order, each classified by <see cref="DisplayGroupMember"/>
/// — never expanded/flattened into a resolved model list (PHASE-02 Section 3: "Groups may overlap; do
/// not flatten user group membership"; this applies equally to nested groups).
/// </summary>
public sealed record DisplayModelGroup(string Name, IReadOnlyList<DisplayGroupMember> Members);
