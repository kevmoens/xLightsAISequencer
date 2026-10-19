namespace XlightsAI.Studio.Release;

/// <summary>
/// Thrown when an <see cref="AestheticRating"/> fails its own <see cref="AestheticRating.Validate"/>
/// check — Step 099's own required failure/edge case, verbatim: "Invalid input... must not corrupt the
/// project." Thrown before <see cref="IAestheticTrialCollector.RecordAsync"/> touches any persisted
/// state, mirroring Step 098's own "reject before any file is written" guarantee.
/// </summary>
public sealed class AestheticRatingInvalidException : Exception
{
    public AestheticRatingInvalidException(string message)
        : base(message)
    {
    }
}
