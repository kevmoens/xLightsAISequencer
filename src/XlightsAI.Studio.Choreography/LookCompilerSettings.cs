using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>Everything <see cref="LookCompiler"/> needs that is not specific to one <see cref="Look"/>/<see cref="SectionMusicContext"/> pair.</summary>
/// <param name="Vocabulary">The generated effect vocabulary (architecture §8a) -- every parameter override is validated against this, never invented.</param>
/// <param name="RecipeCatalog">Registered recipes (ordinarily built via <c>GeneratedEffectRecipeFactory.RegisterAll</c>) this compiler resolves a Look layer's own declared <see cref="LookLayer.Effect"/> xLights name against.</param>
/// <param name="RecipeRegistry">Verifies and constructs each concrete <see cref="EffectInstance"/>.</param>
/// <param name="PaletteLibrary">The Show Bible's own named palette set, keyed by <see cref="Look.PaletteRef"/>.</param>
/// <param name="FillerPool">The always-moving rule's own rotating filler pool (architecture §7, ≥6 distinct states) -- applied by <see cref="ChoreographyEngine"/> after every section is compiled, not by this compiler directly (a single Look's own compile should never need to reach outside its own section window to find its predecessor's last effect).</param>
public sealed record LookCompilerSettings(
    EffectVocabularyCatalog Vocabulary,
    EffectRecipeCatalog RecipeCatalog,
    IEffectRecipeRegistry RecipeRegistry,
    IReadOnlyDictionary<string, ColorPaletteDefinition> PaletteLibrary,
    FillerPool FillerPool);
