namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 056's own analysis-recipe configuration an <see cref="IMotifClusterer"/> run is keyed
/// by. Recorded alongside every <see cref="MotifClusteringAnalysis"/> it produces (Section 4: "every cache
/// key includes algorithm/model/settings versions").
/// </summary>
/// <param name="CanonicalSampleCount">
/// How many evenly-spaced bars each phrase's own per-bar <see cref="BarFingerprint.Features"/> sequence is
/// resampled to before two phrases are compared -- "robust time-scaling" (this step's own implementation
/// guidance, verbatim): a phrase of any actual bar count is mapped to this same fixed-length sequence via
/// nearest-neighbor resampling, so an eight-bar phrase and (for example) a nine-bar variation of the same
/// musical idea still produce directly comparable embeddings. Must be positive.
/// </param>
/// <param name="SimilarityThreshold">
/// The minimum cosine similarity (in <c>[0, 1]</c>) between two phrases' own resampled embeddings for a
/// <see cref="MotifLinkCandidate"/> to be proposed between them -- "tolerance for variation" (this step's
/// own title, verbatim): less than <c>1.0</c>, so a close-but-not-byte-identical repeat is still linked.
/// </param>
/// <param name="RecipeId">Identifies which declared clustering recipe this analysis was built with (mirrors <see cref="PhraseSegmentationSettings.RecipeId"/>'s own convention). This step's own single implemented recipe is <see cref="MotifClusterer.TimeScaledCosineRecipeId"/>.</param>
public sealed record MotifClusteringSettings(
    int CanonicalSampleCount,
    double SimilarityThreshold,
    string RecipeId);
