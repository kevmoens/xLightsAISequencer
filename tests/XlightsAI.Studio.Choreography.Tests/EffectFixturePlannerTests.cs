namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 18 §2 (Phase A). Same "real data by necessity, not choice" convention as
/// <see cref="LookCompilerTests"/>/<see cref="ChoreographyLegalityValidatorTests"/> -- characterizing a
/// *real* effect vocabulary against invented effect names/parameters would prove nothing, so these tests
/// skip cleanly (never fail) when <see cref="RealXLightsFixture.IsAvailable"/> is false.
/// </summary>
public sealed class EffectFixturePlannerTests
{
    /// <summary>
    /// Regression test for a real defect found building Phase 18 §4 (Phase C): both fixture models'
    /// <see cref="Xlights.DisplayModel.StringType"/> previously defaulted to <c>""</c> instead of
    /// <c>"RGB Nodes"</c>, so <see cref="Rendering.RenderedFrameProjector"/> silently skipped every one of
    /// the 112 real fixtures in a full sweep, decoding every frame with zero illuminated points. No real
    /// xLights install needed -- this only inspects the in-memory <see cref="DisplayGraph"/>.
    /// </summary>
    [Fact]
    public void BuildFixtureLayout_BothModels_DeclareRgbNodesStringType()
    {
        var layout = EffectFixturePlanner.BuildFixtureLayout();

        Assert.Equal(2, layout.Models.Count);
        foreach (var model in layout.Models)
        {
            Assert.Equal("RGB Nodes", model.StringType);
        }
    }

    /// <summary>
    /// Regression test for a real defect found investigating why 38/112 real fixtures rendered completely
    /// dark: several effects' own real metadata (Shape/Kaleidoscope/Circles/Warp/Lines/Morph/Life/Fireworks)
    /// describe genuinely 2D-plane behaviour, but <see cref="EffectFixtureSyntheticShow"/>'s own High-tier
    /// model was a 1-row "Single Line" -- verified by direct inspection of its own exported XML. The High
    /// tier is now a real <see cref="EffectFixturePlanner.HighResolutionMatrixColumns"/>x<see cref="EffectFixturePlanner.HighResolutionMatrixRows"/>
    /// Matrix so <see cref="Rendering.RenderedFrameProjector"/>'s own Matrix fan-out can place nodes across
    /// real 2D space; the Low tier intentionally keeps its Single Line shape (a real low-resolution string
    /// prop legitimately has no height either).
    /// </summary>
    [Fact]
    public void BuildFixtureLayout_HighResolutionModel_IsARealTwoDimensionalMatrix()
    {
        var layout = EffectFixturePlanner.BuildFixtureLayout();

        var high = Assert.Single(layout.Models, m => m.XlightsName == EffectFixturePlanner.HighResolutionModel.XlightsName);
        Assert.Equal(Xlights.ModelCategory.Matrix, high.Category);
        Assert.Equal(EffectFixturePlanner.HighResolutionMatrixColumns, high.Parm1);
        Assert.Equal(EffectFixturePlanner.HighResolutionMatrixRows, high.Parm2);
        Assert.Equal(EffectFixturePlanner.HighResolutionMatrixColumns * EffectFixturePlanner.HighResolutionMatrixRows, high.NodeCount);

        var low = Assert.Single(layout.Models, m => m.XlightsName == EffectFixturePlanner.LowResolutionModel.XlightsName);
        Assert.Equal(Xlights.ModelCategory.SingleLine, low.Category);
    }

    [Fact]
    public void Build_EveryRealEffect_ProducesLowAndHighTierFixtures()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();

        var manifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);

        Assert.Equal(vocabulary.Effects.Count * 2, manifest.Fixtures.Count);
        foreach (var effectName in vocabulary.Effects.Keys)
        {
            Assert.Contains(manifest.Fixtures, f => f.EffectName == effectName && f.ResolutionTier == EffectFixtureResolutionTier.Low);
            Assert.Contains(manifest.Fixtures, f => f.EffectName == effectName && f.ResolutionTier == EffectFixtureResolutionTier.High);
        }
    }

    [Fact]
    public void Build_NeverSilentlySkipsAnEffect_EveryFixtureRecordsASuccessOrAnExplicitFailureReason()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();

        var manifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);

        foreach (var fixture in manifest.Fixtures)
        {
            if (fixture.CompiledSuccessfully)
            {
                Assert.Null(fixture.CompileFailureReason);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(fixture.CompileFailureReason));
            }
        }

        Assert.Equal(manifest.Fixtures.Count, manifest.SucceededCount + manifest.FailedCount);
    }

    [Fact]
    public void Build_WellKnownOnEffect_CompilesSuccessfullyOnBothTiers()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();

        var manifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);

        var onFixtures = manifest.Fixtures.Where(f => f.EffectName == "On").ToList();
        Assert.Equal(2, onFixtures.Count);
        Assert.All(onFixtures, f => Assert.True(f.CompiledSuccessfully, f.CompileFailureReason));
    }

    [Fact]
    public async Task JsonEffectFixtureManifestStore_SaveThenLoad_RoundTripsTheManifest()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var manifest = EffectFixturePlanner.Build(vocabulary, recipes, RealXLightsFixture.EffectMetadataRoot);

        var directory = Path.Combine(Path.GetTempPath(), $"xlai-effect-fixture-manifest-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonEffectFixtureManifestStore(directory);
            await store.SaveAsync(manifest, CancellationToken.None);

            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(manifest.Fixtures.Count, loaded.Fixtures.Count);
            Assert.Equal(manifest.SucceededCount, loaded.SucceededCount);
            Assert.Equal(manifest.EffectMetadataDirectory, loaded.EffectMetadataDirectory);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
