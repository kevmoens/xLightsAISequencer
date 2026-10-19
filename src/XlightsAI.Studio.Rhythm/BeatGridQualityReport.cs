namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 037's own persisted-in-memory diagnostics output: never written to disk by this step (a pure
/// recomputation, like <see cref="IBeatGridEditor"/>'s own outputs), but carries the exact
/// <see cref="BeatGrid.VersionId"/>/<see cref="BeatGrid.CorrectionVersion"/> pair it was computed from,
/// so a caller can tell whether a previously-shown report is stale after a later Step 035 correction.
/// </summary>
/// <param name="BeatGridVersionId">The exact <see cref="BeatGrid.VersionId"/> this report was computed from.</param>
/// <param name="CorrectionVersion">The exact <see cref="BeatGrid.CorrectionVersion"/> this report was computed from — a later authorized correction (Step 035) always invalidates a previously computed report.</param>
/// <param name="BeatCount"><see cref="BeatGrid.Beats"/>'s own length at evaluation time.</param>
/// <param name="FirstBeatOffsetTicks">Step 037's own implementation guidance, verbatim: "first-beat offset" — the grid's own earliest beat's <see cref="BeatMarker.TimeTicks"/>, or <see langword="null"/> when <see cref="BeatCount"/> is <c>0</c> (this step's own "silence... must be handled explicitly" edge case).</param>
/// <param name="OverallConfidence">Unit-normalized <c>[0, 1]</c> average of every beat's own effective confidence (<see cref="BeatMarker.UserCorrected"/> beats are treated as fully certain — the same <see cref="BeatGridEditor.UserCorrectedConfidence"/> convention Step 035 already established: a human's explicit correction is never reported as less certain than its own possibly-stale raw algorithmic <see cref="BeatMarker.Confidence"/>). <c>0.0</c>, not an exception, when <see cref="BeatCount"/> is <c>0</c>.</param>
/// <param name="IsLowConfidence"><see langword="true"/> when <see cref="BeatCount"/> is <c>0</c> or <see cref="OverallConfidence"/> is below the evaluating <see cref="BeatGridQualityGateSettings.LowConfidenceThreshold"/> — Step 037's own required acceptance, verbatim: "Low-confidence results are flagged."</param>
/// <param name="TimingDeviationHistogram">For every beat after the first, its absolute deviation from the position a perfectly constant tempo (anchored at the grid's own first beat) would predict, bucketed by <see cref="BeatGridQualityGateSettings.DeviationHistogramBucketTicks"/>. Empty when <see cref="BeatCount"/> is below <c>2</c> or <see cref="BeatGrid.SelectedTempo"/> is <see langword="null"/> — there is no meaningful "ideal" spacing to deviate from in either case (an explicit, disclosed empty result, not a fabricated one).</param>
/// <param name="Warnings">Human-readable, disclosed reasons this grid was flagged (empty when not low-confidence) — Step 037's own implementation guidance, verbatim: "false positives must show warning."</param>
public sealed record BeatGridQualityReport(
    Guid BeatGridVersionId,
    long CorrectionVersion,
    int BeatCount,
    long? FirstBeatOffsetTicks,
    double OverallConfidence,
    bool IsLowConfidence,
    IReadOnlyList<TimingDeviationBucket> TimingDeviationHistogram,
    IReadOnlyList<string> Warnings);
