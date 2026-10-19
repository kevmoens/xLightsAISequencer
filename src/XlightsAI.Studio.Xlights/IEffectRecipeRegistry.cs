using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: the set of known-good, verified effect recipes this build
/// supports. <see cref="CreateVerified"/> never accepts an arbitrary/free-form effect shape — only a
/// recipe id this registry itself recognizes — so no caller (including a future AI proposal, per
/// Section 1's "AI trust" rule) can smuggle an unverified effect shape into a
/// <see cref="SequenceDocument"/>.
/// </summary>
public interface IEffectRecipeRegistry
{
    /// <exception cref="EffectPlanInvalidException">
    /// <paramref name="recipeId"/> is not a known recipe, <paramref name="target"/> is blank, or the
    /// <paramref name="startTicks"/>/<paramref name="endTicks"/> window is not a valid, non-empty,
    /// non-negative <c>[start, end)</c> range.
    /// </exception>
    EffectInstance CreateVerified(string recipeId, ModelId target, long startTicks, long endTicks);

    /// <summary>
    /// PHASE-03 Step 020 ("...edit supported effect parameters"): whether <paramref name="recipeId"/>
    /// recognizes a parameter named <paramref name="key"/> and accepts <paramref name="value"/> for it.
    /// Never invents a plausible-looking answer for a recipe/key this registry does not actually know —
    /// returns <see langword="false"/> for any unrecognized recipe, key, or value (Section 2: "do not
    /// invent xLights effect parameters").
    /// </summary>
    bool IsSupportedParameterValue(string recipeId, string key, string value);
}
