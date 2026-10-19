namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 037's own bounded, non-destructive auto-correction attempt loop: given a <see cref="BeatGrid"/>
/// and a caller-supplied, ordered list of candidate <see cref="IBeatGridEditor"/> corrections, tries
/// each in turn — committing the first one that actually, measurably improves
/// <see cref="BeatGridQualityReport.OverallConfidence"/> — and never silently commits a malformed
/// proposal, a proposal that does not improve quality, or any proposal once the caller's own retry
/// budget is exhausted (this step's own failure/edge case, verbatim: "Malformed proposal, no-improvement
/// and exhausted retry budget must not auto-commit"). This is explicitly an "optional structured AI
/// planner"-style validated proposal boundary (Section 2: "AI trust... optional local or BYOK cloud AI
/// returns validated structured proposals only... do not execute... directly") — every proposal is only
/// ever applied by calling the real, already-tested <see cref="IBeatGridEditor"/>, never by fabricating
/// an edit of its own.
/// </summary>
public interface IBeatGridAutoCorrectionLoop
{
    /// <exception cref="ArgumentNullException"><paramref name="grid"/>, <paramref name="proposals"/> or <paramref name="qualitySettings"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeatGridCorrectionInvalidException"><paramref name="maxAttempts"/> is not positive.</exception>
    BeatGridCorrectionOutcome TryApply(
        BeatGrid grid,
        IReadOnlyList<BeatGridCorrectionProposal> proposals,
        BeatGridQualityGateSettings qualitySettings,
        int maxAttempts);
}
