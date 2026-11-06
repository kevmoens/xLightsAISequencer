namespace XlightsAI.Studio.Rhythm;

/// <param name="WindowTicks">The size (in 100ns ticks) of the "before" and "after" comparison windows straddling each candidate instant -- default 1 second.</param>
/// <param name="StepTicks">Spacing (in ticks) between candidate instants walked across the song -- default 0.5 seconds, finer than <see cref="WindowTicks"/> for reasonable localization without checking every single analysis frame.</param>
/// <param name="BuildupThreshold">Minimum before-to-after rise (on the <c>[0,1]</c> peak-normalized band-energy scale <see cref="BandEnergyFrame"/> already uses) to qualify as a <see cref="EnergyTransitionKind.Buildup"/>.</param>
/// <param name="DropThreshold">Minimum before-to-after fall (same scale) to qualify as a <see cref="EnergyTransitionKind.Drop"/>.</param>
public sealed record EnergyTransitionSettings(
    long WindowTicks = 10_000_000L,
    long StepTicks = 5_000_000L,
    double BuildupThreshold = 0.25,
    double DropThreshold = 0.25);

/// <summary>
/// Build-plan "richer analysis tools" Phase 4 (2026-11-02): walks a song's own <see cref="BandEnergyEnvelope"/>
/// at a fixed step, comparing the average band energy in the window immediately before each candidate
/// instant against the window immediately after it (reusing <see cref="BandEnergyEnvelope.AverageInWindow"/>
/// -- no new DSP), and reports every instant whose before/after delta clears
/// <see cref="EnergyTransitionSettings.BuildupThreshold"/>/<see cref="EnergyTransitionSettings.DropThreshold"/>.
/// Adjacent qualifying instants within one window of each other are debounced down to the single strongest
/// one, the same "largest peak wins, not every frame that clears a threshold" discipline
/// <see cref="OnsetDetector"/> already applies via its own <c>MinInterOnsetSamples</c>.
/// </summary>
public static class EnergyTransitionDetector
{
    public static readonly EnergyTransitionSettings Default = new();

    private const long TicksPerSecond = 10_000_000L;

    public static IReadOnlyList<EnergyTransition> Detect(BandEnergyEnvelope bandEnergy, EnergyTransitionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(bandEnergy);
        var effective = settings ?? Default;

        if (bandEnergy.Frames.Length == 0)
        {
            return [];
        }

        var sampleRate = bandEnergy.Settings.Rhythm.AnalysisSampleRate;
        var lastFrame = bandEnergy.Frames[^1];
        var totalTicks = sampleRate > 0 ? lastFrame.EndSample * TicksPerSecond / sampleRate : 0;

        var transitions = new List<EnergyTransition>();

        for (var t = effective.WindowTicks; t < totalTicks; t += effective.StepTicks)
        {
            var (beforeBass, beforeMid, beforeHigh) = bandEnergy.AverageInWindow(t - effective.WindowTicks, t);
            var (afterBass, afterMid, afterHigh) = bandEnergy.AverageInWindow(t, t + effective.WindowTicks);
            var before = (beforeBass + beforeMid + beforeHigh) / 3.0;
            var after = (afterBass + afterMid + afterHigh) / 3.0;
            var delta = after - before;

            EnergyTransitionKind? kind = delta >= effective.BuildupThreshold
                ? EnergyTransitionKind.Buildup
                : -delta >= effective.DropThreshold
                    ? EnergyTransitionKind.Drop
                    : null;

            if (kind is null)
            {
                continue;
            }

            var magnitude = (float)Math.Abs(delta);

            if (transitions.Count > 0 && transitions[^1].Kind == kind && t - transitions[^1].TimeTicks < effective.WindowTicks)
            {
                if (magnitude > transitions[^1].Magnitude)
                {
                    transitions[^1] = new EnergyTransition(t, kind.Value, magnitude);
                }

                continue;
            }

            transitions.Add(new EnergyTransition(t, kind.Value, magnitude));
        }

        return transitions;
    }
}
