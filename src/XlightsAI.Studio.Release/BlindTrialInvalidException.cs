namespace XlightsAI.Studio.Release;

/// <summary>
/// Thrown by <see cref="IBlindTrialOrderingService.CreateTrial"/> when the supplied candidate set
/// cannot form a valid blind A/B trial (fewer than two candidates, a blank id, or a duplicate id).
/// </summary>
public sealed class BlindTrialInvalidException : Exception
{
    public BlindTrialInvalidException(string message)
        : base(message)
    {
    }
}
