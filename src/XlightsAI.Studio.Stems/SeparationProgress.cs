namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Section 3's own proposed signature. Step 040 reports coarse, whole-clip-pass stages only
/// (no chunk-level granularity yet -- chunking/progress-per-chunk is Step 041's own disclosed scope).
/// </summary>
/// <param name="Fraction">Overall progress in <c>[0, 1]</c>.</param>
/// <param name="Stage">Human-readable current stage name (e.g. "Preprocessing", "Inference", "WritingStems").</param>
/// <param name="ProcessedSamples">PCM sample frames processed so far for the current stage, where applicable; <c>0</c> for stages that are not sample-granular.</param>
public sealed record SeparationProgress(double Fraction, string Stage, long ProcessedSamples);
