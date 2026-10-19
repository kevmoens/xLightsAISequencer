namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 044 — "Compare reference outputs, audio metrics and listening tests; establish
/// supported hardware." Runs a real <see cref="IStemSeparator"/> against every supplied
/// <see cref="QualityClip"/>, scores the real produced stems against their known-correct reference
/// audio, and measures real wall-clock/memory hardware figures for the run -- never a mocked metric.
/// </summary>
public interface ISeparationQualityEvaluator
{
    Task<SeparationQualityReport> EvaluateAsync(
        Guid reportId,
        long createdAtTicks,
        IStemSeparator separator,
        ModelAsset model,
        string recipeVersion,
        IReadOnlyList<QualityClip> clips,
        SeparationQualityThresholds thresholds,
        CancellationToken ct);
}
