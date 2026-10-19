namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 037's own quality-gate configuration layered on top of an already-built/corrected
/// <see cref="BeatGrid"/> (Steps 034/035) — it does not re-run analysis. A plain settings record,
/// following the same convention as <see cref="RhythmSettings"/>/<see cref="OnsetDetectionSettings"/>/
/// <see cref="TempoDetectionSettings"/>/<see cref="BeatGridSettings"/>/<see cref="RhythmGestureSettings"/>,
/// even though <see cref="IBeatGridQualityGate.Evaluate"/> itself is never cached (a pure, synchronous
/// recomputation from values already in hand — the same <see cref="IBeatGridEditor"/>/
/// <see cref="IRhythmGesturePlanner"/> precedent).
/// </summary>
/// <param name="LowConfidenceThreshold">
/// Minimum unit-normalized overall confidence (see <see cref="BeatGridQualityReport.OverallConfidence"/>)
/// a grid must meet to avoid being flagged low-confidence (Step 037's own required acceptance,
/// verbatim: "Low-confidence results are flagged"). Must be in <c>[0, 1]</c>.
/// </param>
/// <param name="DeviationHistogramBucketTicks">
/// Width, in ticks, of each <see cref="TimingDeviationBucket"/> bucket in the produced timing-deviation
/// histogram (Step 037's own implementation guidance, verbatim: "Add timing deviation histograms").
/// Must be positive.
/// </param>
public sealed record BeatGridQualityGateSettings(double LowConfidenceThreshold, long DeviationHistogramBucketTicks);
