using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 30 step 30.2 ("hit-motion-compound"): direct, isolated tests of <see cref="HitMotionCompound.TryBuild"/>, independent of the full <see cref="SectionTimelineCompiler"/> path.</summary>
public sealed class HitMotionCompoundTests
{
    [Fact]
    public void TryBuild_RealVocabulary_ReturnsShockwaveWithRealRampAcrossConfirmedMinMax()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var settings = new LookCompilerSettings(vocabulary, recipes, new EffectRecipeRegistry(recipes),
            new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) });

        var movement = HitMotionCompound.TryBuild(new ModelId("Tree"), start: 0, end: 10_000_000, baseLayerZ: 1, settings, "because it hits", "trace-1");

        Assert.NotNull(movement);
        Assert.Equal(-1, movement!.Layer);
        Assert.Equal(0L, movement.StartTicks);
        Assert.Equal(10_000_000L, movement.EndTicks);
        Assert.Equal("because it hits", movement.SourceAnnotation);
        Assert.Equal("trace-1", movement.CueTraceId);

        var curve = Assert.Single(movement.ValueCurvesOrEmpty);
        Assert.Equal(HitMotionCompound.MovementValueCurveParameterKey, curve.ParameterKey);
        Assert.Equal("Ramp", curve.CurveType);
        Assert.Equal(0, curve.Min);
        Assert.Equal(750, curve.Max);
        Assert.Equal(0, curve.P1);
        Assert.Equal(750, curve.P2);

        var recipe = recipes.Recipes.FirstOrDefault(r => string.Equals(r.RecipeId, movement.RecipeId, StringComparison.Ordinal));
        Assert.NotNull(recipe);
        Assert.Equal("Shockwave", recipe!.XlightsEffectName);
    }

    [Fact]
    public void TryBuild_VocabularyWithNoShockwaveProperties_ReturnsNull_NeverInvents()
    {
        var settings = SectionTimelineCompilerTests.BuildPortableSettings();

        var movement = HitMotionCompound.TryBuild(new ModelId("Tree"), start: 0, end: 10_000_000, baseLayerZ: 1, settings, null, null);

        Assert.Null(movement);
    }

    [Fact]
    public void TryBuild_VocabularyWithNoShockwaveEffectAtAll_ReturnsNull_NeverInvents()
    {
        var vocabulary = new EffectVocabularyCatalog(
            new Dictionary<string, EffectVocabulary>(StringComparer.Ordinal) { ["On"] = new("On", "E_", []) },
            new EffectVocabulary("Buffer", "B_", []), new EffectVocabulary("Blending", "T_", []), new EffectVocabulary("Color", "C_", []));
        var recipes = new EffectRecipeCatalog();
        GeneratedEffectRecipeFactory.RegisterAll(vocabulary, recipes);
        var settings = new LookCompilerSettings(vocabulary, recipes, new EffectRecipeRegistry(recipes),
            new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) });

        var movement = HitMotionCompound.TryBuild(new ModelId("Tree"), start: 0, end: 10_000_000, baseLayerZ: 1, settings, null, null);

        Assert.Null(movement);
    }

    [Fact]
    public void TryBuild_EmptyWindow_ReturnsNull()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var settings = new LookCompilerSettings(vocabulary, recipes, new EffectRecipeRegistry(recipes),
            new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) });

        var movement = HitMotionCompound.TryBuild(new ModelId("Tree"), start: 100, end: 100, baseLayerZ: 1, settings, null, null);

        Assert.Null(movement);
    }
}
