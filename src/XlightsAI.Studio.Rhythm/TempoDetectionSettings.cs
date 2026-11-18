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
/// <param name="PreferredBpmMin">
/// PHASE-28 §3.1 — lower bound of the metrical-level resolution's "this is a plausible beat, not a bar or
/// a half-beat" window. <see cref="MinBpm"/>/<see cref="MaxBpm"/> stay the raw autocorrelation scan range;
/// this narrower band only steers which metrical level (lag, lag/2, lag/3, lag/4, 2*lag, 3*lag) of the
/// strongest peak is reported as the resolved beat period.
/// </param>
/// <param name="PreferredBpmMax">Upper bound of the same metrical-level preferred window — see <see cref="PreferredBpmMin"/>.</param>
/// <param name="BeatLevelTolerance">
/// PHASE-28 §3.1 — how far a candidate metrical level's own autocorrelation score is allowed to fall
/// below the strongest peak's own score and still be accepted as "close enough": a candidate qualifies
/// when its score is at least <c>(1 - BeatLevelTolerance)</c> of the strongest peak's score (e.g. the
/// default <c>0.80</c> accepts anything down to 20% of the best — a true beat period is routinely far
/// less self-similar under raw autocorrelation than the bar period it subdivides, so this is
/// deliberately a wide allowance, not a near-equality check). Must be in <c>(0, 1]</c>.
/// </param>
/// <param name="CandidateMeters">
/// PHASE-28 §3.1 — meter numerators (beats per bar) tried by real meter detection, e.g. <c>[2, 3, 4, 6]</c>
/// for 2/4, 3/4, 4/4, 6/4 (sometimes called 6/8 at the half-note level). <see langword="null"/> means
/// "use the default set" (<see cref="DefaultCandidateMeters"/>) — the same nullable-optional-signal
/// convention already used elsewhere in this codebase (e.g. <c>MusicModel</c>'s own optional analyses).
/// </param>
public sealed record TempoDetectionSettings(
    double MinBpm,
    double MaxBpm,
    double TimeResolutionSeconds,
    int MaxCandidates,
    double PreferredBpmMin = 85,
    double PreferredBpmMax = 190,
    double BeatLevelTolerance = 0.80,
    int[]? CandidateMeters = null)
{
    /// <summary>The meter numerators tried when <see cref="CandidateMeters"/> is <see langword="null"/>.</summary>
    public static readonly int[] DefaultCandidateMeters = [2, 3, 4, 6];

    /// <summary><see cref="CandidateMeters"/> if supplied, otherwise <see cref="DefaultCandidateMeters"/>.</summary>
    public int[] EffectiveCandidateMeters => CandidateMeters ?? DefaultCandidateMeters;

    /// <summary>
    /// Arrays compare by reference under the compiler-generated record equality, which would make two
    /// settings instances with identical, equivalent <see cref="CandidateMeters"/> content compare as
    /// unequal — silently defeating the cache-validity check every store in this project relies on
    /// (<c>existing.Settings == settings</c>). This override restores real value equality.
    /// </summary>
    public bool Equals(TempoDetectionSettings? other) =>
        other is not null
        && MinBpm.Equals(other.MinBpm)
        && MaxBpm.Equals(other.MaxBpm)
        && TimeResolutionSeconds.Equals(other.TimeResolutionSeconds)
        && MaxCandidates == other.MaxCandidates
        && PreferredBpmMin.Equals(other.PreferredBpmMin)
        && PreferredBpmMax.Equals(other.PreferredBpmMax)
        && BeatLevelTolerance.Equals(other.BeatLevelTolerance)
        && EffectiveCandidateMeters.SequenceEqual(other.EffectiveCandidateMeters);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(MinBpm);
        hash.Add(MaxBpm);
        hash.Add(TimeResolutionSeconds);
        hash.Add(MaxCandidates);
        hash.Add(PreferredBpmMin);
        hash.Add(PreferredBpmMax);
        hash.Add(BeatLevelTolerance);
        foreach (var meter in EffectiveCandidateMeters)
        {
            hash.Add(meter);
        }

        return hash.ToHashCode();
    }
}
