namespace XlightsAI.Studio.Rhythm;

/// <param name="LocalWindowTicks">
/// How wide a time-centered neighborhood (in both directions) to measure each onset's own local
/// mean/standard-deviation of strength against -- the same local-adaptive-threshold shape
/// <see cref="OnsetDetector"/>'s own peak-picking already uses (there, a frame-indexed radius; here,
/// a time-windowed neighbor search, since onsets are sparse and irregularly spaced). Default 15
/// seconds: wide enough to gather a real statistical sample even in a sparser passage, narrow enough
/// to adapt across a song's own dynamic range (a quiet verse vs. a loud chorus) rather than averaging
/// across the whole song.
/// </param>
/// <param name="LocalSensitivityMultiplier">
/// Scales the local adaptive threshold (local mean + <c>LocalSensitivityMultiplier</c> * local
/// standard deviation) an onset's own strength must clear to count as a "hit" -- same 1.5 default
/// <see cref="XlightsAI.Studio.Pipeline.AudioAnalysisSettingsBundle"/> already uses for
/// <c>OnsetDetectionSettings.SensitivityMultiplier</c>, for the same reason (calibrated, not guessed).
/// </param>
/// <param name="MinimumAbsoluteStrength">
/// User feedback (2026-11-14), measured against a real song ("Carol Of The Bells"): a fixed
/// song-wide-peak-relative floor (the old <c>MinimumStrength</c> = 0.6) discarded 87% of this song's
/// 471 real detected onsets (471 -&gt; 62) before they ever reached the Director, because the song's
/// one or two loudest orchestral swells dwarfed its otherwise-dense, genuinely real bell-ostinato
/// attacks -- a locally-adaptive gate (<see cref="LocalWindowTicks"/>/<see cref="LocalSensitivityMultiplier"/>)
/// replaces that global comparison. This absolute floor is what remains of the old global guard: the
/// same disclosed purpose <see cref="OnsetDetector.MinimumAbsoluteNormalizedFlux"/> serves there --
/// a near-silent passage's own tiny noise floor must never be promoted into a "hit" just because it is
/// locally the loudest thing around it.
/// </param>
/// <param name="MinimumBassEnergy">When supplied alongside a real <see cref="BandEnergyEnvelope"/>, an onset must also clear this average bass-band energy (over <see cref="CorroborationWindowTicks"/> centered on it) to qualify -- corroborating evidence that this is a genuinely low-end-heavy impact (a "bang"/sub-bass hit), not just a sharp but thin-sounding transient (e.g. a hi-hat). <see langword="null"/> (the default) skips this corroboration entirely.</param>
/// <param name="CorroborationWindowTicks">How wide a window (centered on the onset) to average bass energy over for <see cref="MinimumBassEnergy"/> -- default 50ms (500,000 ticks at the canonical 100ns/tick unit), short enough to stay localized to the actual hit.</param>
public sealed record TransientHitSettings(
    long LocalWindowTicks = 150_000_000L,
    double LocalSensitivityMultiplier = 1.5,
    double MinimumAbsoluteStrength = 0.05,
    double? MinimumBassEnergy = null,
    long CorroborationWindowTicks = 500_000);

/// <summary>
/// Build-plan "richer analysis tools" Phase 2 (2026-11-02); strength gate redesigned 2026-11-14.
/// Promotes the locally-loudest <see cref="OnsetEvent"/>s already present in a song's own
/// <see cref="OnsetAnalysis"/> into <see cref="TransientHit"/>s -- a pure, deterministic post-filter,
/// no new DSP. Directly strengthens the timed-cue guard rail (<c>XlightsAI.Studio.Director.TimedCue</c>):
/// when a user's hand-typed timestamp is close to, but not exactly on, a real detected transient, the
/// Director layer can snap the cue's effective target to that real moment instead of trusting the raw
/// typed guess. Also the Director's own real "Transients" timing-track population -- see
/// <see cref="TransientHitSettings.MinimumAbsoluteStrength"/>'s own doc comment for the real,
/// measured reason a song-wide-peak-relative gate under-produced hits for a dynamically wide song.
/// </summary>
public static class TransientHitDetector
{
    public static readonly TransientHitSettings Default = new();

    public static IReadOnlyList<TransientHit> Detect(OnsetAnalysis onsets, BandEnergyEnvelope? bandEnergy = null, TransientHitSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(onsets);
        var effective = settings ?? Default;

        var hits = new List<TransientHit>();
        for (var i = 0; i < onsets.Onsets.Length; i++)
        {
            var onset = onsets.Onsets[i];
            if (onset.Strength < effective.MinimumAbsoluteStrength)
            {
                continue;
            }

            var (mean, stdDev) = ComputeLocalStatistics(onsets.Onsets, i, effective.LocalWindowTicks);
            var threshold = mean + (effective.LocalSensitivityMultiplier * stdDev);
            if (onset.Strength <= threshold)
            {
                continue;
            }

            if (bandEnergy is not null && effective.MinimumBassEnergy is { } minBass)
            {
                var halfWindow = effective.CorroborationWindowTicks / 2;
                var (bass, _, _) = bandEnergy.AverageInWindow(onset.TimeTicks - halfWindow, onset.TimeTicks + halfWindow);
                if (bass < minBass)
                {
                    continue;
                }
            }

            hits.Add(new TransientHit(onset.TimeTicks, onset.Strength));
        }

        return hits;
    }

    /// <summary>
    /// Mean/standard-deviation of <see cref="OnsetEvent.Strength"/> among every OTHER onset within
    /// <paramref name="windowTicks"/> of <paramref name="centerIndex"/>'s own onset -- deliberately
    /// excluding the candidate itself (unlike <see cref="OnsetDetector.ComputeLocalStatistics"/>'s own
    /// frame-indexed radius, which includes its own center frame): a genuinely isolated onset with no
    /// nearby neighbors must compare against "no other real activity nearby" (count 0, mean/stdDev 0,
    /// so any real strength above the absolute floor passes), not against itself alone, which would
    /// always compute mean == its own strength and stdDev == 0 and could never clear a strict
    /// greater-than threshold. This is the time-windowed neighbor search a sparse, irregularly-spaced
    /// onset list needs in place of that method's simpler fixed-radius array indexing.
    /// </summary>
    private static (double Mean, double StdDev) ComputeLocalStatistics(OnsetEvent[] onsets, int centerIndex, long windowTicks)
    {
        var centerTime = onsets[centerIndex].TimeTicks;

        double sum = 0;
        var count = 0;
        for (var i = 0; i < onsets.Length; i++)
        {
            if (i == centerIndex || Math.Abs(onsets[i].TimeTicks - centerTime) > windowTicks)
            {
                continue;
            }

            sum += onsets[i].Strength;
            count++;
        }

        var mean = count > 0 ? sum / count : 0.0;

        double sumSquaredDeviation = 0;
        for (var i = 0; i < onsets.Length; i++)
        {
            if (i == centerIndex || Math.Abs(onsets[i].TimeTicks - centerTime) > windowTicks)
            {
                continue;
            }

            var deviation = onsets[i].Strength - mean;
            sumSquaredDeviation += deviation * deviation;
        }

        var stdDev = count > 0 ? Math.Sqrt(sumSquaredDeviation / count) : 0.0;
        return (mean, stdDev);
    }
}
