using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Director;

/// <summary>
/// User feedback (2026-11-15), confirmed against a real generation ("Carol of the Bells"): the Director
/// prompt already tells the Choreographer about every real, audio-detected
/// <see cref="LeadInstrumentWindow"/> ("a lead instrument/section genuinely standing out above its own
/// recent surroundings... horns, strings, a riff") and asks for a distinct, repeating beat-locked stab
/// near each one -- but nothing previously checked whether any of them actually got one. A real run left
/// 3 of 4 detected windows with no dedicated treatment: the Show Bible wrote only one matching Look, and
/// the independent per-section selection never even guaranteed that one landed near its own window.
/// Mirrors <see cref="TimedCueComplianceChecker"/> exactly (same "a qualifying short/discrete effect
/// lands inside the window" structural proxy, same duration threshold) -- the only difference is the
/// windows come from real audio detection instead of a user-authored cue.
/// </summary>
public static class LeadInstrumentProminenceComplianceChecker
{
    /// <summary>Returns every window in <paramref name="windows"/> for which no effect in <paramref name="ir"/> qualifies -- empty when every window is satisfied, or when <paramref name="windows"/> itself is empty (nothing to check).</summary>
    public static IReadOnlyList<LeadInstrumentWindow> FindMissedWindows(ChoreographyIR ir, IReadOnlyList<LeadInstrumentWindow> windows)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(windows);

        return windows.Where(window => !IsSatisfied(ir, window)).ToList();
    }

    private static bool IsSatisfied(ChoreographyIR ir, LeadInstrumentWindow window)
    {
        return ir.Effects.Any(effect =>
            effect.EndTicks - effect.StartTicks <= TimedCueComplianceChecker.MaxQualifyingEffectDuration.Ticks &&
            effect.StartTicks < window.EndTicks && effect.EndTicks > window.StartTicks);
    }
}
