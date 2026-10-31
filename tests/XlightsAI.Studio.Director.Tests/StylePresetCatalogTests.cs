namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan Phase 8 task 1: the style preset taxonomy itself, independent of prompt rendering.</summary>
public sealed class StylePresetCatalogTests
{
    [Theory]
    [InlineData(StylePresetCatalog.TraditionalClassicId)]
    [InlineData(StylePresetCatalog.HighEnergyEdmId)]
    [InlineData(StylePresetCatalog.ElegantWarmWhiteId)]
    [InlineData(StylePresetCatalog.SingingFaceStorytellingId)]
    public void Resolve_KnownId_ReturnsTheMatchingPreset(string id)
    {
        var preset = StylePresetCatalog.Resolve(id);

        Assert.Equal(id, preset.Id);
        Assert.NotEmpty(preset.EffectPool);
    }

    [Fact]
    public void Resolve_IsCaseInsensitiveByDisplayName()
    {
        var preset = StylePresetCatalog.Resolve("high-energy / edm");

        Assert.Equal(StylePresetCatalog.HighEnergyEdmId, preset.Id);
    }

    [Fact]
    public void Resolve_UnknownStyle_ReturnsUnconstrainedGenericPreset_NeverThrows()
    {
        var preset = StylePresetCatalog.Resolve("Some Style Nobody Defined");

        Assert.Empty(preset.EffectPool);
        Assert.Equal(1, preset.PaletteDiscipline.MinActiveSlots);
        Assert.Equal(8, preset.PaletteDiscipline.MaxActiveSlots);
    }

    [Fact]
    public void AllPresets_DeclareAPaletteBandAndADensityBand()
    {
        Assert.All(StylePresetCatalog.All, preset =>
        {
            Assert.True(preset.PaletteDiscipline.MinActiveSlots <= preset.PaletteDiscipline.MaxActiveSlots);
            Assert.True(preset.Density.MinChangeRatePerSecond <= preset.Density.MaxChangeRatePerSecond);
            Assert.True(preset.Density.MinDutyCycle <= preset.Density.MaxDutyCycle);
        });
    }
}
