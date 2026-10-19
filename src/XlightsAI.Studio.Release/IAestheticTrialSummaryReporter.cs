namespace XlightsAI.Studio.Release;

/// <summary>Phase-local contract (disclosed). See <see cref="AestheticTrialSummary"/>.</summary>
public interface IAestheticTrialSummaryReporter
{
    /// <exception cref="ArgumentNullException"><paramref name="ratings"/> is <see langword="null"/>.</exception>
    IReadOnlyList<AestheticTrialSummary> Summarize(IReadOnlyList<AestheticRating> ratings);
}
