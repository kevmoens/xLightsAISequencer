namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 054's own analysis-recipe configuration an <see cref="ISectionLabelClassifier"/> run is
/// keyed by. Recorded alongside every <see cref="SectionLabelAnalysis"/> it produces (Section 4: "every
/// cache key includes algorithm/model/settings versions").
/// </summary>
/// <param name="RepetitionSimilarityThreshold">
/// The minimum cosine similarity (Step 052's own <see cref="FingerprintSimilarity.Cosine"/>) between two
/// segments' averaged fingerprints for them to be judged the same recurring idea, in <c>(0, 1]</c>. Only
/// repetition this strong is treated as chorus-identifying evidence -- incidental resemblance (e.g. same
/// chords, different melody) must not auto-accept a motif, mirroring Section 7 test scenario 2.
/// </param>
/// <param name="MinConfidenceToLabel">
/// The minimum <see cref="SectionLabelCandidate.Confidence"/> a classification must reach to be assigned
/// its computed <see cref="SectionKind"/>; below this floor the segment is labeled
/// <see cref="SectionKind.Unknown"/> instead -- this step's own declared, bounded way "Unknown is valid;
/// never force classical song structure" is enforced structurally rather than left to chance. In <c>[0, 1]</c>.
/// </param>
/// <param name="RecipeId">Identifies which declared classification recipe this analysis was built with (mirrors <see cref="BarFingerprintSettings.RecipeId"/>'s own convention). This step's own single implemented recipe is <see cref="SectionLabelClassifier.RepetitionAndPositionRecipeId"/>.</param>
public sealed record SectionLabelSettings(
    double RepetitionSimilarityThreshold,
    double MinConfidenceToLabel,
    string RecipeId);
