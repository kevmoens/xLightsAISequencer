namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Section 3's own proposed signature, verbatim: one detected contiguous region of audible
/// activity within a single <see cref="StemAsset"/>, with a confidence <see cref="Score"/> -- not a
/// claim of precise instrument classification (Step 043's own implementation guidance: "avoid treating
/// bleed as precise instrument classification"). <see cref="StartTicks"/>/<see cref="EndTicks"/> are
/// canonical monotonic-clock ticks (Section 2), already offset by the stem's own
/// <see cref="StemAsset.SourceOriginTicks"/>, so an activity region can be placed directly on the same
/// timeline as the original song without any further adjustment.
/// </summary>
/// <param name="Kind">Which stem (<see cref="StemAsset.Kind"/>) this region was detected in.</param>
/// <param name="StartTicks">First tick (inclusive) of this active region, half-open (Section 2: "[start,end)").</param>
/// <param name="EndTicks">One past the last tick of this active region; always <c>&gt; StartTicks</c>.</param>
/// <param name="Score">Confidence in <c>[0, 1]</c> -- the average peak-normalized RMS energy across the region, not a probability of correct instrument identity.</param>
public sealed record StemActivity(StemKind Kind, long StartTicks, long EndTicks, double Score);
