namespace XlightsAI.Studio.Rhythm;

/// <param name="MinimumStrength">An <see cref="OnsetEvent"/> below this normalized flux strength is not a loud enough transient to count as a "hit" -- it's treated as an ordinary (possibly quiet, melodic) onset instead. 0.6 is a deliberately conservative default: high enough that routine note onsets rarely qualify, low enough that real percussive hits reliably do.</param>
/// <param name="MinimumBassEnergy">When supplied alongside a real <see cref="BandEnergyEnvelope"/>, an onset must also clear this average bass-band energy (over <see cref="CorroborationWindowTicks"/> centered on it) to qualify -- corroborating evidence that this is a genuinely low-end-heavy impact (a "bang"/sub-bass hit), not just a sharp but thin-sounding transient (e.g. a hi-hat). <see langword="null"/> (the default) skips this corroboration entirely and relies on <see cref="MinimumStrength"/> alone.</param>
/// <param name="CorroborationWindowTicks">How wide a window (centered on the onset) to average bass energy over for <see cref="MinimumBassEnergy"/> -- default 50ms (500,000 ticks at the canonical 100ns/tick unit), short enough to stay localized to the actual hit.</param>
public sealed record TransientHitSettings(double MinimumStrength = 0.6, double? MinimumBassEnergy = null, long CorroborationWindowTicks = 500_000);

/// <summary>
/// Build-plan "richer analysis tools" Phase 2 (2026-11-02): promotes the loudest <see cref="OnsetEvent"/>s
/// already present in a song's own <see cref="OnsetAnalysis"/> into <see cref="TransientHit"/>s -- a pure,
/// deterministic post-filter, no new DSP. Directly strengthens the timed-cue guard rail
/// (<c>XlightsAI.Studio.Director.TimedCue</c>): when a user's hand-typed timestamp is close to, but not
/// exactly on, a real detected transient, the Director layer can snap the cue's effective target to that
/// real moment instead of trusting the raw typed guess.
/// </summary>
public static class TransientHitDetector
{
    public static readonly TransientHitSettings Default = new();

    public static IReadOnlyList<TransientHit> Detect(OnsetAnalysis onsets, BandEnergyEnvelope? bandEnergy = null, TransientHitSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(onsets);
        var effective = settings ?? Default;

        var hits = new List<TransientHit>();
        foreach (var onset in onsets.Onsets)
        {
            if (onset.Strength < effective.MinimumStrength)
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
}
