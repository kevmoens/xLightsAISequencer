using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Promoted out of what was two byte-for-byte identical <c>RealXLightsFixture.BuildCatalogs()</c> copies
/// (<c>XlightsAI.Studio.Director.Tests</c> and <c>XlightsAI.Studio.Choreography.Tests</c>) so production
/// code (the Shell's AI choreography wiring) and both test fixtures share one implementation rather than
/// a third copy drifting from the two already duplicated ones. Lives in <c>XlightsAI.Studio.Xlights</c>,
/// not <c>XlightsAI.Studio.Knowledge</c>, because <see cref="EffectRecipeCatalog"/>/
/// <see cref="GeneratedEffectRecipeFactory"/> live here and <c>Xlights</c> already references
/// <c>Knowledge</c> (the reverse reference would be cyclic).
/// </summary>
public static class EffectCatalogFactory
{
    /// <summary>Parses a real xLights <c>effectmetadata</c> directory (architecture §8a) into the generated vocabulary, then registers every corresponding generated recipe into a fresh default recipe catalog (architecture §12 blocker 2).</summary>
    public static (EffectVocabularyCatalog Vocabulary, EffectRecipeCatalog Recipes) BuildFromMetadataDirectory(string effectMetadataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectMetadataDirectory);

        var vocabulary = EffectVocabularyBuilder.BuildFromDirectory(effectMetadataDirectory);
        var recipes = EffectRecipeCatalog.CreateDefault();
        GeneratedEffectRecipeFactory.RegisterAll(vocabulary, recipes);
        return (vocabulary, recipes);
    }
}
