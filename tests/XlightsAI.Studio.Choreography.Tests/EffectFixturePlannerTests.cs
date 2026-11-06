namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 18 §2 (Phase A). Same "real data by necessity, not choice" convention as
/// <see cref="LookCompilerTests"/>/<see cref="ChoreographyLegalityValidatorTests"/> -- characterizing a
/// *real* effect vocabulary against invented effect names/parameters would prove nothing, so these tests
/// skip cleanly (never fail) when <see cref="RealXLightsFixture.IsAvailable"/> is false.
/// </summary>
public sealed class EffectFixturePlannerTests
{
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
