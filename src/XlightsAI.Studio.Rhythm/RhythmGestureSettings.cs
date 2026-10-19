namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 036's own pulse-duration configuration for <see cref="IRhythmGesturePlanner"/> — not part of
/// Section 3's proposed <c>BuildPulses</c> signature itself (which carries no settings parameter), so
/// it is a constructor-supplied dependency of <see cref="RhythmGesturePlanner"/> instead, the same
/// settings-record convention already established by <see cref="RhythmSettings"/>/
/// <see cref="OnsetDetectionSettings"/>/<see cref="TempoDetectionSettings"/>/<see cref="BeatGridSettings"/>.
/// </summary>
/// <param name="MinPulseDurationTicks">
/// Pulse duration assigned to a beat with <see cref="BeatMarker.Confidence"/> <c>== 0</c> — the floor
/// of Step 036's own "restrained default visual density" (implementation guidance, verbatim). Must be
/// positive.
/// </param>
/// <param name="MaxPulseDurationTicks">
/// Pulse duration assigned to a beat with <see cref="BeatMarker.Confidence"/> <c>== 1</c>. Must be
/// greater than or equal to <see cref="MinPulseDurationTicks"/>. Beat strength in between linearly
/// interpolates between the two (this step's own implementation guidance, verbatim: "Translate
/// selected beat strength into finite pulse duration").
/// </param>
public sealed record RhythmGestureSettings(long MinPulseDurationTicks, long MaxPulseDurationTicks)
{
    /// <summary>A short, restrained default: 50-150 ms pulses (well under one beat at any reasonable tempo), later snapped to the real xLights frame grid by <see cref="RhythmGesturePlanner"/>.</summary>
    public static RhythmGestureSettings Default { get; } = new(
        MinPulseDurationTicks: TimeSpan.FromMilliseconds(50).Ticks,
        MaxPulseDurationTicks: TimeSpan.FromMilliseconds(150).Ticks);
}
