using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.WholeSong;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Deterministically assigns every real prop in the show layout (<see cref="DisplayGraph.Models"/> —
/// already how <see cref="ThreeEffectPlanFactory"/> itself resolves prop names today) a
/// <see cref="MusicalRoleAssignment"/> per phrase, replacing <c>ShowImportViewModel.EffectTargetNamesCsv</c>'s
/// manual 3-prop text entry. Builds a plain, un-persisted <see cref="SpatialPath"/> per prop directly (a
/// single-node route naming just that one prop) rather than going through
/// <see cref="IDisplayGraphBuilder"/>/<see cref="ISpatialPathSuggester"/>'s real-geometry-aware routing or
/// <see cref="IMusicalRoleAssignmentCatalog"/>'s versioned persistence — both are real, valuable features
/// for a human to later edit multi-prop spatial routes and review/re-assign roles through the UI, but
/// neither is required to produce a first automatic generation, and persisting through the catalog here
/// would misrepresent a generator's own output as a reviewed human decision (see
/// <see cref="Composition.PrecedenceTier.Automated"/>'s own doc comment: "a newly generated, not-yet-
/// reviewed automated candidate").
///
/// Deterministic heuristic (same philosophy as <see cref="DeterministicSectionDirectionPlanner"/>, per the
/// user's own confirmed decision): each phrase's <see cref="SongSectionDirectionInput.FocalRole"/> (from
/// the phrase's own section) goes to one rotating prop per phrase (round-robin by prop index, keyed by the
/// phrase's own position in the song) so every prop eventually takes the spotlight; every other prop gets
/// a supporting role, rotated the same way, so no two props are stuck doing the same thing for the whole
/// song.
/// </summary>
public static class DeterministicPropRoleAssigner
{
    private static readonly MusicalRole[] AllRoles = Enum.GetValues<MusicalRole>();

    public static IReadOnlyList<MusicalRoleAssignment> AssignRoles(
        DisplayGraph layout, PhraseSegmentationAnalysis phrases, IReadOnlyList<SongSectionDirectionInput> sectionInputs)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(phrases);
        ArgumentNullException.ThrowIfNull(sectionInputs);

        if (layout.Models.Count == 0)
        {
            return [];
        }

        var focalRoleBySection = sectionInputs.ToDictionary(s => s.SectionId, s => s.FocalRole);
        var assignments = new List<MusicalRoleAssignment>();

        for (var phraseIndex = 0; phraseIndex < phrases.Phrases.Count; phraseIndex++)
        {
            var phrase = phrases.Phrases[phraseIndex];
            var focalRole = focalRoleBySection.TryGetValue(phrase.SectionLabelId, out var role) ? role : MusicalRole.Atmosphere;
            var focalPropIndex = phraseIndex % layout.Models.Count;

            for (var propIndex = 0; propIndex < layout.Models.Count; propIndex++)
            {
                var model = layout.Models[propIndex];
                var isFocalProp = propIndex == focalPropIndex;
                var assignedRole = isFocalProp ? focalRole : SupportingRole(propIndex, phraseIndex);
                var spatialPath = new SpatialPath(
                    Id: Guid.NewGuid(), Name: $"{model.XlightsName}-path", OrderedModels: [model.Id], IsClosed: false, Version: 0);

                assignments.Add(new MusicalRoleAssignment(
                    PhraseId: phrase.Id,
                    SourceTrackId: model.XlightsName,
                    Role: assignedRole,
                    SpatialPathId: spatialPath.Id,
                    PrecedenceTier: PrecedenceTier.Automated,
                    Priority: isFocalProp ? 0 : 1,
                    Weight: isFocalProp ? 1.0 : 0.5,
                    Version: 0));
            }
        }

        return assignments;
    }

    private static MusicalRole SupportingRole(int propIndex, int phraseIndex) =>
        AllRoles[(propIndex + phraseIndex + 1) % AllRoles.Length];
}
