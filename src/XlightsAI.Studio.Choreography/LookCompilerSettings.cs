using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>Everything <see cref="LookCompiler"/> needs that is not specific to one <see cref="Look"/>/<see cref="SectionMusicContext"/> pair.</summary>
/// <param name="Vocabulary">The generated effect vocabulary (architecture §8a) -- every parameter override is validated against this, never invented.</param>
/// <param name="RecipeCatalog">Registered recipes (ordinarily built via <c>GeneratedEffectRecipeFactory.RegisterAll</c>) this compiler resolves a Look layer's own declared <see cref="LookLayer.Effect"/> xLights name against.</param>
/// <param name="RecipeRegistry">Verifies and constructs each concrete <see cref="EffectInstance"/>.</param>
/// <param name="PaletteLibrary">The Show Bible's own named palette set, keyed by <see cref="Look.PaletteRef"/>.</param>
public sealed record LookCompilerSettings(
    EffectVocabularyCatalog Vocabulary,
    EffectRecipeCatalog RecipeCatalog,
    IEffectRecipeRegistry RecipeRegistry,
    IReadOnlyDictionary<string, ColorPaletteDefinition> PaletteLibrary);
