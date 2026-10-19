namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 044's own failure/edge case, verbatim: "Silence, invalid timing boundaries and missing
/// media must be handled explicitly." Thrown by <see cref="SeparationQualityMetricCalculator"/> and
/// <see cref="SeparationQualityEvaluator"/> for every one of those three cases that is not itself a
/// legitimate, scorable input (a silent *reference* clip is legitimate and scored explicitly — see
/// <see cref="SeparationQualityMetric.ReferenceIsSilent"/> — this exception is for genuinely malformed
/// input, not silence itself): a candidate/reference sample-count mismatch ("invalid timing
/// boundaries"), a missing reference or candidate media file ("missing media"), or a zero-length clip.
/// </summary>
public sealed class SeparationQualityInvalidInputException : Exception
{
    public SeparationQualityInvalidInputException(string message) : base(message)
    {
    }

    public SeparationQualityInvalidInputException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
