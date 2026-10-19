namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real (no third-party DSP dependency) computation of <see cref="SeparationQualityMetric"/>. Pure and
/// synchronous — no file I/O, no ambient clock — the same precedent <see cref="ModelFeasibilityEvaluator"/>
/// already established for a phase-local decision gate. All real file I/O (decoding candidate/reference
/// WAV files, running the actual separator) is the impure orchestrator's job
/// (<see cref="SeparationQualityEvaluator"/>), never this calculator's.
/// </summary>
public static class SeparationQualityMetricCalculator
{
    /// <summary>
    /// Sentinel SDR (dB) reported when the reference clip is silent and the candidate reproduced that
    /// silence losslessly. A large, finite, explicitly-documented constant — never
    /// <see cref="double.PositiveInfinity"/>, which the ratio would otherwise naturally produce and
    /// which <see cref="System.Text.Json.JsonSerializer"/> cannot serialize by default.
    /// </summary>
    public const double PerfectSilenceSentinelDb = 120.0;

    /// <summary>
    /// Sentinel SDR (dB) reported when the reference clip is silent but the candidate is not — a
    /// large, finite, explicitly-documented "worst case" constant, never <see cref="double.NegativeInfinity"/>/
    /// <see cref="double.NaN"/>.
    /// </summary>
    public const double FailedSilenceSentinelDb = -60.0;

    public static SeparationQualityMetric Compute(
        string clipId,
        StemKind kind,
        IReadOnlyList<float> candidate,
        IReadOnlyList<float> reference,
        SeparationQualityThresholds thresholds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clipId);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholds.Validate();

        if (reference.Count == 0 || candidate.Count == 0)
        {
            throw new SeparationQualityInvalidInputException(
                $"Clip '{clipId}' (stem {kind}) has a zero-length candidate or reference signal; cannot measure quality.");
        }

        if (candidate.Count != reference.Count)
        {
            throw new SeparationQualityInvalidInputException(
                $"Clip '{clipId}' (stem {kind}): candidate length {candidate.Count} does not match reference " +
                $"length {reference.Count} -- invalid timing boundaries (this step's own failure/edge case, verbatim).");
        }

        var n = reference.Count;
        double referenceEnergy = 0.0, errorEnergy = 0.0;
        double sumRef = 0.0, sumCand = 0.0;

        for (var i = 0; i < n; i++)
        {
            double r = reference[i];
            double c = candidate[i];
            referenceEnergy += r * r;
            var e = r - c;
            errorEnergy += e * e;
            sumRef += r;
            sumCand += c;
        }

        var referenceIsSilent = referenceEnergy <= thresholds.SilenceEnergyEpsilon;

        double sdrDb;
        if (referenceIsSilent)
        {
            sdrDb = errorEnergy <= thresholds.SilenceEnergyEpsilon ? PerfectSilenceSentinelDb : FailedSilenceSentinelDb;
        }
        else
        {
            var safeErrorEnergy = Math.Max(errorEnergy, thresholds.SilenceEnergyEpsilon);
            sdrDb = 10.0 * Math.Log10(referenceEnergy / safeErrorEnergy);
        }

        var meanRef = sumRef / n;
        var meanCand = sumCand / n;
        double covariance = 0.0, varRef = 0.0, varCand = 0.0;
        for (var i = 0; i < n; i++)
        {
            var dr = reference[i] - meanRef;
            var dc = candidate[i] - meanCand;
            covariance += dr * dc;
            varRef += dr * dr;
            varCand += dc * dc;
        }

        double correlation;
        if (varRef <= thresholds.SilenceEnergyEpsilon || varCand <= thresholds.SilenceEnergyEpsilon)
        {
            // Both (or one) signal is effectively constant -- a Pearson correlation is mathematically
            // undefined (0/0), never computed as NaN here. A silent reference reproduced as silence is
            // "perfectly correlated" (1.0); any other constant-signal mismatch is reported as 0.0
            // (unknown/no linear relationship), never a fabricated high score.
            correlation = referenceIsSilent && errorEnergy <= thresholds.SilenceEnergyEpsilon ? 1.0 : 0.0;
        }
        else
        {
            correlation = covariance / Math.Sqrt(varRef * varCand);
        }

        var passesThreshold = referenceIsSilent
            ? errorEnergy <= thresholds.SilenceEnergyEpsilon
            : sdrDb >= thresholds.MinSignalToDistortionRatioDb && correlation >= thresholds.MinCorrelationCoefficient;

        return new SeparationQualityMetric(clipId, kind, referenceIsSilent, sdrDb, correlation, passesThreshold);
    }
}
