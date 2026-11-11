using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 18 §6 (Phase E): converting a raw Phase D <see cref="EffectVisualCharacterizationManifest"/>
/// into the shippable <see cref="Knowledge.EffectVisualCharacterCatalog"/> must exclude the hardware-only
/// effects, never drop a non-hardware effect silently, and attach the real documented behavior only to the
/// specific effects (and, for VU Meter, the specific tier) the build plan names.
/// </summary>
public sealed class EffectVisualCharacterCatalogBuilderTests
{
    private static EffectVisualCharacterizationDescription Description(string summary) =>
        new("chase", "left-to-right", "medium", "moderate", summary);

    private static EffectVisualCharacterizationOutcome Characterized(
        string effectName, EffectFixtureResolutionTier tier, string summary, double meanBrightness = 0.5, double peakBrightness = 0.5) =>
        new(effectName, tier, EffectVisualCharacterizationStatus.Characterized, FailureReason: null, Description(summary), WallClockSeconds: 1.0,
            meanBrightness, peakBrightness);

    private static EffectVisualCharacterizationOutcome NotCharacterized(string effectName, EffectFixtureResolutionTier tier) =>
        new(effectName, tier, EffectVisualCharacterizationStatus.AiCallFailed, FailureReason: "provider error", Description: null, WallClockSeconds: 1.0);

    [Theory]
    [InlineData("DMX")]
    [InlineData("Servo")]
    [InlineData("Moving Head")]
    public void Build_ExcludesHardwareEffects_EvenWhenPresentAndCharacterizedInSourceManifest(string hardwareEffectName)
    {
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model",
        [
            Characterized(hardwareEffectName, EffectFixtureResolutionTier.Low, "completely dark, no visible motion"),
            Characterized(hardwareEffectName, EffectFixtureResolutionTier.High, "completely dark, no visible motion"),
        ]);

        var catalog = EffectVisualCharacterCatalogBuilder.Build(manifest);

