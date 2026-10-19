using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 090: one section's own validated creative direction -- a narrower, explicitly-scoped
/// realization of PHASE-14.md Section 3's own proposed
/// <c>SongSectionPlan(Guid SectionId, double TargetEnergy, string PaletteId, IReadOnlyList&lt;Guid&gt;
/// PhrasePlanIds)</c>. <c>PhrasePlanIds</c> is deliberately omitted here -- disclosed in
/// <c>implementation-evidence/PHASE-14/STEP-090.md</c> -- because assembling/ordering accepted phrase plans
/// is Step 091's own later, disclosed scope ("Assemble accepted phrase candidates..."), not this step's own
/// narrow "song-level direction: energy, contrast, palette, focal role, motif identity" scope. This type adds
/// <see cref="TargetContrast"/> and <see cref="FocalRole"/>, neither of which the proposed signature names,
/// because this step's own required acceptance explicitly asks for "user-tunable target contrast" and a
/// "focal role" linked to the section -- both absent from the Section-3 proposal entirely.
/// </summary>
/// <param name="SectionId">The already-accepted <c>SectionLabelCandidate.Id</c> this direction was validated against.</param>
/// <param name="Kind">The section's own already-classified label, carried through unchanged from the source <c>SectionLabelAnalysis</c> -- never re-derived here.</param>
/// <param name="TargetEnergy">Validated target energy in <c>[0,1]</c>.</param>
/// <param name="TargetContrast">Validated target contrast in <c>[0,1]</c>.</param>
/// <param name="PaletteId">Validated non-blank palette id.</param>
/// <param name="FocalRole">The <see cref="MusicalRole"/> that is the focal point of this section.</param>
/// <param name="MotifGroupIds">Every <c>MotifGroupCandidate.Id</c> this section is linked to, validated to exist in the source motif clustering analysis. Ordered exactly as supplied.</param>
public sealed record SongSectionDirection(
    Guid SectionId,
    SectionKind Kind,
    double TargetEnergy,
    double TargetContrast,
    string PaletteId,
    MusicalRole FocalRole,
    IReadOnlyList<Guid> MotifGroupIds);
