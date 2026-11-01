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

    /// <summary>Same always-present-in-a-real-vocabulary 6-name set <c>XlightsAI.Studio.Choreography.Tests.RealXLightsFixture</c> uses -- a real, valid <c>SectionScript.FillerEffects</c> value for tests in this project that construct a script by hand (build-plan Phase 15: FillerEffects is now required).</summary>
    public static readonly string[] SixCommonEffectNames = ["On", "Bars", "SingleStrand", "Color Wash", "Twinkle", "Butterfly"];
}
