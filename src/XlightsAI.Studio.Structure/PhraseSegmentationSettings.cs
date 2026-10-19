namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 055's own analysis-recipe configuration an <see cref="IPhraseSegmenter"/> run is keyed
/// by. Recorded alongside every <see cref="PhraseSegmentationAnalysis"/> it produces (Section 4: "every
/// cache key includes algorithm/model/settings versions").
/// </summary>
/// <param name="TargetPhraseLengthBars">
/// The bar-based hypothesis's own target phrase length, in bars. Must be positive. A section shorter
/// than or equal to this value is a single irregular-length phrase spanning the whole section (Step
/// 055's own implementation guidance, verbatim: "permit irregular length... only by deliberate policy" --
/// this is this recipe's own declared, disclosed instance of that policy).
/// </param>
/// <param name="CadenceSearchRadiusBars">
/// How many bars on either side of a bar-hypothesis split point a melodic cadence is searched for, in
/// <c>[0, ...)</c>. <c>0</c> disables cadence snapping entirely (every internal boundary falls back to
/// the plain bar-hypothesis grid).
/// </param>
/// <param name="MinCadenceGapTicks">
/// The minimum gap (in ticks) between the end of one melody note and the start of the next for that gap
/// to count as a melodic cadence (a phrase-ending rest), in <c>[0, ...)</c>.
/// </param>
/// <param name="RecipeId">Identifies which declared segmentation recipe this analysis was built with (mirrors <see cref="BarFingerprintSettings.RecipeId"/>'s own convention). This step's own single implemented recipe is <see cref="PhraseSegmenter.BarHypothesisWithCadenceRecipeId"/>.</param>
public sealed record PhraseSegmentationSettings(
    int TargetPhraseLengthBars,
    int CadenceSearchRadiusBars,
    long MinCadenceGapTicks,
    string RecipeId);
