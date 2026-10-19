namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 053's own analysis-recipe configuration an <see cref="ISectionBoundaryDetector"/> run
/// is keyed by. Recorded alongside every <see cref="SectionBoundaryAnalysis"/> it produces (Section 4:
/// "every cache key includes algorithm/model/settings versions").
/// </summary>
/// <param name="InitialNoveltyThreshold">
/// The bar-to-bar dissimilarity a candidate must meet or exceed to be proposed on the search's first
/// iteration, in <c>(0, 1]</c>. Starts strict (fewer, higher-confidence candidates) and is relaxed
/// toward <see cref="MinNoveltyThreshold"/> only if too few candidates are found (Step 053's own
/// "snap only when evidence supports beat grid" guidance, applied here as "propose only when evidence
/// supports a real change," relaxed deliberately and visibly rather than silently).
/// </param>
/// <param name="MinNoveltyThreshold">The floor the adaptive search will never relax past, in <c>[0, InitialNoveltyThreshold]</c>. Reaching this floor without satisfying <see cref="MinBoundaryCount"/> is this step's own "exhausted retry budget" condition.</param>
/// <param name="MinBoundaryCount">The number of non-trivial candidates (excluding the always-present bar-<c>0</c> entry) the search attempts to reach before declaring <see cref="SectionBoundaryErrorReport.Converged"/>. Must be non-negative.</param>
/// <param name="MaxRefinementIterations">The hard cap on adaptive-threshold search iterations -- "exhausted retry budget must not auto-commit" (Step 053's own failure/edge case, verbatim) is this field's own declared ceiling. Must be positive.</param>
/// <param name="RecipeId">Identifies which declared novelty-detection recipe this analysis was built with (mirrors <see cref="BarFingerprintSettings.RecipeId"/>'s own convention). This step's own single implemented recipe is <see cref="SectionBoundaryDetector.AdjacentCosineNoveltyRecipeId"/>.</param>
public sealed record SectionBoundarySettings(
    double InitialNoveltyThreshold,
    double MinNoveltyThreshold,
    int MinBoundaryCount,
    int MaxRefinementIterations,
    string RecipeId);
