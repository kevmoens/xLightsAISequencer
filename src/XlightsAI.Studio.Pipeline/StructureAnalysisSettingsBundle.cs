using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// One coherent set of Structure analysis-recipe settings for <see cref="StructureAnalysisRunner"/>. Unlike
/// <see cref="AudioAnalysisSettingsBundle"/>, these are musical/structural tuning knobs rather than
/// sample-rate-derived DSP parameters, so there is a single <see cref="Default"/> rather than a
/// per-sample-rate factory. Each field's value and the recipe it pairs with mirrors a real, already-tested
/// choice from this repository's own Structure acceptance tests (see each settings type's own doc comment
/// for the recipe id constants these reference) rather than an untested guess.
/// </summary>
public sealed record StructureAnalysisSettingsBundle(
    BarFingerprintSettings BarFingerprint,
    SectionBoundarySettings SectionBoundary,
    SectionLabelSettings SectionLabel,
    PhraseSegmentationSettings PhraseSegmentation,
    MotifClusteringSettings MotifClustering)
{
    public static StructureAnalysisSettingsBundle Default { get; } = new(
        BarFingerprint: new BarFingerprintSettings(ChromaBins: 12, RecipeId: BarFingerprintBuilder.EnergyPlusChromaRecipeId),
        SectionBoundary: new SectionBoundarySettings(
            InitialNoveltyThreshold: 0.6, MinNoveltyThreshold: 0.3, MinBoundaryCount: 1, MaxRefinementIterations: 10,
            RecipeId: SectionBoundaryDetector.AdjacentCosineNoveltyRecipeId),
        SectionLabel: new SectionLabelSettings(
            RepetitionSimilarityThreshold: 0.85, MinConfidenceToLabel: 0.5,
            RecipeId: SectionLabelClassifier.RepetitionAndPositionRecipeId),
        PhraseSegmentation: new PhraseSegmentationSettings(
            TargetPhraseLengthBars: 8, CadenceSearchRadiusBars: 1, MinCadenceGapTicks: 2_000_000L,
            RecipeId: PhraseSegmenter.BarHypothesisWithCadenceRecipeId),
        MotifClustering: new MotifClusteringSettings(
            CanonicalSampleCount: 8, SimilarityThreshold: 0.8, RecipeId: MotifClusterer.TimeScaledCosineRecipeId));
}
