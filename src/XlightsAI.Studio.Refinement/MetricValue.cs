namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Section 3's own proposed signature, verbatim: <c>MetricValue(string Name, double Value,
/// double Weight, bool HardGate, string Explanation)</c>. One scored dimension inside a
/// <see cref="QualityReport"/> -- either a hard invariant gate (<see cref="HardGate"/> <see langword="true"/>,
/// <see cref="Value"/> is <c>1.0</c> when it passed or <c>0.0</c> when it failed) or a soft musical/density
/// metric in a clearly-documented unit (this step's own implementation guidance, verbatim: "soft
/// music/density metrics with clear units"). <see cref="Explanation"/> is never blank -- Section 5's own
/// cross-cutting UI rule ("show... an explicit version/status indicator when data is derived") requires a
/// human-readable reason a reader can act on, not only a bare number, and this step's own required
/// failure/edge case ("missing media must be handled explicitly") means an unavailable metric must say so
/// in words, never silently fabricate a confident-looking number.
/// </summary>
/// <param name="Name">A short, stable identifier for this metric (e.g. <c>"LegalEffects"</c>, <c>"Density"</c>) -- used to compare the same dimension across candidates/attempts.</param>
/// <param name="Value">
/// This metric's own score. For a <see cref="HardGate"/> metric, always exactly <c>1.0</c> (passed) or
/// <c>0.0</c> (failed) -- never a partial credit value, because a hard gate is binary by definition
/// (PHASE-12 Section 3: "Hard gates cannot be traded for weighted scoring"). For a soft metric, a
/// finite value in <c>[0, 1]</c> where higher is always better, or exactly <c>0.5</c> (a fixed, documented
/// "no data" midpoint) when <see cref="Weight"/> is <c>0</c> because the analysis input this metric needs
/// was not supplied.
/// </param>
/// <param name="Weight">
/// How much this metric should count toward an aggregate comparison. <c>0</c> means "not currently
/// evaluated" (missing analysis input) rather than "evaluated as worthless" -- a consumer must never
/// silently average this metric's own <see cref="Value"/> into a score when <see cref="Weight"/> is zero.
/// Must be finite and non-negative.
/// </param>
/// <param name="HardGate">
/// <see langword="true"/> when this metric is an invariant gate (scope/lock/effect-legality) rather than a
/// soft, tradeable score. A hard-gate failure (<see cref="Value"/> <c>== 0.0</c>) must always also appear
/// in the owning <see cref="QualityReport.Violations"/> -- never only reflected here.
/// </param>
/// <param name="Explanation">A human-readable reason for this exact <see cref="Value"/> -- never blank.</param>
public sealed record MetricValue(string Name, double Value, double Weight, bool HardGate, string Explanation)
{
    /// <exception cref="RefinementRunInvalidException">
    /// <see cref="Name"/>/<see cref="Explanation"/> is blank; <see cref="Value"/> is not finite, or is
    /// outside <c>[0, 1]</c>; <see cref="Weight"/> is not finite or is negative; or <see cref="HardGate"/>
    /// is <see langword="true"/> while <see cref="Value"/> is neither exactly <c>0.0</c> nor <c>1.0</c>.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new RefinementRunInvalidException("A MetricValue's Name must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(Explanation))
        {
            throw new RefinementRunInvalidException($"MetricValue '{Name}' Explanation must not be blank.");
        }

        if (!double.IsFinite(Value) || Value < 0.0 || Value > 1.0)
        {
            throw new RefinementRunInvalidException($"MetricValue '{Name}' Value must be a finite number in [0,1] (was {Value}).");
        }

        if (!double.IsFinite(Weight) || Weight < 0.0)
        {
            throw new RefinementRunInvalidException($"MetricValue '{Name}' Weight must be a finite, non-negative number (was {Weight}).");
        }

        if (HardGate && Value != 0.0 && Value != 1.0)
        {
            throw new RefinementRunInvalidException($"MetricValue '{Name}' is a hard gate, so Value must be exactly 0.0 or 1.0 (was {Value}).");
        }
    }
}
