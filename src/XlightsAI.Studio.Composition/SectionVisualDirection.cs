using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 069: one section's own planned energy/color story -- a deliberately narrower realization of
/// PHASE-10.md Section 3's own proposed <c>SectionVisualDirection(Guid SectionId, string PaletteId, double
/// Intensity, ModelId? FocalProp, IReadOnlyList&lt;RoleAssignment&gt; Roles)</c> signature. <see cref="FocalProp"/>
/// and <c>Roles</c> are deliberately omitted here -- disclosed as an architecture decision in
/// <c>implementation-evidence/PHASE-10/STEP-069.md</c>, the same reasoning Step 068's own
/// <see cref="VisualBudgetCandidate"/> ADR already used for <c>PlannedGesture</c>: a focal *prop*/role
/// targeting is Step 070/071's own later, disclosed scope (build/transition/collision recipes targeting
/// concrete models), not this step's own narrow "normalize section intensity and assign a palette" scope.
/// </summary>
/// <param name="SectionId">The <see cref="SectionEnergyInput.SectionId"/> this direction was planned for.</param>
/// <param name="Kind">The section's own already-classified label, carried through unchanged from the input.</param>
/// <param name="PaletteId">A deterministic palette identity derived from <see cref="Kind"/> (see <see cref="SectionVisualStoryPlanner"/>) -- never a free-form or randomly chosen value.</param>
/// <param name="Intensity">The normalized, hierarchy-preserving intensity in <c>[0,1]</c> this section should be rendered at, relative to every other section in the same <see cref="VisualStory"/>.</param>
public sealed record SectionVisualDirection(Guid SectionId, SectionKind Kind, string PaletteId, double Intensity);
