using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// Phase 30 step 30.0 gate: every candidate layer-blend mode's exact <c>T_CHOICE_LayerMethod</c> XML
/// value, as carried in <see cref="ConfirmedLayerBlendModeCatalog"/>, must actually appear -- spelled
/// exactly that way -- in <c>Fixtures/blend-modes/blend-mode-exemplars.xsq</c>, parsed with the same
/// <see cref="IXsqStructuralReader"/> the rest of this project already trusts for real .xsq structure.
/// This is the automated half of "confirm against a real fixture before any mode ships"; the other half
/// (that the fixture's own values were mined from real xLights-authored show files, not invented) is
/// recorded in implementation-evidence/PHASE-30-blend-mode-baseline.md, not re-provable in a unit test.
/// </summary>
public sealed class Phase30BlendModeFixtureTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "blend-modes", name);

    [Fact]
    public void Fixture_has_two_overlapping_layers_on_one_model()
    {
        var document = new XsqStructuralReader().Read(FixturePath("blend-mode-exemplars.xsq"));

        var element = Assert.Single(document.ElementEffects, e => e.Name == "Everything");
        var baseEffect = Assert.Single(element.Effects, e => e.Name == "On");
        Assert.Equal(0, baseEffect.StartTimeMs);
        Assert.Equal(8000, baseEffect.EndTimeMs);

        var overlayEffects = element.Effects.Where(e => e.Name == "Bars").ToList();
        Assert.Equal(8, overlayEffects.Count);
        Assert.All(overlayEffects, e => Assert.True(e.StartTimeMs >= baseEffect.StartTimeMs && e.EndTimeMs <= baseEffect.EndTimeMs));
    }

    [Theory]
    [InlineData("Mask", "2 is Mask")]
    [InlineData("Unmask", "2 is Unmask")]
    [InlineData("Reveal 1-on-2", "1 reveals 2")]
    [InlineData("Reveal 2-on-1", "2 reveals 1")]
    [InlineData("Average", "Average")]
    [InlineData("Subtractive", "Subtractive")]
    [InlineData("Shadow 1-on-2", "Shadow 1 on 2")]
    [InlineData("Shadow 2-on-1", "Shadow 2 on 1")]
    public void Confirmed_catalog_value_matches_exactly_what_the_fixture_carries(string candidateName, string expectedXmlValue)
    {
        var catalogEntry = ConfirmedLayerBlendModeCatalog.Find(candidateName);
        Assert.NotNull(catalogEntry);
        Assert.Equal(expectedXmlValue, catalogEntry!.XmlValue);

        var document = new XsqStructuralReader().Read(FixturePath("blend-mode-exemplars.xsq"));
        var settingsStrings = document.EffectDb;
        Assert.Contains(settingsStrings, s => s == $"T_CHOICE_LayerMethod={expectedXmlValue}");
    }

    [Fact]
    public void Catalog_has_exactly_the_eight_build_plan_candidates_and_no_more()
    {
        var names = ConfirmedLayerBlendModeCatalog.Modes.Select(m => m.CandidateName).ToList();
        Assert.Equal(
            new[] { "Mask", "Unmask", "Reveal 1-on-2", "Reveal 2-on-1", "Average", "Subtractive", "Shadow 1-on-2", "Shadow 2-on-1" },
            names);
    }

    [Theory]
    [InlineData("Mask", true)]
    [InlineData("Reveal 1-on-2", true)]
    [InlineData("Reveal 2-on-1", true)]
    [InlineData("Unmask", false)]
    [InlineData("Average", false)]
    [InlineData("Subtractive", false)]
    [InlineData("Shadow 1-on-2", false)]
    [InlineData("Shadow 2-on-1", false)]
    public void Each_mode_honestly_records_whether_it_was_reconfirmed_at_the_installed_version(string candidateName, bool expectedConfirmedAtInstalledVersion)
    {
        var catalogEntry = ConfirmedLayerBlendModeCatalog.Find(candidateName);
        Assert.NotNull(catalogEntry);
        Assert.Equal(expectedConfirmedAtInstalledVersion, catalogEntry!.ConfirmedAtInstalledVersion);
    }

    [Fact]
    public void IsConfirmedXmlValue_accepts_every_catalog_value_and_the_default_but_rejects_an_unconfirmed_guess()
    {
        foreach (var mode in ConfirmedLayerBlendModeCatalog.Modes)
        {
            Assert.True(ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue(mode.XmlValue));
        }

        Assert.True(ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue(ConfirmedLayerBlendModeCatalog.DefaultXmlValue));
        Assert.False(ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue("Brightness"));
        Assert.False(ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue("Bottom-Top"));
    }
}
