namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// One analysis frame's spectral-flux-derived energy, split into three frequency bands. Unlike
/// <see cref="EnergyFrame.NormalizedEnergy"/> (one broadband scalar) or <see cref="OnsetDetector"/>'s
/// own internal per-bin flux (collapsed into a single sum and discarded), every band here is exposed as
/// a first-class, independently queryable value so a bass-heavy section can be told apart from a
/// treble-heavy one. <see cref="Bass"/>/<see cref="Mid"/>/<see cref="High"/> are peak-normalized against
/// one shared scale (the single largest per-band flux value anywhere in the whole run, across all three
/// bands) rather than each band normalized against its own independent peak -- a bass-heavy frame must
/// read as "more bass than treble" on a common scale, not as "100% of bass's own peak" next to "100% of
/// treble's own peak," which would erase the very distinction this type exists to expose.
/// </summary>
/// <param name="StartSample">First sample (inclusive) this frame's window covers.</param>
/// <param name="EndSample">One past the last sample this frame's window covers (half-open).</param>
/// <param name="Bass">Peak-normalized spectral flux in <c>[BassCutoffHz, HighCutoffHz)</c>... below <c>BassCutoffHz</c>, in <c>[0, 1]</c>.</param>
/// <param name="Mid">Peak-normalized spectral flux in <c>[BassCutoffHz, HighCutoffHz)</c>, in <c>[0, 1]</c>.</param>
/// <param name="High">Peak-normalized spectral flux at or above <c>HighCutoffHz</c>, in <c>[0, 1]</c>.</param>
public sealed record BandEnergyFrame(long StartSample, long EndSample, float Bass, float Mid, float High);
