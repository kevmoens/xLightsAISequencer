namespace XlightsAI.Studio.Release;

/// <summary>
/// Phase-local contract (disclosed). One candidate's honestly-aggregated result across every
/// <see cref="AestheticRating"/> actually recorded for it. <see cref="SampleSize"/> is always the real,
/// observed count — Step 099's own implementation guidance, verbatim: "report sample size honestly."
/// </summary>
/// <param name="CandidateId">The candidate this summary aggregates ratings for.</param>
/// <param name="SampleSize">The real number of ratings aggregated — never padded, estimated, or assumed equal across candidates.</param>
public sealed record AestheticTrialSummary(
    string CandidateId,
    int SampleSize,
    double MeanMusicality,
    double MeanClarity,
    double MeanContrast,
    double MeanExcitement,
    double MeanCleanupMinutes);
