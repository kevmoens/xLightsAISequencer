using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.1 gate: "<c>LayerBlendPlanner</c> selects only confirmed modes -- write a
/// test that asserts this directly, e.g. fuzzing/enumerating planner output and checking every value
/// against <c>ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue</c>." Hermetic -- no real xLights install
/// required, since <see cref="LayerBlendPlanner"/> only ever touches <see cref="Look"/>/<see cref="MultiLayerTemplateCatalog"/>
/// data, never the real effect vocabulary.
/// </summary>
public sealed class LayerBlendPlannerTests
{
    private static LookLayer Layer(int z, string effect, string blend = "Normal") =>
        new(z, "group:Test", effect, z == 0 ? LookLayer.RoleSubject : LookLayer.RoleSupport, null, blend, "Beats", 0.5);

    private static Look LookWithLayers(params LookLayer[] layers) =>
        new("look-1", "Look 1", "test intent", 0.5, "chase", "warm-gold", "group:Test", layers);

    private static MultiLayerTemplateLayer TemplateLayer(string effect, string? blendToPrevious = null) =>
        new("support", effect, 0.1, 0.9, "beat", "note", blendToPrevious);

    private static EffectVisualCharacterCatalog EmptyBrightnessCatalog() => new(DateTimeOffset.UtcNow, []);

    [Fact]
    public void Plan_EveryEmittedBlendValue_IsAConfirmedXmlValue_AcrossEveryMinedCandidate()
    {
        // Fuzz: build one template per real confirmed candidate, pairing the same two effect names, and
        // confirm the planner only ever emits that candidate's own confirmed XML value -- never anything
        // else -- for every single one of the 8 confirmed modes.
        foreach (var mode in ConfirmedLayerBlendModeCatalog.Modes)
        {
            var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow,
            [
                new MultiLayerTemplate(
                    "t-" + mode.CandidateName, "t", "mid", ["Any"], "chase",
                    [TemplateLayer("On"), TemplateLayer("Twinkle", mode.CandidateName)],
                    "fixture"),
            ]);

            var look = LookWithLayers(Layer(0, "On"), Layer(1, "Twinkle"));
            var planned = LayerBlendPlanner.Plan(look, catalog, EmptyBrightnessCatalog());

            var topLayer = Assert.Single(planned.Layers, l => l.Z == 1);
            Assert.True(ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue(topLayer.Blend));
            Assert.Equal(mode.XmlValue, topLayer.Blend);
        }
    }

    [Fact]
    public void Plan_LeavesLayerUnchanged_WhenCatalogHasNoMinedEvidenceForThisEffectPair()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow, []);
        var look = LookWithLayers(Layer(0, "On"), Layer(1, "Twinkle"));

        var planned = LayerBlendPlanner.Plan(look, catalog, EmptyBrightnessCatalog());

        Assert.Equal("Normal", planned.Layers.Single(l => l.Z == 1).Blend);
    }

    [Fact]
    public void Plan_SingleLayerLook_ReturnsLookUnchanged()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow, []);
        var look = LookWithLayers(Layer(0, "On"));

        var planned = LayerBlendPlanner.Plan(look, catalog, EmptyBrightnessCatalog());

        Assert.Same(look, planned);
    }

    [Fact]
    public void Plan_NeverTouchesTheBaseLayer_OnlyTheLayerStackedOnTop()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow,
        [
            new MultiLayerTemplate("t1", "t", "mid", ["Any"], "chase",
                [TemplateLayer("On"), TemplateLayer("Twinkle", "Mask")], "fixture"),
        ]);

        var look = LookWithLayers(Layer(0, "On"), Layer(1, "Twinkle"));
        var planned = LayerBlendPlanner.Plan(look, catalog, EmptyBrightnessCatalog());

        Assert.Equal("Normal", planned.Layers.Single(l => l.Z == 0).Blend);
        Assert.Equal("2 is Mask", planned.Layers.Single(l => l.Z == 1).Blend);
    }

    [Fact]
    public void Plan_ThreeLayerStack_SelectsIndependentlyPerAdjacentPair()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow,
        [
            new MultiLayerTemplate("t1", "t", "mid", ["Any"], "chase",
                [TemplateLayer("On"), TemplateLayer("Twinkle", "Mask"), TemplateLayer("Shockwave", "Average")], "fixture"),
        ]);

        var look = LookWithLayers(Layer(0, "On"), Layer(1, "Twinkle"), Layer(2, "Shockwave"));
        var planned = LayerBlendPlanner.Plan(look, catalog, EmptyBrightnessCatalog());

        Assert.Equal("Normal", planned.Layers.Single(l => l.Z == 0).Blend);
        Assert.Equal("2 is Mask", planned.Layers.Single(l => l.Z == 1).Blend);
        Assert.Equal("Average", planned.Layers.Single(l => l.Z == 2).Blend);
    }
}
