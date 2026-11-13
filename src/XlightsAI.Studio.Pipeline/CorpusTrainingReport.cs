namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 25 Part C: the persisted, aggregate-only output of one <c>CorpusTrainingRunner</c>
/// pass over an external (non-exemplar-eligible) sequence folder. Every field here is a count, ratio,
/// or histogram bucket — never a <c>ModelName</c>/<c>EffectName</c>/<c>SettingsString</c> copied verbatim
/// from a source <c>.xsq</c>, so this report can never become a backdoor way to redistribute a
/// third-party sequence's actual effect content under a different file name (see
/// <c>ExternalTrainingManifestBuilder</c>'s own doc comment for the licensing reasoning this respects).
/// <see cref="FailureReasons"/> carries only a song title (the sequence's own filename, already visible
/// on the user's own disk) plus an exception type/message — never any parsed effect content.
/// </summary>
public sealed record CorpusTrainingReport(
    DateTimeOffset GeneratedAtUtc,
    string ExternalSequencesRoot,
    int DiscoveredSequenceCount,
    int ExcludedSequenceCount,
    int AnalyzedSequenceCount,
    int FailedSequenceCount,
    IReadOnlyList<string> FailureReasons,
    CoverageRatioDistribution CatchAllCoverageRatioDistribution,
    WholeCanvasCoOccurrenceStats WholeCanvasCoOccurrence,
    LayerComplexityStats LayerComplexity,
    ReuseRateStats ReuseRate);

/// <summary>
/// Build-plan Phase 25 Part A/C: the real, measured distribution of <c>NodeCount / TotalPixels</c>
/// across every model group in every analyzed song whose show directory also carried its own
/// <c>xlights_rgbeffects.xml</c> alongside the <c>.xsq</c> (layout is optional for training ingestion;
/// a song with no discoverable layout simply contributes no samples here). Feeds Part A's
/// <c>WholeCanvasCoverageThreshold</c> calibration — group names are deliberately never recorded, only
/// the ratio values themselves.
/// </summary>
public sealed record CoverageRatioDistribution(
    int GroupSampleCount,
    int SongsWithLayoutCount,
    double? Min,
    double? Max,
    double? Median,
    double? P90,
    IReadOnlyDictionary<string, int> Histogram);

/// <summary>
/// Build-plan Phase 25 Part B.4/C: the real, measured aggregate of <see cref="WholeCanvasCoOccurrenceMiner"/>'s
/// per-window samples across every analyzed song that had at least one whole-canvas-classified
/// group/model — feeds Part B.2's prompt guidance. Every ratio/histogram here is aggregate-only; no
/// group/model/effect name is ever recorded (see <see cref="WholeCanvasCoOccurrenceMiner"/>'s own
/// licensing-boundary doc comment).
/// </summary>
/// <param name="WindowSampleCount">Total whole-canvas effect windows sampled across every analyzed song.</param>
/// <param name="SongsWithWholeCanvasCount">How many analyzed songs contributed at least one sample (had a real layout and a whole-canvas-classified group/model).</param>
/// <param name="FoundationCoFireRatio">Share of sampled windows where a Foundation-classified group/model also had an overlapping active effect, or <see langword="null"/> when no window was sampled.</param>
/// <param name="SubjectCoFireRatio">Same as <paramref name="FoundationCoFireRatio"/>, for Subject.</param>
/// <param name="AccentCoFireRatio">Same as <paramref name="FoundationCoFireRatio"/>, for Accent.</param>
/// <param name="SectionKindHistogram">Count of sampled windows per section kind name (or <c>"Unknown"</c> for a window outside every labeled section).</param>
/// <param name="EnergyTierHistogram">Count of sampled windows per energy tier (<c>"Low"</c>/<c>"Mid"</c>/<c>"High"</c>, each a third of the 0-1 percentile range) — windows with no measurable energy percentile are excluded, never fabricated into a tier.</param>
/// <param name="MedianDurationBars">Median window duration in bars across every sampled window with a measurable duration, or <see langword="null"/> when none had one.</param>
/// <param name="P90DurationBars">90th-percentile window duration in bars, same population as <paramref name="MedianDurationBars"/>.</param>
public sealed record WholeCanvasCoOccurrenceStats(
    int WindowSampleCount,
    int SongsWithWholeCanvasCount,
    double? FoundationCoFireRatio,
    double? SubjectCoFireRatio,
    double? AccentCoFireRatio,
    IReadOnlyDictionary<string, int> SectionKindHistogram,
    IReadOnlyDictionary<string, int> EnergyTierHistogram,
    double? MedianDurationBars,
    double? P90DurationBars);

