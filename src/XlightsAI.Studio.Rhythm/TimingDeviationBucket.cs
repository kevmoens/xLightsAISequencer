namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// One bucket of <see cref="BeatGridQualityReport.TimingDeviationHistogram"/> — Step 037's own
/// implementation guidance, verbatim: "Add timing deviation histograms." Section 2's own half-open
/// <c>[start, end)</c> time-window convention applies here too.
/// </summary>
/// <param name="BucketStartTicks">Inclusive lower bound, in ticks, of this bucket's absolute-deviation range.</param>
/// <param name="BucketEndTicksExclusive">Exclusive upper bound of this bucket's absolute-deviation range.</param>
/// <param name="Count">Number of beats whose absolute deviation from the grid's own ideal, tempo-predicted position falls in this bucket.</param>
public sealed record TimingDeviationBucket(long BucketStartTicks, long BucketEndTicksExclusive, int Count);
