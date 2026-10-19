using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 050 ("Compile melody to stepping, holding, jumping choreography across ordered props").
/// Section 3's own proposed signature, with one disclosed, deliberate addition: an
/// <see cref="IEffectRecipeRegistry"/> parameter. See <see cref="MelodicGestureMapper"/> remarks for why.
/// Pure, synchronous domain composition -- same shape as <see cref="MelodicContourClassifier"/> and
/// <see cref="ThreeEffectPlanFactory"/> (no file I/O, no ambient clock, never mutates
/// <paramref name="track"/>).
/// </summary>
public interface IMelodicGestureMapper
{
    /// <exception cref="EffectPlanInvalidException">
    /// <paramref name="orderedPath"/> is empty, <paramref name="window"/> is not a valid non-empty
    /// <c>[start, end)</c> range, or <paramref name="settings"/> names an unknown
    /// <see cref="GestureMapSettings.PitchPolicy"/> or <see cref="GestureMapSettings.RecipeId"/>. Thrown
    /// before any <see cref="EffectInstance"/> is constructed.
    /// </exception>
    IReadOnlyList<EffectInstance> MapToEffects(
        MelodyTrack track,
        IReadOnlyList<ModelId> orderedPath,
        TimeWindow window,
        GestureMapSettings settings,
        IEffectRecipeRegistry recipeRegistry);
}
