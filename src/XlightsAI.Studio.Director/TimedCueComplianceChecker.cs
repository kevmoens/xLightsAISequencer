using XlightsAI.Studio.Choreography;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 1 (2026-11-01): the post-compile guard rail for
/// <see cref="TimedCue"/>s -- mechanically checks whether a compiled <see cref="ChoreographyIR"/>
/// actually contains a qualifying short/discrete effect inside each cue's own tolerance-expanded
/// window, rather than trusting that the Choreographer honored the cue just because it was asked to.
/// A cue with no qualifying effect anywhere near it is "missed," which
/// <see cref="RevisionDirectiveDeriver.FromMissedTimedCues"/> turns into a real, targeted repair
/// directive (same detect → direct → regenerate pattern as <see cref="ChoreographyLegalityValidator.RuleModelCoverage"/>).
///
/// "Qualifying" is deliberately a structural proxy, not a perceptual one: a short effect (duration at
/// or under <see cref="MaxQualifyingEffectDuration"/>) landing inside the window is treated as "a hard
/// hit near that moment." This cannot verify the hit actually reads as synced to a specific sung word
/// or sound in the audio -- only that SOMETHING brief and deliberate was placed near the right real
/// time, which is the limit of what a structural (not audio-perceptual) check can honestly claim.
/// </summary>
public static class TimedCueComplianceChecker
{
    /// <summary>
    /// A compiled effect at or under this duration counts as "short/discrete" for cue-satisfaction
    /// purposes -- long enough to cover a real hard-hit/flash effect's actual duration, short enough
    /// that a long sustained wash/hold never accidentally satisfies a "hit this exact moment" cue.
    /// </summary>
    public static readonly TimeSpan MaxQualifyingEffectDuration = TimeSpan.FromSeconds(2);

    /// <summary>Returns every cue in <paramref name="cues"/> for which no effect in <paramref name="ir"/> qualifies -- empty when every cue is satisfied, or when <paramref name="cues"/> itself is empty (nothing to check).</summary>
    public static IReadOnlyList<TimedCue> FindMissedCues(ChoreographyIR ir, IReadOnlyList<TimedCue> cues)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(cues);

        return cues.Where(cue => !IsSatisfied(ir, cue)).ToList();
    }

    private static bool IsSatisfied(ChoreographyIR ir, TimedCue cue)
    {
        var windowStartTicks = cue.WindowStart.Ticks;
        var windowEndTicks = cue.WindowEnd.Ticks;

        return ir.Effects.Any(effect =>
            effect.EndTicks - effect.StartTicks <= MaxQualifyingEffectDuration.Ticks &&
            effect.StartTicks < windowEndTicks && effect.EndTicks > windowStartTicks);
    }
}
