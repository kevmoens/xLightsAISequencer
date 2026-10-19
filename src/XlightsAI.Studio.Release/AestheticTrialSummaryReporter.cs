namespace XlightsAI.Studio.Release;

/// <summary>
/// Default <see cref="IAestheticTrialSummaryReporter"/>. Pure, deterministic aggregation -- no I/O,
/// no cherry-picking: every candidate id present anywhere in <paramref name="ratings"/> appears exactly
/// once in the result, each with its own real, observed <see cref="AestheticTrialSummary.SampleSize"/>,
/// even when one candidate has far fewer ratings than another (Step 099's own implementation guidance,
/// verbatim: "report sample size honestly").
/// </summary>
public sealed class AestheticTrialSummaryReporter : IAestheticTrialSummaryReporter
{
    public IReadOnlyList<AestheticTrialSummary> Summarize(IReadOnlyList<AestheticRating> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        return ratings
            .GroupBy(r => r.CandidateId, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.ToList();
                return new AestheticTrialSummary(
                    group.Key,
                    SampleSize: items.Count,
                    MeanMusicality: items.Average(r => r.Musicality),
                    MeanClarity: items.Average(r => r.Clarity),
                    MeanContrast: items.Average(r => r.Contrast),
                    MeanExcitement: items.Average(r => r.Excitement),
                    MeanCleanupMinutes: items.Average(r => r.CleanupMinutes));
            })
            .OrderBy(summary => summary.CandidateId, StringComparer.Ordinal)
            .ToList();
    }
}
