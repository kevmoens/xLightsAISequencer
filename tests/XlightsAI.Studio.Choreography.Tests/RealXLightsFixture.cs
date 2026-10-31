using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Shared access to the real xLights install's own effect metadata and the real <c>C:\xLights</c> show,
/// when present on the machine running the tests -- mirrors <c>XlightsAI.Studio.Knowledge.Tests.RealXlightsInstallTests</c>'
/// own established "skip cleanly, never fail, when absent" convention. Phase 4's own compiler is
/// meaningless to test against invented effect names/parameters (this codebase's own discipline forbids
/// that), so most of this phase's own tests are real-data tests by necessity, not choice.
/// </summary>
public static class RealXLightsFixture
{
    public const string EffectMetadataRoot = @"C:\Program Files\xLights\effectmetadata";
    public const string ShowDirectory = @"C:\xLights";
    public const string LayoutPath = @"C:\xLights\xlights_rgbeffects.xml";

    public static bool IsAvailable => Directory.Exists(EffectMetadataRoot) && File.Exists(LayoutPath);

    public static (EffectVocabularyCatalog Vocabulary, EffectRecipeCatalog Recipes) BuildCatalogs() =>
        EffectCatalogFactory.BuildFromMetadataDirectory(EffectMetadataRoot);

    /// <summary>A rotating filler pool of 6 common, always-present real effects -- not a creative choice, just a deterministic, always-available ≥6-state pool for the always-moving rule.</summary>
    public static readonly string[] SixCommonEffectNames = ["On", "Bars", "SingleStrand", "Color Wash", "Twinkle", "Butterfly"];

    public static FillerPool BuildFillerPool(EffectRecipeCatalog recipes)
    {
        // Prefer the *generated* recipe (vocabulary-verified defaults) over a same-named legacy
        // hand-written one (e.g. OnEffectRecipe's own fabricated "EffectFamily" default, which fails
        // real vocabulary validation) -- same rule LookCompiler.RecipeIdFor applies.
        var ids = SixCommonEffectNames
            .Select(name =>
                recipes.Recipes.FirstOrDefault(r => r.RecipeId.StartsWith("effect.generated.", StringComparison.Ordinal) && r.XlightsEffectName == name)
                ?? recipes.Recipes.First(r => string.Equals(r.XlightsEffectName, name, StringComparison.Ordinal)))
            .Select(r => r.RecipeId)
            .ToList();
        return new FillerPool(ids);
    }
}
