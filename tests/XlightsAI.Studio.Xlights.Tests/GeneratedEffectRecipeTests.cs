using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-2a task 6: proves a generated recipe registers and resolves through the existing Phase 1b
/// extension point (<see cref="EffectRecipeCatalog.Register"/>/<see cref="EffectRecipeRegistry"/>)
/// without touching <see cref="EffectRecipeRegistry"/> or <see cref="XsqExporter"/> themselves.
/// </summary>
public sealed class GeneratedEffectRecipeTests
{
    private static EffectVocabularyCatalog BuildFixtureVocabulary()
    {
        var fixturesRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "effectmetadata");
        return EffectVocabularyBuilder.BuildFromDirectory(fixturesRoot);
    }

    [Fact]
    public void RegisterAll_AddsOneRecipePerGeneratedEffect_AlongsideTheBuiltInOnRecipe()
    {
        var vocabularyCatalog = BuildFixtureVocabulary();
        var recipeCatalog = EffectRecipeCatalog.CreateDefault();

        GeneratedEffectRecipeFactory.RegisterAll(vocabularyCatalog, recipeCatalog);

        Assert.True(recipeCatalog.TryGet(OnEffectRecipe.Id, out _));
        Assert.True(recipeCatalog.TryGet("effect.generated.bars.v1", out var bars));
        Assert.Equal("Bars", bars.XlightsEffectName);
    }

    [Fact]
    public void GeneratedRecipe_ResolvesThroughTheRegistryWithoutAnyCodeChangeToIt()
    {
        var vocabularyCatalog = BuildFixtureVocabulary();
        var recipeCatalog = EffectRecipeCatalog.CreateDefault();
        GeneratedEffectRecipeFactory.RegisterAll(vocabularyCatalog, recipeCatalog);

        var registry = new EffectRecipeRegistry(recipeCatalog);
        var instance = registry.CreateVerified("effect.generated.bars.v1", new ModelId("Tree"), 0, 10_000_000);

        Assert.Equal("effect.generated.bars.v1", instance.RecipeId);
    }

    [Fact]
    public void GeneratedRecipe_DefaultParameters_MatchTheGeneratedVocabularyDefaults()
    {
        var vocabularyCatalog = BuildFixtureVocabulary();
        var recipe = new GeneratedEffectRecipe(vocabularyCatalog.Effects["Bars"], vocabularyCatalog);

        var defaults = recipe.CreateDefaultParameters();

        Assert.Equal("1", defaults["E_SLIDER_Bars_BarCount"]);
        Assert.Equal("up", defaults["E_CHOICE_Bars_Direction"]);
    }

    [Fact]
    public void GeneratedRecipe_IsSupportedParameterValue_DelegatesToTheRealVocabulary_NeverInventingAcceptance()
    {
        var vocabularyCatalog = BuildFixtureVocabulary();
        var recipe = new GeneratedEffectRecipe(vocabularyCatalog.Effects["Bars"], vocabularyCatalog);

        Assert.True(recipe.IsSupportedParameterValue("E_SLIDER_Bars_BarCount", "3"));
        Assert.False(recipe.IsSupportedParameterValue("E_SLIDER_Bars_BarCount", "99"));
        Assert.False(recipe.IsSupportedParameterValue("E_SLIDER_DoesNotExist", "1"));
    }
}
