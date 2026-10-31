namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1b (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md, "The effect recipe system"): one known-good,
/// independently registrable effect recipe — the real xLights effect family it compiles to, its own
/// verified default parameters, and whether a given key/value is one this build has actually confirmed
/// (never invented) against real xLights behavior (architecture §8a). <see cref="EffectRecipeCatalog"/>
/// is the registration mechanism this plugs into; <see cref="EffectRecipeRegistry"/> is the only
/// consumer that needs to resolve a recipe id to one of these. Adding a new recipe means implementing
/// this interface once and calling <see cref="EffectRecipeCatalog.Register"/> — never editing
/// <see cref="EffectRecipeRegistry"/>, <see cref="XsqExporter"/> and a plan factory in parallel, which
/// is the "three coupled files" gap architecture §12 blocker 2 names.
/// </summary>
public interface IEffectRecipe
{
    /// <summary>The recipe id callers pass to <see cref="IEffectRecipeRegistry.CreateVerified"/>.</summary>
    string RecipeId { get; }

    /// <summary>The recipe's own schema/behavior version, carried onto every <see cref="EffectInstance"/> it produces.</summary>
    string RecipeVersion { get; }

    /// <summary>The real xLights effect name (the <c>&lt;Effect name="..."&gt;</c> value) this recipe compiles to.</summary>
    string XlightsEffectName { get; }

    /// <summary>
    /// True when <paramref name="value"/> is a verified-acceptable value for parameter
    /// <paramref name="key"/> on this recipe. Never invents a plausible-looking answer for a key/value
    /// this recipe has not actually verified (architecture §2's "knowing-doing gap" / Section 2's "do
    /// not invent xLights effect parameters").
    /// </summary>
    bool IsSupportedParameterValue(string key, string value);

    /// <summary>
    /// This recipe's own default, fully-verified typed parameter set — the starting point
    /// <see cref="EffectRecipeRegistry.CreateVerified"/> attaches to the <see cref="EffectInstance"/>
    /// it produces.
    /// </summary>
    IReadOnlyDictionary<string, string> CreateDefaultParameters();
}
