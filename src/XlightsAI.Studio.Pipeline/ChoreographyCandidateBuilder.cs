using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// The actual bridge code that did not exist anywhere in the codebase before this pipeline: turns each
/// <see cref="MusicalRoleAssignment"/> M3's <see cref="DeterministicPropRoleAssigner"/> produced into a
/// real <see cref="ChoreographyCandidate"/> — the Composition and WholeSong projects were confirmed
/// (during planning) to never reference each other at all. The candidate's time window is the
/// assignment's own phrase's real window (<see cref="PhraseCandidate.StartTicks"/>/<see cref="PhraseCandidate.EndTicks"/>
/// from M2's Structure analysis); the recipe is always <see cref="EffectRecipeRegistry.SimpleOnRecipeId"/>
/// because, confirmed before writing this, it is the only verified effect recipe this registry implements
/// at all — not a simplification on this pipeline's part.
/// </summary>
public static class ChoreographyCandidateBuilder
{
    public static IReadOnlyList<ChoreographyCandidate> BuildCandidates(
        IReadOnlyList<MusicalRoleAssignment> roleAssignments, PhraseSegmentationAnalysis phrases)
    {
        ArgumentNullException.ThrowIfNull(roleAssignments);
        ArgumentNullException.ThrowIfNull(phrases);

        var phrasesById = phrases.Phrases.ToDictionary(p => p.Id);
        var candidates = new List<ChoreographyCandidate>(roleAssignments.Count);

        foreach (var assignment in roleAssignments)
        {
            if (!phrasesById.TryGetValue(assignment.PhraseId, out var phrase))
            {
                throw new ArgumentException(
                    $"MusicalRoleAssignment references phrase '{assignment.PhraseId}', which is not present in the supplied PhraseSegmentationAnalysis.",
                    nameof(roleAssignments));
            }

            candidates.Add(new ChoreographyCandidate(
                Id: Guid.NewGuid(),
                Assignment: assignment,
                Target: new ModelId(assignment.SourceTrackId),
                Window: new TimeWindow(phrase.StartTicks, phrase.EndTicks),
                RecipeId: EffectRecipeRegistry.SimpleOnRecipeId));
        }

        return candidates;
    }
}
