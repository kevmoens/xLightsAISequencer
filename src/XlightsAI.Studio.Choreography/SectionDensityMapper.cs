namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 12 §3a ("Section-Energy-Driven Change Density"): maps a section's own already-declared
/// energy ceiling (<see cref="SectionMusicContext.EnergyCeiling"/>, itself sourced from
/// <c>EnergyCeilingByKind</c>/architecture §10's structural arc) onto a target change-rate band -- a
/// measurable ceiling on how often <see cref="LookCompiler"/>'s own Mode B discrete layers may change,
/// not a new energy signal and not a new compiled-IR concept.
/// </summary>
/// <remarks>
/// Calibrated against the 2026-10-30 real-run comparison
/// (<c>docs/AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md</c>): the real hand-sequenced reference's own
/// bucketed-start-time density swung ~4.7x between its quietest and loudest sections, while the
/// AI-generated version (before this phase) was flat at ~1.2x. Architecture §10's own declared ceiling
/// table (<c>DefaultEnergyCeilings.ArchitectureSection10</c>) spans [0.10 (Outro), 0.85 (Chorus)] --
/// <see cref="FloorChangeRatePerSecond"/>/<see cref="CeilingToRateSlope"/> below are chosen so that exact
/// range maps onto a ~5.1x spread, deliberately a touch past the reference's own 4.7x rather than short of
/// it (a throttle that only ever narrows a layer's own natural cadence, never widens it -- see
/// <see cref="LookCompiler"/>'s own "never sped up, only ever throttled" discipline -- cannot overshoot in
/// practice).
/// </remarks>
public static class SectionDensityMapper
{
    /// <summary>The slowest a section's own target change-rate is ever allowed to fall to, regardless of how low its declared ceiling is -- a true zero would starve the always-moving rule's own later gap-filling pass of anything to build on.</summary>
    private const double FloorChangeRatePerSecond = 0.2;

    /// <summary>How much target change-rate one full unit of declared energy ceiling buys -- see this type's own remarks for how this was calibrated.</summary>
    private const double CeilingToRateSlope = 2.4;

    /// <summary>This section's own target change-rate (effect starts/second), from its declared <paramref name="energyCeiling"/> in <c>[0,1]</c> (clamped if out of range).</summary>
    public static double TargetChangeRatePerSecond(double energyCeiling)
    {
        var clamped = Math.Clamp(energyCeiling, 0.0, 1.0);
        return FloorChangeRatePerSecond + (clamped * CeilingToRateSlope);
    }
}
