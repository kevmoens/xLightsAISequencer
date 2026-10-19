namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 033's own "rank multiple tempo candidates" scan configuration, layered on top of the already
/// persisted <see cref="OnsetAnalysis"/> this step consumes (it does not re-decode audio or re-derive
/// onsets — see <see cref="ITempoDetector"/>). Recorded alongside every <see cref="TempoAnalysis"/> it
/// produces — part of the cache key (Section 4: "every cache key includes algorithm/model/settings
/// versions") exactly like <see cref="RhythmSettings"/> and <see cref="OnsetDetectionSettings"/>.
/// </summary>
/// <param name="MinBpm">Lowest BPM this scan considers. Must be positive and less than <see cref="MaxBpm"/>.</param>
/// <param name="MaxBpm">Highest BPM this scan considers. Must be greater than <see cref="MinBpm"/>.</param>
/// <param name="TimeResolutionSeconds">
/// Bin width, in seconds, the onset impulse train is quantized to before autocorrelation. Must be
/// positive. Smaller values resolve tempo more precisely but scan a proportionally larger lag range for
/// the same <see cref="MinBpm"/>/<see cref="MaxBpm"/> span.
/// </param>
/// <param name="MaxCandidates">Maximum number of ranked <see cref="TempoHypothesis"/> entries <see cref="TempoAnalysis.Candidates"/> may contain. Must be positive.</param>
public sealed record TempoDetectionSettings(double MinBpm, double MaxBpm, double TimeResolutionSeconds, int MaxCandidates);
