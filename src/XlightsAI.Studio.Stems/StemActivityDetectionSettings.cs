namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 043's own windowing/hysteresis configuration, mirroring
/// <c>XlightsAI.Studio.Rhythm.RhythmSettings</c>'/<c>OnsetDetectionSettings</c>' own shape: a plain,
/// serializable settings record recorded alongside every produced <see cref="StemActivityResult"/>
/// (Section 4: "every cache key includes algorithm/model/settings versions," applied here to the
/// settings themselves, not just the detector's own code version).
/// </summary>
/// <param name="WindowSamples">Samples summarized by each RMS window. Must be positive.</param>
/// <param name="HopSamples">Samples to advance between the start of one window and the next. Must be positive.</param>
/// <param name="OnThreshold">
/// Peak-normalized RMS (in <c>(0, 1]</c>) a window must reach to transition an inactive stem into an
/// active region ("hysteresis," Step 043's own implementation guidance, verbatim).
/// </param>
/// <param name="OffThreshold">
/// Peak-normalized RMS (in <c>[0, OnThreshold)</c>) an already-active region must fall below before it
/// is closed. Strictly less than <see cref="OnThreshold"/> -- the hysteresis gap itself -- so a signal
/// hovering right at one single level does not flicker rapidly between active/inactive.
/// </param>
/// <param name="MinActiveDurationTicks">
/// Minimum region duration, in canonical 100&#8201;ns ticks, for a detected region to be kept. Shorter
/// regions are discarded as noise/bleed rather than reported as real activity (Step 043's own
/// implementation guidance: "avoid treating bleed as precise instrument classification"). Must not be
/// negative.
/// </param>
public sealed record StemActivityDetectionSettings(
    int WindowSamples,
    int HopSamples,
    double OnThreshold,
    double OffThreshold,
    long MinActiveDurationTicks);
