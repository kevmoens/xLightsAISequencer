using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 25 Part D.1/D.2: hermetic unit tests for the shared <see cref="LayerComplexityAnalyzer"/>
/// sweep -- the single primitive now reused by <see cref="MultiLayerStackMiner"/>, the training-only
/// <see cref="LayerComplexityMiner"/>, and <see cref="ExemplarIndexBuilder"/>'s own per-phrase-window
/// complexity field.
/// </summary>
public sealed class LayerComplexityAnalyzerTests
{
    [Fact]
    public void PeakConcurrentLayerCount_CountsDistinctLayerIndexValues_NotRawOverlapCount()
    {
        // Two placements on the SAME layer that happen to overlap (e.g. a re-triggered effect) must
        // count as one layer, not two -- distinguishing this from CraftRuleInducer.MaxConcurrentOverlap.
        var effects = Aligned(
            Effect("Model1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Model1", "On", layerIndex: 0, start: 2_000, end: 8_000),
            Effect("Model1", "Twinkle", layerIndex: 1, start: 0, end: 10_000));

        Assert.Equal(2, LayerComplexityAnalyzer.PeakConcurrentLayerCount(effects));
    }

    [Fact]
    public void PeakConcurrentLayerCount_MaxesAcrossModels_NeverSumsThem()
    {
        var effects = Aligned(
            Effect("Model1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Model1", "Twinkle", layerIndex: 1, start: 0, end: 10_000),
            Effect("Model2", "On", layerIndex: 0, start: 0, end: 10_000));

        Assert.Equal(2, LayerComplexityAnalyzer.PeakConcurrentLayerCount(effects));
    }

    [Fact]
    public void PeakConcurrentLayerCount_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0, LayerComplexityAnalyzer.PeakConcurrentLayerCount([]));
    }

    [Fact]
    public void FindLayerWindows_ExcludesRunsBelowTheMinimum()
    {
        var effects = Aligned(
            Effect("Model1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Model1", "Twinkle", layerIndex: 1, start: 0, end: 10_000));

        Assert.Empty(LayerComplexityAnalyzer.FindLayerWindows(effects, minDistinctLayers: 3));
        Assert.Single(LayerComplexityAnalyzer.FindLayerWindows(effects, minDistinctLayers: 2));
    }

    [Fact]
    public void FindLayerWindows_ReportsDominantSectionKindAndPeakCount()
    {
        var effects = Aligned(
            Effect("Model1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Model1", "Twinkle", layerIndex: 1, start: 0, end: 10_000),
            Effect("Model1", "Shockwave", layerIndex: 2, start: 2_000, end: 6_000));

        var window = Assert.Single(LayerComplexityAnalyzer.FindLayerWindows(effects, minDistinctLayers: 3));
        Assert.Equal(3, window.PeakConcurrentLayerCount);
        Assert.Equal(SectionKind.Chorus, window.DominantSectionKind);
    }

    private static IReadOnlyList<AlignedEffectInstance> Aligned(params ParsedEffectInstance[] effects) =>
        effects.Select(e => new AlignedEffectInstance(e, StartBarIndex: 0, StartBeatWithinBar: 0, OffsetFromNearestBeatMs: null, SectionKind: SectionKind.Chorus, PhraseStartBarIndex: 0, PhraseBars: 8)).ToList();

    private static ParsedEffectInstance Effect(string modelName, string effectName, int layerIndex, long start, long end) => new(
        modelName, "model", effectName, StartTimeMs: start, EndTimeMs: end,
        LayerIndex: layerIndex, PaletteIndex: 0, SettingsString: "", PaletteString: "");
}
