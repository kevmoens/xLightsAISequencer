namespace XlightsAI.Studio.Stems;

/// <summary>
/// The persisted, versioned decision document Step 044 requires -- this step's own user-visible
/// outcome, verbatim: "Saved audio/analysis artifact with timestamp provenance." Only ever constructed
/// by <see cref="SeparationQualityEvaluator.EvaluateAsync"/>, which performs the real separation runs
/// and real metric computation this report records -- never hand-built by a caller to fabricate a
/// passing result.
/// </summary>
/// <param name="Id">Stable identity for this evaluation run, for cache/versioning per Section 4.</param>
/// <param name="CreatedAtTicks">Caller-supplied wall-clock ticks (<see cref="DateTime.UtcNow"/>-style,
/// <c>Ticks</c>) this report was produced -- supplied explicitly rather than sampled internally inside
/// the evaluator, the same precedent <see cref="ModelFeasibilityReport.CreatedAtTicks"/> already set.</param>
/// <param name="ModelId">The <see cref="ModelAsset.Id"/> the measured clips were separated with.</param>
/// <param name="RecipeVersion">The <see cref="StemSeparationRecipe.RecipeVersion"/> used.</param>
/// <param name="ClipMetrics">Every measured clip/stem metric, passing or not -- a failing measurement is
/// kept, not deleted, so the decision remains auditable (same precedent as
/// <see cref="ModelFeasibilityReport.Candidates"/>).</param>
/// <param name="Thresholds">The exact acceptance bar every metric above was measured against.</param>
/// <param name="Hardware">The real, measured hardware/performance profile for this run.</param>
/// <param name="OverallPass">True only when <see cref="ClipMetrics"/> is non-empty and every one of its
/// entries has <see cref="SeparationQualityMetric.PassesThreshold"/> set.</param>
/// <param name="Decision">Free-text summary of the actual decision reached.</param>
public sealed record SeparationQualityReport(
    Guid Id,
    long CreatedAtTicks,
    string ModelId,
    string RecipeVersion,
    IReadOnlyList<SeparationQualityMetric> ClipMetrics,
    SeparationQualityThresholds Thresholds,
    HardwareProfile Hardware,
    bool OverallPass,
    string Decision)
{
    public int PassingClipCount => ClipMetrics.Count(m => m.PassesThreshold);
}