        Assert.Empty(catalog.Characters);
    }

    [Fact]
    public void Build_ExcludesOutcomesThatWereNeverCharacterized()
    {
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model",
        [
            NotCharacterized("Twinkle", EffectFixtureResolutionTier.Low),
            Characterized("Twinkle", EffectFixtureResolutionTier.High, "sparse sparkling points"),
        ]);

        var catalog = EffectVisualCharacterCatalogBuilder.Build(manifest);

        var entry = Assert.Single(catalog.Characters);
        Assert.Equal("High", entry.ResolutionTier);
    }

    [Fact]
    public void Build_IncludesAnOrdinaryEffect_WithNoDocumentedBehavior()
    {
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model",
        [
            Characterized("Twinkle", EffectFixtureResolutionTier.Low, "sparse sparkling points"),
        ]);

        var catalog = EffectVisualCharacterCatalogBuilder.Build(manifest);

        var entry = Assert.Single(catalog.Characters);
        Assert.Equal("Twinkle", entry.EffectName);
        Assert.Equal("sparse sparkling points", entry.Description);
        Assert.Null(entry.DocumentedBehavior);
        Assert.Null(entry.DocumentationSource);
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
    public void Build_DocumentedBehaviorEffects_CarryTheRealManualQuoteOnBothTiers(string effectName)
    {
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model",
        [
            Characterized(effectName, EffectFixtureResolutionTier.Low, "completely dark, no visible motion"),
            Characterized(effectName, EffectFixtureResolutionTier.High, "completely dark, no visible motion"),
        ]);

        var catalog = EffectVisualCharacterCatalogBuilder.Build(manifest);

        Assert.Equal(2, catalog.Characters.Count);
        foreach (var entry in catalog.Characters)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.DocumentedBehavior));
            Assert.StartsWith("https://manual.xlights.org/", entry.DocumentationSource);
            // The real AI-observed description is still carried, never discarded in favor of the quote.
            Assert.Equal("completely dark, no visible motion", entry.Description);
        }
    }

    [Fact]
    public void Build_VuMeter_DocumentedBehaviorAppliesOnlyToLowTier()
    {
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model",
        [
            Characterized("VU Meter", EffectFixtureResolutionTier.Low, "completely dark, no visible motion"),
            Characterized("VU Meter", EffectFixtureResolutionTier.High, "a real responsive volume bar"),
        ]);

        var catalog = EffectVisualCharacterCatalogBuilder.Build(manifest);

        var low = catalog.Characters.Single(c => c.ResolutionTier == "Low");
        var high = catalog.Characters.Single(c => c.ResolutionTier == "High");

        Assert.False(string.IsNullOrWhiteSpace(low.DocumentedBehavior));
        Assert.Null(high.DocumentedBehavior);
        Assert.Null(high.DocumentationSource);
    }

    /// <summary>Build-plan Phase 21 §2c: every catalog entry must carry its real, measured brightness -- never null, never a placeholder.</summary>
    [Fact]
    public void Build_EveryEntry_CarriesItsRealMeasuredBrightness()
    {
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model",
        [
            Characterized("Twinkle", EffectFixtureResolutionTier.Low, "sparse sparkling points", meanBrightness: 0.03, peakBrightness: 0.1),
        ]);

        var catalog = EffectVisualCharacterCatalogBuilder.Build(manifest);

        var entry = Assert.Single(catalog.Characters);
        Assert.Equal(0.03, entry.MeanBrightness);
        Assert.Equal(0.1, entry.PeakBrightness);
    }

    /// <summary>Build-plan Phase 21 §2a: a Characterized outcome with no measured brightness is a data-integrity bug (brightness is computed before the AI call ever runs), never silently accepted as a null/placeholder tier.</summary>
    [Fact]
    public void Build_CharacterizedOutcomeWithNoMeasuredBrightness_Throws()
    {
        var unmeasured = new EffectVisualCharacterizationOutcome(
            "Twinkle", EffectFixtureResolutionTier.Low, EffectVisualCharacterizationStatus.Characterized, FailureReason: null,
            Description("sparse sparkling points"), WallClockSeconds: 1.0, MeanBrightness: null, PeakBrightness: null);
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model", [unmeasured]);

        Assert.Throws<InvalidOperationException>(() => EffectVisualCharacterCatalogBuilder.Build(manifest));
    }

    /// <summary>Build-plan Phase 21 §2b: two real cut points -- the 33rd/66th percentiles -- bucket MeanBrightness into Off/Dim/Bright.</summary>
    [Fact]
    public void ComputeBrightnessCutPoints_DerivesTercilesFromTheRealDistribution()
    {
        // 10 evenly-spaced values 0.0..0.9 -- the 1/3 and 2/3 interpolated ranks land at 0.3 and 0.6.
        var values = Enumerable.Range(0, 10).Select(i => i / 10.0).ToList();

        var (cut1, cut2) = EffectVisualCharacterCatalogBuilder.ComputeBrightnessCutPoints(values);

        Assert.Equal(0.3, cut1, 5);
        Assert.Equal(0.6, cut2, 5);
    }

    [Fact]
    public void Build_BucketsMeanBrightnessIntoOffDimBright_UsingTheRealDerivedCutPoints()
    {
        var manifest = new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, "test-model",
        [
            Characterized("A", EffectFixtureResolutionTier.Low, "dark", meanBrightness: 0.0),
            Characterized("B", EffectFixtureResolutionTier.Low, "mid", meanBrightness: 0.5),
            Characterized("C", EffectFixtureResolutionTier.Low, "bright", meanBrightness: 1.0),
        ]);

        var catalog = EffectVisualCharacterCatalogBuilder.Build(manifest);

        Assert.Equal(BrightnessTier.Off, catalog.Characters.Single(c => c.EffectName == "A").BrightnessTier);
        Assert.Equal(BrightnessTier.Dim, catalog.Characters.Single(c => c.EffectName == "B").BrightnessTier);
        Assert.Equal(BrightnessTier.Bright, catalog.Characters.Single(c => c.EffectName == "C").BrightnessTier);
    }
}
