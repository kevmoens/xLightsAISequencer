using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.4 (effect-family concentration advisory): a real, synthetic compiled IR
/// (no real xLights vocabulary needed -- same discipline as <see cref="EmphasisContrastPlannerTests"/>)
/// exercising both halves of the acceptance definition: a section leaning on one family past 50% gets
/// flagged, and a section whose Look declares that exact reliance in its own <see cref="Look.Intent"/> is
/// correctly exempted -- proven directly, not merely assumed.
/// </summary>
public sealed class EffectFamilyConcentrationAdvisorTests
{
    private static readonly ModelId Tree = new("Tree");

    private sealed class FakeRecipe(string recipeId, string xlightsEffectName) : IEffectRecipe
    {
        public string RecipeId => recipeId;
        public string RecipeVersion => "1";
        public string XlightsEffectName => xlightsEffectName;
        public bool IsSupportedParameterValue(string key, string value) => true;
        public IReadOnlyDictionary<string, string> CreateDefaultParameters() => new Dictionary<string, string>();
    }

    private static EffectRecipeCatalog BuildRecipeCatalog()
    {
        var catalog = new EffectRecipeCatalog();
        catalog.Register(new FakeRecipe("effect.generated.shader.v1", "Shader"));
        catalog.Register(new FakeRecipe("effect.generated.twinkle.v1", "Twinkle"));
        return catalog;
    }

    private static EffectFamilyIndex BuildFamilyIndex() =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)); // no mined pairs needed -- each effect is its own singleton family

    private static EffectInstance Effect(string recipeId, long startTicks, long endTicks) =>
        new(Guid.NewGuid(), Tree, 0, startTicks, endTicks, recipeId, "1", new Dictionary<string, string>());

    private static Look BuildLook(string intent) =>
        new("look-1", "Test Look", intent, EnergyCeiling: 1.0, Motion: "hold", PaletteRef: "palette-1",
            Subject: "Tree", Layers: [new LookLayer(0, "Tree", "Shader", LookLayer.RoleSubject, null, "Normal", null, 1.0)]);

    private const long TicksPerSecond = 10_000_000L;

    [Fact]
    public void Measure_SectionDominatedByOneFamily_ReportsAFinding()
    {
        var ir = new ChoreographyIR([],
        [
            Effect("effect.generated.shader.v1", 0, TicksPerSecond),
            Effect("effect.generated.shader.v1", TicksPerSecond, 2 * TicksPerSecond),
            Effect("effect.generated.shader.v1", 2 * TicksPerSecond, 3 * TicksPerSecond),
            Effect("effect.generated.twinkle.v1", 3 * TicksPerSecond, 4 * TicksPerSecond),
        ], []);
        var window = new TimeWindow(0, 4 * TicksPerSecond);
        var look = BuildLook("A bright, high-energy chorus section.");

        var findings = EffectFamilyConcentrationAdvisor.Measure(ir, window, "section-1", look, BuildRecipeCatalog(), BuildFamilyIndex());

        var finding = Assert.Single(findings);
        Assert.Equal("Shader", finding.Family);
        Assert.Equal(3, finding.FamilyLayerCount);
        Assert.Equal(4, finding.TotalLayerCount);
        Assert.True(finding.Share > EffectFamilyConcentrationAdvisor.ConcentrationThreshold);
    }

    [Fact]
    public void Measure_SectionWithDeclaredSingleFamilyIntent_IsExempted()
    {
        var ir = new ChoreographyIR([],
        [
            Effect("effect.generated.shader.v1", 0, TicksPerSecond),
            Effect("effect.generated.shader.v1", TicksPerSecond, 2 * TicksPerSecond),
            Effect("effect.generated.shader.v1", 2 * TicksPerSecond, 3 * TicksPerSecond),
            Effect("effect.generated.twinkle.v1", 3 * TicksPerSecond, 4 * TicksPerSecond),
        ], []);
        var window = new TimeWindow(0, 4 * TicksPerSecond);
        var look = BuildLook("Deliberately saturates the whole section in Shader texture -- a sustained single-family look.");

        var findings = EffectFamilyConcentrationAdvisor.Measure(ir, window, "section-1", look, BuildRecipeCatalog(), BuildFamilyIndex());

        Assert.Empty(findings);
    }

    [Fact]
    public void Measure_NoSingleFamilyOverThreshold_ReportsNoFinding()
    {
        var ir = new ChoreographyIR([],
        [
            Effect("effect.generated.shader.v1", 0, TicksPerSecond),
            Effect("effect.generated.twinkle.v1", TicksPerSecond, 2 * TicksPerSecond),
        ], []);
        var window = new TimeWindow(0, 2 * TicksPerSecond);
        var look = BuildLook("A balanced section.");

        var findings = EffectFamilyConcentrationAdvisor.Measure(ir, window, "section-1", look, BuildRecipeCatalog(), BuildFamilyIndex());

        Assert.Empty(findings);
    }

    [Fact]
    public void Measure_MinedFamilyPair_CombinesSharesAcrossBothEffectNames()
    {
        var familyIndex = new EffectFamilyIndex(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Shader"] = "Shader",
            ["Twinkle"] = "Shader",
        });
        var ir = new ChoreographyIR([],
        [
            Effect("effect.generated.shader.v1", 0, TicksPerSecond),
            Effect("effect.generated.twinkle.v1", TicksPerSecond, 2 * TicksPerSecond),
            Effect("effect.generated.twinkle.v1", 2 * TicksPerSecond, 3 * TicksPerSecond),
        ], []);
        var window = new TimeWindow(0, 3 * TicksPerSecond);
        var look = BuildLook("A generic section.");

        var findings = EffectFamilyConcentrationAdvisor.Measure(ir, window, "section-1", look, BuildRecipeCatalog(), familyIndex);

        var finding = Assert.Single(findings);
        Assert.Equal("Shader", finding.Family);
        Assert.Equal(3, finding.FamilyLayerCount);
    }
}
