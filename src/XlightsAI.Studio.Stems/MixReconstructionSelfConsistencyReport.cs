namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044E. Step 044's own quality mechanism (<see cref="SeparationQualityEvaluator"/>/
/// <see cref="QualityClip"/>) assumes a genuine, known-correct reference stem file exists per stem kind --
/// no such reference exists for real musical separation (no professionally-mastered multitrack exists to
/// score against; the same honest constraint Step 044 itself already named). This is a disclosed, new,
/// deliberately thin report instead -- it reuses <see cref="SeparationQualityMetricCalculator.Compute"/>
/// (the pure, generic calculator -- any two equal-length float arrays) and the exact same
/// <see cref="HardwareProfile"/> measurement pattern <see cref="SeparationQualityEvaluator"/> already
/// established, completely unmodified, never asserting separation-quality parity.
/// </summary>
/// <param name="Id">Stable identity for this evaluation run.</param>
/// <param name="CreatedAtTicks">Caller-supplied wall-clock ticks this report was produced -- supplied
/// explicitly rather than sampled internally, the same precedent <see cref="ModelFeasibilityReport.CreatedAtTicks"/>
/// and <see cref="SeparationQualityReport.CreatedAtTicks"/> already set.</param>
/// <param name="ModelId">The <see cref="ModelAsset.Id"/> (<see cref="SpectrogramModelBundle.Archive"/>) the measured clip was separated with.</param>
/// <param name="RecipeVersion">The <see cref="SpectrogramSeparationRecipe.RecipeVersion"/> used.</param>
/// <param name="ReconstructionMetric">The real, measured energy/correlation self-consistency metric between
/// the reconstructed (summed-stems) mix and the real original input mix. <b>Not</b> a separation-quality
/// measurement -- see this record's own type doc comment and <see cref="Decision"/>.</param>
/// <param name="Hardware">The real, measured hardware/performance profile for this specific run.</param>
/// <param name="Decision">Free-text summary of the actual decision reached. Must never claim this proxy
/// matches reference/professional separation quality -- Step 044's own implementation guidance, verbatim:
/// "reject parity claims unsupported by results."</param>
public sealed record MixReconstructionSelfConsistencyReport(
    Guid Id,
    long CreatedAtTicks,
    string ModelId,
    string RecipeVersion,
    SeparationQualityMetric ReconstructionMetric,
    HardwareProfile Hardware,
    string Decision);
