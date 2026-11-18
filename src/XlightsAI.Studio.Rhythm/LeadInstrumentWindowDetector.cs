namespace XlightsAI.Studio.Rhythm;

/// <param name="BaselineWindowTicks">How far back (100ns ticks) the trailing rolling baseline looks -- default 8 seconds, long enough to represent "this song's own recent surroundings" rather than reacting to the immediately preceding beat.</param>
/// <param name="StepTicks">Both the candidate-instant spacing AND the "current" short window's own duration -- default 0.5 seconds. A single step clearing the prominence bar is a transient (already <see cref="TransientHitDetector"/>'s job, not this one); only several consecutive qualifying steps merge into a real window.</param>
/// <param name="ProminenceMultiplier">Minimum current-vs-baseline ratio (on <see cref="BandEnergyFrame"/>'s shared peak-normalized <c>[0,1]</c> scale) to count as "prominent" -- default 1.5x.</param>
/// <param name="MinimumDurationTicks">A run of qualifying steps shorter than this (default 1.5 seconds) is rejected as a transient spike, not a sustained lead-instrument phrase.</param>
/// <param name="MinimumBaselineFloor">Baseline value floor before multiplying by <see cref="ProminenceMultiplier"/> -- without this, a near-silent baseline (e.g. a true intro hush) would make almost any real sound "infinitely prominent."</param>
public sealed record LeadInstrumentWindowSettings(
    long BaselineWindowTicks = 80_000_000L,
    long StepTicks = 5_000_000L,
    double ProminenceMultiplier = 1.5,
    long MinimumDurationTicks = 15_000_000L,
    double MinimumBaselineFloor = 0.05);

/// <summary>
/// Build-plan Phase 27 (2026-11-14): measures "a real lead instrument/section is prominent right now" --
/// a signal nothing in this codebase previously computed (confirmed: <c>EmphasisContrastPlanner</c> is
/// instant-keyed off already-compiled effects, not raw audio, with no window-duration concept). Pure
/// computation over an existing <see cref="BandEnergyEnvelope"/> (no new audio decoding): walks a trailing
/// rolling baseline of Mid+High energy (reusing <see cref="BandEnergyEnvelope.AverageInWindow"/>, the
/// same windowed-scan approach <see cref="EnergyTransitionDetector"/> already uses for its own
/// before/after comparison), marks short steps whose CURRENT Mid+High energy clears the baseline by
/// <see cref="LeadInstrumentWindowSettings.ProminenceMultiplier"/>, and merges consecutive marked steps
/// into one real window -- rejecting any run shorter than <see cref="LeadInstrumentWindowSettings.MinimumDurationTicks"/>
/// as a transient spike (that case is already <see cref="TransientHitDetector"/>'s job), keeping only a
/// genuinely sustained phrase.
/// </summary>
public static class LeadInstrumentWindowDetector
{
    public static readonly LeadInstrumentWindowSettings Default = new();

    private const long TicksPerSecond = 10_000_000L;

    public static IReadOnlyList<LeadInstrumentWindow> Detect(BandEnergyEnvelope bandEnergy, LeadInstrumentWindowSettings? settings = null)
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

        var runStartTicks = (long?)null;
        var runRatios = new List<double>();
        var windows = new List<LeadInstrumentWindow>();

        void FlushRun(long runEndTicks)
        {
            if (runStartTicks is not { } start)
            {
                return;
            }

            if (runEndTicks - start >= effective.MinimumDurationTicks)
            {
                windows.Add(new LeadInstrumentWindow(start, runEndTicks, Math.Round(runRatios.Average(), 2)));
            }

            runStartTicks = null;
            runRatios.Clear();
        }

        for (var t = effective.BaselineWindowTicks; t < totalTicks; t += effective.StepTicks)
        {
            var (baseBass, baseMid, baseHigh) = bandEnergy.AverageInWindow(t - effective.BaselineWindowTicks, t);
            var (curBass, curMid, curHigh) = bandEnergy.AverageInWindow(t, t + effective.StepTicks);
            _ = baseBass;
            _ = curBass;

            var baseline = Math.Max((baseMid + baseHigh) / 2.0, effective.MinimumBaselineFloor);
            var current = (curMid + curHigh) / 2.0;
            var ratio = current / baseline;

            if (ratio >= effective.ProminenceMultiplier)
            {
                runStartTicks ??= t;
                runRatios.Add(ratio);
            }
            else
            {
                FlushRun(t);
            }
        }

        FlushRun(totalTicks);

        return windows;
    }
}
