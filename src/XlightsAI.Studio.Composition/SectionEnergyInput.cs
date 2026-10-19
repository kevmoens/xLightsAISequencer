using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 069 ("Build sectional energy and color/brightness story"): one already-classified song
/// section's raw musical energy, the input <see cref="ISectionVisualStoryPlanner.Plan"/> normalizes into a
/// <see cref="VisualStory"/>. Reuses <see cref="SectionKind"/> directly (Step 054's own already-accepted
/// contract from Phase 08) rather than duplicating a parallel "section type" concept -- the same
/// reuse-over-duplication precedent Step 068's own <see cref="VisualBudgetCandidate"/> already established
/// for <see cref="MusicalRoleAssignment"/>.
/// </summary>
/// <param name="SectionId">Stable identity for this section -- the already-accepted <see cref="SectionLabelCandidate.Id"/> (Phase 08's own "Section 3's own <c>SectionId</c>"), reused verbatim rather than introducing a second id type for the same concept.</param>
/// <param name="Kind">This section's already-classified label (<see cref="SectionKind.Unknown"/> is a valid input, exactly as Phase 08 established).</param>
/// <param name="RawEnergy">A non-negative, finite, caller-supplied energy proxy for this section (e.g. mean stem activity/loudness) on an arbitrary scale -- only the relative ordering across sections in the same <see cref="VisualStory"/> matters, never an absolute unit.</param>
public sealed record SectionEnergyInput(Guid SectionId, SectionKind Kind, double RawEnergy);
