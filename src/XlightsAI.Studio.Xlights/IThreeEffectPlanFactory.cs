using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 012 ("Create effect plan containing three hardcoded simple effects on selected real
/// props"): builds an initial <see cref="SequenceDocument"/> targeting exactly three real, distinct,
/// already-imported props (models or model groups) from a <see cref="DisplayGraph"/>, spaced across a
/// known song duration. Never mutates <paramref name="layout"/> and never writes anything — pure
/// domain composition over <see cref="IEffectRecipeRegistry"/>.
/// </summary>
public interface IThreeEffectPlanFactory
{
    /// <exception cref="EffectPlanInvalidException">
    /// <paramref name="targetNames"/> does not contain exactly three distinct entries, any entry
    /// does not resolve to a real model/group name in
    /// <paramref name="layout"/>, or <paramref name="songDurationTicks"/> is not positive. Thrown
    /// before any <see cref="SequenceDocument"/> is constructed.
    /// </exception>
    SequenceDocument CreatePlan(
        DisplayGraph layout,
        IReadOnlyList<string> targetNames,
        long songDurationTicks,
        IEffectRecipeRegistry recipeRegistry);
}
