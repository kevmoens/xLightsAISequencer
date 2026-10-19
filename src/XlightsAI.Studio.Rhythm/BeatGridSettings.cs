namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 034's own beat-grid generation configuration, layered on top of an already-ranked
/// <see cref="TempoAnalysis"/> (it does not re-run onset or tempo detection — see
/// <see cref="IBeatGridBuilder"/>). Recorded alongside every <see cref="BeatGrid"/> it produces —
/// part of the cache key (Section 4: "every cache key includes algorithm/model/settings versions")
/// exactly like <see cref="RhythmSettings"/>/<see cref="OnsetDetectionSettings"/>/<see cref="TempoDetectionSettings"/>.
/// </summary>
/// <param name="BeatsPerBar">
/// Meter numerator used to group generated beats into bars (<see cref="BeatMarker.BarIndex"/>/<see cref="BeatMarker.BeatWithinBar"/>).
/// Must be positive. Step 034's own implementation guidance: "meter must be user correctable" — this
/// value is a plain, replaceable setting (a later rebuild with a different <see cref="BeatsPerBar"/>
/// simply invalidates the cache like any other setting change), not a hardcoded constant.
/// </param>
/// <param name="AlignmentToleranceTicks">
/// Maximum absolute distance, in ticks, between a generated beat position and its nearest real onset
/// for that beat to be considered "aligned" to actual detected rhythmic evidence (this step's own
/// required acceptance, verbatim: "Tempo fixture aligns events within configured threshold"). Must be
/// non-negative. A beat with no onset inside this tolerance is still generated (not dropped) but
/// reports a lower <see cref="BeatMarker.Confidence"/> — never silently treated as equally certain.
/// </param>
public sealed record BeatGridSettings(int BeatsPerBar, long AlignmentToleranceTicks);
