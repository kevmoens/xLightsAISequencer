using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IThreeEffectPlanFactory"/>. Splits <paramref name="songDurationTicks"/> (see
/// <see cref="CreatePlan"/>) into three contiguous, non-overlapping, half-open windows — one
/// deterministic "On" effect per selected target, in the order the caller supplied them — and
/// validates every target resolves against the same already-imported <see cref="DisplayGraph"/>
/// (never a guessed/invented model name) before constructing anything.
/// </summary>
public sealed class ThreeEffectPlanFactory : IThreeEffectPlanFactory
{
    public const int RequiredTargetCount = 3;

    public SequenceDocument CreatePlan(
        DisplayGraph layout,
        IReadOnlyList<string> targetNames,
        long songDurationTicks,
        IEffectRecipeRegistry recipeRegistry)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(targetNames);
        ArgumentNullException.ThrowIfNull(recipeRegistry);

        if (targetNames.Count != RequiredTargetCount)
        {
            throw new EffectPlanInvalidException(
                $"Exactly {RequiredTargetCount} target props must be selected (received {targetNames.Count}).");
        }

        if (targetNames.Distinct(StringComparer.Ordinal).Count() != RequiredTargetCount)
        {
            throw new EffectPlanInvalidException("The selected target props must be distinct; the same prop was selected more than once.");
        }

        if (songDurationTicks <= 0)
        {
            throw new EffectPlanInvalidException($"Song duration must be a positive number of ticks (received {songDurationTicks}).");
        }

        var modelNames = new HashSet<string>(layout.Models.Select(m => m.XlightsName), StringComparer.Ordinal);
        var groupNames = new HashSet<string>(layout.Groups.Select(g => g.Name), StringComparer.Ordinal);

        var resolvedTargets = new List<ModelId>(RequiredTargetCount);
        foreach (var name in targetNames)
        {
            if (!modelNames.Contains(name) && !groupNames.Contains(name))
            {
                throw new EffectPlanInvalidException(
                    $"'{name}' is not a real model or model group name in the imported layout; no effect plan was created.");
            }

            resolvedTargets.Add(new ModelId(name));
        }

        // Equal thirds, half-open [start, end): the final segment absorbs any remainder so the
        // three windows exactly tile [0, songDurationTicks) with no gap and no overrun.
        var segmentTicks = songDurationTicks / RequiredTargetCount;
        var effects = new List<EffectInstance>(RequiredTargetCount);
        for (var i = 0; i < RequiredTargetCount; i++)
        {
            var startTicks = i * segmentTicks;
            var endTicks = i == RequiredTargetCount - 1 ? songDurationTicks : startTicks + segmentTicks;
            effects.Add(recipeRegistry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, resolvedTargets[i], startTicks, endTicks));
        }

        return new SequenceDocument(Guid.NewGuid(), Revision: 1, effects);
    }
}
