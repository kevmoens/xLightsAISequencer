using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Mirrors <c>XlightsAI.Studio.Choreography.Tests.RealXLightsFixture</c> (not shared across test projects by convention in this codebase) -- real xLights install/show access, skip-if-absent.</summary>
public static class RealXLightsFixture
{
    public const string EffectMetadataRoot = @"C:\Program Files\xLights\effectmetadata";
    public const string ShowDirectory = @"C:\xLights";
    public const string LayoutPath = @"C:\xLights\xlights_rgbeffects.xml";

    public static bool IsAvailable => Directory.Exists(EffectMetadataRoot) && File.Exists(LayoutPath);

    public static (EffectVocabularyCatalog Vocabulary, EffectRecipeCatalog Recipes) BuildCatalogs() =>
        EffectCatalogFactory.BuildFromMetadataDirectory(EffectMetadataRoot);
}
