namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 044's own required acceptance, verbatim: "Measured separation quality/performance is
/// documented and acceptance threshold met." One objective, reproducible measurement of a single
/// produced stem against its known-correct reference/ground-truth clip. Never a "listening test" score
/// (no human judgment is recorded here — see <c>implementation-evidence/PHASE-06/STEP-044.md</c> §3 for
/// how the two reference clips in the full-phase test matrix's own item 7 are selected).
/// </summary>
/// <param name="ClipId">Identifies which reference clip + stem kind this measurement belongs to.</param>
/// <param name="Kind">Which stem (drums/bass/vocals/...) this measurement was taken against.</param>
/// <param name="ReferenceIsSilent">True when the reference clip's own signal energy was at or below
/// <see cref="SeparationQualityThresholds.SilenceEnergyEpsilon"/> — this step's own "Silence ... must be
/// handled explicitly" failure/edge case, surfaced as data rather than hidden inside the numeric score.</param>
/// <param name="SignalToDistortionRatioDb">
/// <c>10 * log10(referenceEnergy / errorEnergy)</c>, in dB. When <see cref="ReferenceIsSilent"/> is true,
/// this is one of two documented finite sentinel values (never <c>NaN</c>/<c>Infinity</c>, which
/// <see cref="System.Text.Json.JsonSerializer"/> cannot round-trip by default) —
/// <see cref="SeparationQualityMetricCalculator.PerfectSilenceSentinelDb"/> when the candidate
/// reproduced the silence, <see cref="SeparationQualityMetricCalculator.FailedSilenceSentinelDb"/>
/// otherwise.</param>
/// <param name="CorrelationCoefficient">Pearson correlation coefficient between candidate and
/// reference, in <c>[-1, 1]</c>.</param>
/// <param name="PassesThreshold">True when this measurement clears the
/// <see cref="SeparationQualityThresholds"/> supplied to the calculator that produced it.</param>
public sealed record SeparationQualityMetric(
    string ClipId,
    StemKind Kind,
    bool ReferenceIsSilent,
    double SignalToDistortionRatioDb,
    double CorrelationCoefficient,
    bool PassesThreshold);
