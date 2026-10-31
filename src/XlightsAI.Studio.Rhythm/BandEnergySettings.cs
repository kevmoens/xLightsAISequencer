namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 1's own windowing/band-split configuration for
/// <see cref="IBandEnergyAnalyzer"/>. Shares <see cref="RhythmSettings"/>'s FFT/hop shape (the same
/// Hann-windowed spectral-flux machinery <see cref="OnsetDetector"/> already established) and adds the
/// two frequency cutoffs that split the spectrum into bass/mid/high bands. Recorded alongside every
/// <see cref="BandEnergyEnvelope"/> it produces (Section 4: "every cache key includes algorithm/
/// model/settings versions").
/// </summary>
/// <param name="Rhythm">The shared FFT size/hop/sample-rate windowing this run used — identical shape to <see cref="OnsetDetector"/>'s own, so a band-energy run and an onset run against the same song can reuse the same decoded frame grid conceptually (even though each still decodes independently today).</param>
/// <param name="BassCutoffHz">Upper bound (exclusive) of the bass band, in Hz. Must be positive and less than <see cref="HighCutoffHz"/>.</param>
/// <param name="HighCutoffHz">Lower bound (inclusive) of the high band, in Hz; everything in <c>[BassCutoffHz, HighCutoffHz)</c> is the mid band. Must be greater than <see cref="BassCutoffHz"/>.</param>
public sealed record BandEnergySettings(RhythmSettings Rhythm, double BassCutoffHz, double HighCutoffHz);
