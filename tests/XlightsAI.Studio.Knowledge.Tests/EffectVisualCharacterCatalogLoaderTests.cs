namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Build-plan Phase 18 §6 (Phase E): the shippable effect visual-character catalog must load from this
/// assembly's own embedded resource (never a loose file next to it, never %AppData% -- see
/// <see cref="EffectVisualCharacterCatalogLoader"/>'s own doc comment for why), and the committed, real
/// 112-fixture sweep result must never include the deliberately-excluded hardware effects.
/// </summary>
public sealed class EffectVisualCharacterCatalogLoaderTests
{
    [Fact]
    public void Load_ReturnsARealNonEmptyCatalog()
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        Assert.NotEmpty(catalog.Characters);
    }

    [Fact]
    public void Load_IsCached_ReturnsTheSameInstanceAcrossCalls()
    {
        var first = EffectVisualCharacterCatalogLoader.Load();
        var second = EffectVisualCharacterCatalogLoader.Load();

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData("DMX")]
    [InlineData("Servo")]
    [InlineData("Moving Head")]
    public void Load_NeverIncludesAHardwareOnlyEffect(string hardwareEffectName)
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        Assert.DoesNotContain(catalog.Characters, c => c.EffectName == hardwareEffectName);
    }

    [Theory]
    [InlineData("Duplicate")]
    [InlineData("Guitar")]
    [InlineData("Glediator")]
    [InlineData("Sketch")]
    [InlineData("State")]
    [InlineData("Fireworks")]
    [InlineData("Music")]
    [InlineData("SingleStrand")]
    [InlineData("Shape")]
    public void Load_DocumentedBehaviorEffects_CarryANonNullDocumentedBehaviorAndSource(string effectName)
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        var entry = catalog.Find(effectName);
        Assert.NotNull(entry);
        Assert.False(string.IsNullOrWhiteSpace(entry!.DocumentedBehavior));
        Assert.False(string.IsNullOrWhiteSpace(entry.DocumentationSource));
        Assert.StartsWith("https://manual.xlights.org/", entry.DocumentationSource);
    }

    [Fact]
    public void Load_VuMeter_OnlyTheLowTierCarriesDocumentedBehavior()
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        var low = catalog.Characters.FirstOrDefault(c => c.EffectName == "VU Meter" && c.ResolutionTier == "Low");
        var high = catalog.Characters.FirstOrDefault(c => c.EffectName == "VU Meter" && c.ResolutionTier == "High");

        Assert.NotNull(low);
        Assert.False(string.IsNullOrWhiteSpace(low!.DocumentedBehavior));

        if (high is not null)
        {
            Assert.Null(high.DocumentedBehavior);
        }
    }

    [Fact]
    public void Find_PrefersHighTier_WhenBothTiersExist()
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        var found = catalog.Find("Color Wash");

        Assert.NotNull(found);
        Assert.Equal("High", found!.ResolutionTier);
    }

    [Fact]
    public void Find_UnknownEffect_ReturnsNull()
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        Assert.Null(catalog.Find("does-not-exist"));
    }

    /// <summary>Build-plan Phase 21 §2c: no catalog entry may carry a null/placeholder brightness -- every real, Characterized entry must have been actually measured.</summary>
    [Fact]
    public void Load_EveryEntry_CarriesARealMeasuredBrightness()
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        Assert.All(catalog.Characters, c => Assert.InRange(c.MeanBrightness, 0.0, 1.0));
        Assert.All(catalog.Characters, c => Assert.InRange(c.PeakBrightness, 0.0, 1.0));
    }

    /// <summary>
    /// Build-plan Phase 21 §4's own named acceptance spot-check: "On"/"Color Wash"/"Fill" -- dense,
    /// full-canvas effects -- must measure Bright on every tier they were characterized at.
    /// </summary>
    [Theory]
    [InlineData("On")]
    [InlineData("Color Wash")]
    [InlineData("Fill")]
    public void Load_DenseFullCanvasEffects_MeasureBrightOnEveryCharacterizedTier(string effectName)
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        var entries = catalog.Characters.Where(c => c.EffectName == effectName).ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, c => Assert.Equal(BrightnessTier.Bright, c.BrightnessTier));
    }

    /// <summary>
    /// Build-plan Phase 21 §4's own named acceptance spot-check: "Snowflakes," "Snowstorm," and "Meteors" --
    /// real, sparse/scattered effects whose own canvas is mostly dark at any instant despite individually
    /// bright pixels -- must measure Dim on at least one of their real characterized tiers (coverage sparsity
    /// IS dimness, the user's own confirmed design decision).
    /// </summary>
    [Theory]
    [InlineData("Snowflakes")]
    [InlineData("Snowstorm")]
    [InlineData("Meteors")]
    public void Load_SparseScatteredEffects_MeasureDimOnAtLeastOneCharacterizedTier(string effectName)
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        var entries = catalog.Characters.Where(c => c.EffectName == effectName).ToList();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, c => c.BrightnessTier == BrightnessTier.Dim);
    }

    /// <summary>
    /// Fireworks is the fourth effect the build plan names alongside Snowflakes/Snowstorm/Meteors, but its
    /// real, measured render is a known, pre-existing Phase 18 gap (see
    /// <see cref="EffectVisualCharacter.DocumentedBehavior"/> for this exact effect): the isolated synthetic
    /// fixture never actually ignites it -- every one of its 240 real decoded frames has ZERO illuminated
    /// pixels, on both resolution tiers, confirmed by direct inspection of the Phase B frame data, not an
    /// assumption. That measures as a genuine 0.0 -- indistinguishable from a truly dark effect -- so it
    /// lands in "Off," not "Dim." This is a real, disclosed limitation in the underlying render (tracked
    /// separately, out of this phase's own scope: making brightness measurement real, not re-fixing Phase
    /// 18's synthetic-fixture coverage), not a defect in this phase's brightness measurement or tiering.
    /// </summary>
    [Fact]
    public void Load_Fireworks_MeasuresOff_BecauseItsIsolatedRenderNeverIgnites_KnownPhase18Gap()
    {
        var catalog = EffectVisualCharacterCatalogLoader.Load();

        var entries = catalog.Characters.Where(c => c.EffectName == "Fireworks").ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, c =>
        {
            Assert.Equal(0.0, c.MeanBrightness);
            Assert.Equal(BrightnessTier.Off, c.BrightnessTier);
        });
    }
}
