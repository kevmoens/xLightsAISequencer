namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 3 ("duty-cycle correction table per effect family"). Architecture §7's own
/// measured values, verbatim: "Twinkle ≈0.25, SingleStrand ≈0.35, Butterfly ≈0.8, Plasma ≈0.85, Color
/// Wash ≈0.9. Declared pixel budget overstated rendered brightness by 3-4x because Twinkle-family
/// effects light a fraction of nodes per frame." Looked up by the xLights effect name
/// (<see cref="IEffectRecipe.XlightsEffectName"/>). An effect not in this table gets
/// <see cref="DefaultDutyCycle"/> (1.0 -- "assume fully-lit, no correction") rather than a guessed
/// fraction; <see cref="PixelCoverageLadder"/> flags this as reduced confidence, never silently invents
/// a measured value this build has not verified.
/// </summary>
public static class DutyCycleTable
{
    /// <summary>Used for any effect not present in <see cref="Corrections"/> -- the conservative "no correction known" default, never a guessed fraction.</summary>
    public const double DefaultDutyCycle = 1.0;

    /// <summary>Architecture §7's own verified per-effect-family duty-cycle corrections, by real xLights effect name.</summary>
    public static readonly IReadOnlyDictionary<string, double> Corrections = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["Twinkle"] = 0.25,
        ["SingleStrand"] = 0.35,
        ["Butterfly"] = 0.8,
        ["Plasma"] = 0.85,
        ["ColorWash"] = 0.9,
    };

    /// <summary>The duty-cycle correction for <paramref name="xlightsEffectName"/>, or <see cref="DefaultDutyCycle"/> when unmeasured.</summary>
    public static double CorrectionFor(string xlightsEffectName) =>
        Corrections.TryGetValue(xlightsEffectName, out var value) ? value : DefaultDutyCycle;

    /// <summary>Whether <paramref name="xlightsEffectName"/> has a real, measured duty-cycle correction (vs. the unmeasured default).</summary>
    public static bool HasMeasuredCorrection(string xlightsEffectName) => Corrections.ContainsKey(xlightsEffectName);
}
