namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 044 — "Compare reference outputs, audio metrics and listening tests; establish
/// supported hardware." The declared, versioned acceptance bar a <see cref="SeparationQualityMetric"/>
/// must clear to be reported as passing. Carried alongside every <see cref="SeparationQualityReport"/>
/// (not a static constant) so a report is self-describing even if the threshold is tightened later —
/// Section 4's "every cache key includes algorithm/model/settings versions" rule, applied to a quality
/// gate rather than a cache key.
/// </summary>
/// <param name="MinSignalToDistortionRatioDb">Minimum acceptable signal-to-distortion ratio, in dB,
/// computed by <see cref="SeparationQualityMetricCalculator"/> as
/// <c>10 * log10(referenceEnergy / errorEnergy)</c>.</param>
/// <param name="MinCorrelationCoefficient">Minimum acceptable Pearson correlation coefficient
/// (<c>[-1, 1]</c>) between the candidate and reference signal.</param>
/// <param name="SilenceEnergyEpsilon">Signal energy (sum of squared samples) at or below which a
/// reference clip is treated as digital silence, so a true-silence comparison is handled explicitly
/// (this step's own failure/edge case, verbatim: "Silence ... must be handled explicitly") rather than
/// dividing by zero.</param>
public sealed record SeparationQualityThresholds(
    double MinSignalToDistortionRatioDb,
    double MinCorrelationCoefficient,
    double SilenceEnergyEpsilon = 1e-9)
{
    public void Validate()
    {
        if (SilenceEnergyEpsilon <= 0)
        {
            throw new SeparationQualityInvalidInputException(
                $"{nameof(SilenceEnergyEpsilon)} must be strictly positive; got {SilenceEnergyEpsilon}.");
        }

        if (MinCorrelationCoefficient is < -1.0 or > 1.0)
        {
            throw new SeparationQualityInvalidInputException(
                $"{nameof(MinCorrelationCoefficient)} must be within [-1, 1]; got {MinCorrelationCoefficient}.");
        }
    }
}
