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
            RecipeId: SectionBoundaryDetector.AdjacentCosineNoveltyRecipeId,
            // User feedback (2026-11-04): a real 50-bar song converged after one iteration with one
            // section spanning 33 of those 50 bars (66%) -- MinBoundaryCount alone could not catch this,
            // since a handful of very obvious transitions already satisfied it. 0.35 means no single
            // section is allowed to dominate more than a third of the song before the search is required
            // to keep relaxing toward MinNoveltyThreshold looking for real internal structure.
            MaxSectionSpanFraction: 0.35),
        SectionLabel: new SectionLabelSettings(
            RepetitionSimilarityThreshold: 0.85, MinConfidenceToLabel: 0.5,
            RecipeId: SectionLabelClassifier.RepetitionAndPositionRecipeId),
        PhraseSegmentation: new PhraseSegmentationSettings(
            TargetPhraseLengthBars: 8, CadenceSearchRadiusBars: 1, MinCadenceGapTicks: 2_000_000L,
            RecipeId: PhraseSegmenter.BarHypothesisWithCadenceRecipeId),
        MotifClustering: new MotifClusteringSettings(
            CanonicalSampleCount: 8, SimilarityThreshold: 0.8, RecipeId: MotifClusterer.TimeScaledCosineRecipeId));
}
