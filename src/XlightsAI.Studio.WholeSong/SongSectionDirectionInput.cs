using XlightsAI.Studio.Composition;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 090 ("Create song-level creative direction with palettes, motif identities, energy and
/// focal points"): one section's own user-tunable creative-direction request, before cross-validation
/// against the song's already-accepted <c>SectionLabelAnalysis</c>/<c>MotifClusteringAnalysis</c>
/// (<see cref="ISongDirectionPlanner.Plan"/> performs that check and produces the validated
/// <see cref="SongSectionDirection"/>). Kept as a separate input shape -- rather than asking the caller to
/// construct a <see cref="SongSectionDirection"/> directly -- so an unvalidated UI/AI proposal can never be
/// mistaken for an already-accepted plan section, the same input/output split PHASE-08's own
/// <c>SectionLabelCandidate</c>/<c>SectionLabelAnalysis</c> pair already established.
/// </summary>
/// <param name="SectionId">The <c>SectionLabelCandidate.Id</c> this direction applies to -- must exist in the <see cref="ISongDirectionPlanner.Plan"/> call's own supplied section-label analysis.</param>
/// <param name="TargetEnergy">User-tunable target energy in <c>[0,1]</c> for this section (Step 090's own implementation guidance: "Define song-level direction and priorities").</param>
/// <param name="TargetContrast">User-tunable target contrast in <c>[0,1]</c> for this section relative to its neighbors -- Step 090's own implementation guidance, verbatim: "user-tunable target contrast."</param>
/// <param name="PaletteId">A non-blank palette identity linked to this section -- Step 090's own implementation guidance, verbatim: "link palette... to existing section IDs."</param>
/// <param name="FocalRole">The <see cref="MusicalRole"/> that should be the focal point of this section -- Step 090's own implementation guidance, verbatim: "link... focal role to existing section IDs." Reuses PHASE-10 Step 066's own already-accepted <see cref="MusicalRole"/> enum rather than inventing a parallel "focal role" concept.</param>
/// <param name="MotifGroupIds">Zero or more <c>MotifGroupCandidate.Id</c> values this section's own creative direction is linked to (motif identity) -- every id must exist in the <see cref="ISongDirectionPlanner.Plan"/> call's own supplied motif clustering analysis. Empty is valid: not every section carries a repeated motif.</param>
public sealed record SongSectionDirectionInput(
    Guid SectionId,
    double TargetEnergy,
    double TargetContrast,
    string PaletteId,
    MusicalRole FocalRole,
    IReadOnlyList<Guid> MotifGroupIds);
