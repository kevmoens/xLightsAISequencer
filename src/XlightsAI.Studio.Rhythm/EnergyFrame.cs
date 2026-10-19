namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 031's own first definition of what one windowed energy measurement looks like — not named in
/// Section 3's own proposed signatures (which only name the later <c>OnsetEvent</c>/<c>BeatMarker</c>
/// types this step's output feeds into). One contiguous, half-open <c>[StartSample, EndSample)</c>
/// run of decoded mono samples (Section 2: "All time scopes are half-open [start,end)"), at a single
/// fixed <see cref="RhythmSettings.FftSize"/>/<see cref="RhythmSettings.HopSamples"/> window.
/// </summary>
/// <param name="StartSample">First sample index (inclusive) this frame summarizes.</param>
/// <param name="EndSample">One past the last sample index this frame summarizes; always <c>&gt; StartSample</c>. May summarize fewer than <see cref="RhythmSettings.FftSize"/> samples only for the final frame, if the decoded signal does not end on an exact window boundary.</param>
/// <param name="Rms">Root-mean-square sample value in <c>[StartSample, EndSample)</c> — the raw, un-normalized loudness measurement for this window.</param>
/// <param name="NormalizedEnergy">
/// <see cref="Rms"/> divided by the single largest <see cref="Rms"/> value across every frame in the
/// same <see cref="EnergyEnvelope"/> ("unit normalization," Section 3 implementation guidance) —
/// always in <c>[0, 1]</c>, with exactly one frame (the loudest) equal to <c>1.0</c> whenever the
/// source signal is not true digital silence. Always <c>0</c> for a fully silent signal (the peak RMS
/// is <c>0</c>, so this is defined as <c>0</c> rather than an undefined <c>0/0</c>).
/// </param>
public sealed record EnergyFrame(long StartSample, long EndSample, float Rms, float NormalizedEnergy);
