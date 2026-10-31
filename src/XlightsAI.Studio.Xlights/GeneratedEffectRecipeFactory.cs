using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-2a: the one place every generated effect gets turned into an <see cref="IEffectRecipe"/> and
/// registered — the mechanism architecture §12 blocker 2 calls for so that "adding effects beyond On
/// doesn't require touching <see cref="EffectRecipeRegistry"/>, <see cref="XsqExporter"/>, or a plan
/// factory." Callers who want more than the built-in <see cref="OnEffectRecipe"/> build an
/// <see cref="EffectVocabularyCatalog"/> (e.g. via <c>XlightsAI.Studio.Knowledge.EffectVocabularyBuilder.BuildFromDirectory</c>,
/// a dev-time step against a real xLights install) and pass it to <see cref="RegisterAll"/>.
/// </summary>
public static class GeneratedEffectRecipeFactory
{
    /// <summary>
    /// Registers one <see cref="GeneratedEffectRecipe"/> per effect in <paramref name="vocabularyCatalog"/>
    /// into <paramref name="recipeCatalog"/>, replacing any existing recipe already registered under the
    /// same generated id (e.g. a second call after the vocabulary is regenerated).
    /// </summary>
    public static void RegisterAll(EffectVocabularyCatalog vocabularyCatalog, EffectRecipeCatalog recipeCatalog)
    {
        ArgumentNullException.ThrowIfNull(vocabularyCatalog);
        ArgumentNullException.ThrowIfNull(recipeCatalog);

        foreach (var vocabulary in vocabularyCatalog.Effects.Values)
        {
            recipeCatalog.Register(new GeneratedEffectRecipe(vocabulary, vocabularyCatalog));
        }
    }
}
