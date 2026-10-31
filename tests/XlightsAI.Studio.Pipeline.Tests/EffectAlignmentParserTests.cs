namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// <see cref="EffectAlignmentParser"/> against the same golden fixture
/// <c>XlightsAI.Studio.Xlights.Tests</c> uses to prove <see cref="XlightsAI.Studio.Xlights.XsqStructuralReader"/>
/// itself: three models, each with one "On" effect on its own single <c>&lt;EffectLayer&gt;</c>.
/// </summary>
public sealed class EffectAlignmentParserTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "golden-three-effect-sequence.xsq");

    [Fact]
    public void Parse_ReturnsThreePlacements_WithLayerIndexZeroAndResolvedSettings()
    {
        var instances = EffectAlignmentParser.Parse(FixturePath);

        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(0, i.LayerIndex));
        Assert.All(instances, i => Assert.Equal("On", i.EffectName));
        Assert.All(instances, i => Assert.Equal(string.Empty, i.SettingsString)); // all-default effect, architecture §8a
        Assert.All(instances, i => Assert.Equal("model", i.ModelType));

        Assert.Equal(["Roofline", "Arch 1", "Arch 2"], instances.Select(i => i.ModelName));
        Assert.Equal([0, 10000, 20000], instances.Select(i => i.StartTimeMs));
        Assert.Equal([10000, 20000, 30000], instances.Select(i => i.EndTimeMs));
    }
}
