namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.1: <see cref="BlendModeCompatibilityIndex"/> must report only real, mined
/// evidence (never a guessed/default candidate for a pair the catalog has never actually seen), prefer
/// stronger (corpus-validated, more frequent) evidence, and use the existing
/// <see cref="EffectVisualCharacterCatalog"/> brightness signal only as a tie-breaker, never to invent a
/// candidate the catalog itself has no mined support for.
/// </summary>
public sealed class BlendModeCompatibilityIndexTests
{
    private static EffectVisualCharacterCatalog BuildBrightnessCatalog() => new(
        DateTimeOffset.UtcNow,
        [
            new EffectVisualCharacter("On", "High", "d", DateTimeOffset.UtcNow, BrightnessTier: BrightnessTier.Bright),
            new EffectVisualCharacter("Twinkle", "High", "d", DateTimeOffset.UtcNow, BrightnessTier: BrightnessTier.Dim),
            new EffectVisualCharacter("Shockwave", "High", "d", DateTimeOffset.UtcNow, BrightnessTier: BrightnessTier.Bright),
        ]);

    private static MultiLayerTemplate Template(string id, string confidenceTier, params (string Effect, string? Blend)[] layers) => new(
        Id: id, Name: id, EnergyBand: "mid", Props: ["Any"], Motion: "chase",
        Layers: layers.Select(l => new MultiLayerTemplateLayer("support", l.Effect, 0.1, 0.9, "beat", "note", l.Blend)).ToList(),
        SourceNote: "fixture", ConfidenceTier: confidenceTier);

    [Fact]
    public void ScoresFor_ReturnsEmpty_WhenCatalogHasNoMinedEvidenceForThisExactPair()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow,
            [Template("t1", "corpus-validated", ("On", null), ("Twinkle", "Mask"))]);

        var scores = BlendModeCompatibilityIndex.ScoresFor(catalog, BuildBrightnessCatalog(), "Twinkle", "Shockwave");

        Assert.Empty(scores);
    }

    [Fact]
    public void SelectBlendMode_ReturnsNull_WhenNoRealEvidenceExists()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow, []);
        Assert.Null(BlendModeCompatibilityIndex.SelectBlendMode(catalog, BuildBrightnessCatalog(), "On", "Twinkle"));
    }

    [Fact]
    public void SelectBlendMode_PrefersTheCandidateWithMoreMinedTemplateSupport()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow,
        [
            Template("t1", "corpus-validated", ("On", null), ("Twinkle", "Mask")),
            Template("t2", "corpus-validated", ("On", null), ("Twinkle", "Mask")),
            Template("t3", "corpus-validated", ("On", null), ("Twinkle", "Average")),
        ]);

        var selected = BlendModeCompatibilityIndex.SelectBlendMode(catalog, BuildBrightnessCatalog(), "On", "Twinkle");

        Assert.Equal("Mask", selected);
    }

    [Fact]
    public void ScoresFor_WeighsCorpusValidatedEvidence_AboveSingleSongShowcase()
    {
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow,
        [
            Template("mined-1", "corpus-validated", ("On", null), ("Twinkle", "Mask")),
            Template("showcase-1", "single-song-showcase", ("On", null), ("Twinkle", "Average")),
        ]);

        var scores = BlendModeCompatibilityIndex.ScoresFor(catalog, BuildBrightnessCatalog(), "On", "Twinkle");

        Assert.Equal("Mask", scores[0].CandidateName);
        Assert.True(scores[0].Weight > scores[1].Weight);
    }

    [Fact]
    public void ScoresFor_NeverReturnsAnUnconfirmedCandidateName_EvenIfATemplateSomehowCarriedOne()
    {
        // Defensive: BlendModeToPrevious is only ever supposed to be a confirmed candidate name, but this
        // index must never silently forward a corrupted/foreign value as if it were real evidence.
        var catalog = new MultiLayerTemplateCatalog(1, DateTimeOffset.UtcNow,
            [Template("t1", "corpus-validated", ("On", null), ("Twinkle", "Mask"))]);

        var scores = BlendModeCompatibilityIndex.ScoresFor(catalog, BuildBrightnessCatalog(), "On", "Twinkle");

        Assert.All(scores, s => Assert.True(ConfirmedLayerBlendModeCatalog.Find(s.CandidateName) is not null));
    }
}
