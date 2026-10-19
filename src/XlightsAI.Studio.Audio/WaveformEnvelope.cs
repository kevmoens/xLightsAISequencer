namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 026 — Section 3's proposed signature, verbatim shape: one min/max/RMS summary of a
/// contiguous, half-open <c>[StartSample, EndSample)</c> run of decoded mono samples (Section 2: "All
/// time scopes are half-open [start,end)"), at one particular pyramid resolution level. Cheap to
/// render at any zoom level without re-decoding the source audio.
/// </summary>
/// <param name="StartSample">First sample index (inclusive) this envelope summarizes.</param>
/// <param name="EndSample">One past the last sample index this envelope summarizes; always <c>&gt; StartSample</c>.</param>
/// <param name="Min">Minimum sample value in <c>[StartSample, EndSample)</c>.</param>
/// <param name="Max">Maximum sample value in <c>[StartSample, EndSample)</c>.</param>
/// <param name="Rms">Root-mean-square sample value in <c>[StartSample, EndSample)</c> — a loudness-like summary distinct from peak Min/Max.</param>
public sealed record WaveformEnvelope(long StartSample, long EndSample, float Min, float Max, float Rms);