/// <summary>
/// Build-plan Phase 25 Part C §3 bullet 3/Part D.1: the real, measured aggregate of
/// <see cref="LayerComplexityMiner"/>'s per-window samples across every analyzed song -- how many
/// models/groups reached 3+/4+/5+ simultaneous layers, broken down by section kind and duration, feeding
/// <see cref="MultiLayerStackMiner"/>'s own <c>minDistinctLayers</c> default and Part D.3-D.4's
/// complexity-bias weighting with real numbers instead of a guess. Group/model names are never recorded.
/// </summary>
/// <param name="ThreePlus">Every window reaching 3 or more concurrent layers (includes every window counted in <paramref name="FourPlus"/>/<paramref name="FivePlus"/> too).</param>
/// <param name="FourPlus">The subset of <paramref name="ThreePlus"/> reaching 4 or more.</param>
/// <param name="FivePlus">The subset of <paramref name="FourPlus"/> reaching 5 or more.</param>
public sealed record LayerComplexityStats(
    LayerComplexityTierStats ThreePlus,
    LayerComplexityTierStats FourPlus,
    LayerComplexityTierStats FivePlus);

/// <summary>One complexity tier's own aggregate within <see cref="LayerComplexityStats"/>.</summary>
/// <param name="WindowCount">How many real windows across the whole training run reached this tier's own layer-count floor.</param>
/// <param name="SectionKindHistogram">Count of this tier's own windows per section kind name (or <c>"Unknown"</c> for a window outside every labeled section).</param>
/// <param name="MedianDurationBars">Median window duration in bars across this tier's own windows with a measurable duration, or <see langword="null"/> when none had one.</param>
/// <param name="P90DurationBars">90th-percentile window duration in bars, same population as <paramref name="MedianDurationBars"/>.</param>
public sealed record LayerComplexityTierStats(
    int WindowCount,
    IReadOnlyDictionary<string, int> SectionKindHistogram,
    double? MedianDurationBars,
    double? P90DurationBars);

/// <summary>
/// Build-plan Phase 25 Part C §3 bullet 4/Part E: the real, measured aggregate of
/// <see cref="ReuseRateMiner"/>'s per-song structural-signature buckets across every analyzed song -- how
/// often a structurally similar passage (same phrase-length window, same section kind, similar energy
/// percentile) recurs within one real song. Feeds Part E's reuse-budget gauges (not built yet); group/
/// model/effect names are never recorded, only bucket occurrence counts.
/// </summary>
/// <param name="TotalBucketCount">Total distinct (phrase-length, section-kind, energy-tier) signatures observed across every analyzed song, counting the same signature separately in each song it appears in.</param>
/// <param name="TotalWindowCount">Total phrase windows these buckets represent (sum of every bucket's own occurrence count).</param>
/// <param name="ReusedBucketCount">How many of those buckets recurred at least once within their own song (occurrence count >= 2).</param>
/// <param name="RepeatedWindowCount">Across every reused bucket, how many of its windows were not the first occurrence -- i.e. the real count of "a passage that had already appeared earlier in this song, appearing again."</param>
/// <param name="OverallReuseRatio"><see cref="RepeatedWindowCount"/> divided by <see cref="TotalWindowCount"/>, or <see langword="null"/> when no window was sampled.</param>
/// <param name="MedianOccurrencesPerReusedBucket">Median occurrence count among only the reused buckets (>= 2), or <see langword="null"/> when none reused.</param>
/// <param name="P90OccurrencesPerReusedBucket">90th-percentile occurrence count, same population as <paramref name="MedianOccurrencesPerReusedBucket"/>.</param>
/// <param name="SectionKindHistogramOfReusedBuckets">Count of reused buckets per section kind name (or <c>"Unknown"</c> for a bucket with no majority section kind).</param>
public sealed record ReuseRateStats(
    int TotalBucketCount,
    int TotalWindowCount,
    int ReusedBucketCount,
    int RepeatedWindowCount,
    double? OverallReuseRatio,
    double? MedianOccurrencesPerReusedBucket,
    double? P90OccurrencesPerReusedBucket,
    IReadOnlyDictionary<string, int> SectionKindHistogramOfReusedBuckets);
