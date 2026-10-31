namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Build-plan Phase 10 §6b tasks 1/2: the shippable multi-layer Tree/Matrix template catalog must load
/// from this assembly's own embedded resource (never from a loose file next to it -- see
/// <see cref="MultiLayerTemplateCatalogLoader"/>'s own doc comment for why), and every template must
/// carry the generic, palette-free shape a real <c>Look</c>/<c>LookLayer</c> can be built from directly.
/// </summary>
public sealed class MultiLayerTemplateCatalogLoaderTests
{
    [Fact]
    public void Load_ReturnsTheThreeCuratedPatterns()
    {
        var catalog = MultiLayerTemplateCatalogLoader.Load();

        Assert.True(catalog.Version >= 1);
        Assert.NotEmpty(catalog.Templates);
        Assert.Contains(catalog.Templates, t => t.Id == "edm-dense-canvas-tree-matrix");
        Assert.Contains(catalog.Templates, t => t.Id == "quiet-ballad-hold-plus-pop");
        Assert.Contains(catalog.Templates, t => t.Id == "mid-energy-chase-and-accent-tree");
    }

    [Fact]
    public void Load_EveryTemplate_HasAtLeastOneLayerWithAValidRoleAndDutyRange()
    {
        var catalog = MultiLayerTemplateCatalogLoader.Load();

        foreach (var template in catalog.Templates)
        {
            Assert.NotEmpty(template.Layers);
            Assert.False(string.IsNullOrWhiteSpace(template.EnergyBand));
            Assert.NotEmpty(template.Props);

            foreach (var layer in template.Layers)
            {
                Assert.Contains(layer.Role, new[] { "subject", "support", "secondary" });
                Assert.False(string.IsNullOrWhiteSpace(layer.Effect));
                Assert.InRange(layer.DutyMin, 0.0, 1.0);
                Assert.InRange(layer.DutyMax, 0.0, 1.0);
                Assert.True(layer.DutyMin <= layer.DutyMax);
                Assert.False(string.IsNullOrWhiteSpace(layer.Cadence));
            }
        }
    }

    [Fact]
    public void Load_IsCached_ReturnsTheSameInstanceAcrossCalls()
    {
        var first = MultiLayerTemplateCatalogLoader.Load();
        var second = MultiLayerTemplateCatalogLoader.Load();

        Assert.Same(first, second);
    }

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        var catalog = MultiLayerTemplateCatalogLoader.Load();

        Assert.Null(catalog.Find("does-not-exist"));
    }

    private const string RealMetadataRoot = @"C:\Program Files\xLights\effectmetadata";

    [Fact]
    public void Load_EveryLayerEffect_IsARealXlightsEffectName()
    {
        if (!Directory.Exists(RealMetadataRoot))
        {
            // No real xLights install on this machine (CI, a dev box without xLights) -- nothing to
            // check here, same skip convention as RealXlightsInstallTests.
            return;
        }

        var vocabulary = EffectVocabularyBuilder.BuildFromDirectory(RealMetadataRoot);
        var catalog = MultiLayerTemplateCatalogLoader.Load();

        foreach (var template in catalog.Templates)
        {
            foreach (var layer in template.Layers)
            {
                Assert.True(vocabulary.Effects.ContainsKey(layer.Effect), $"Template '{template.Id}' layer effect '{layer.Effect}' is not a real xLights effect name.");
            }
        }
    }
}
