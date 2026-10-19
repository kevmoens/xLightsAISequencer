namespace XlightsAI.Studio.Release;

/// <summary>
/// PHASE-16 Section 3 proposed contract. Step 099 ("Conduct A/B musicality evaluations and
/// manual-effort measurement on varied songs"): one human reviewer's own rating of one candidate
/// within one blind trial (<see cref="AnonymousTrialId"/> — see <see cref="IBlindTrialOrderingService"/>).
/// Required acceptance, verbatim: "Collect preference ratings and editing-time results, not only auto
/// scores" — <see cref="CleanupMinutes"/> is the manual-effort half of that requirement; the four
/// 1-5 integer dimensions are the preference half.
/// </summary>
/// <param name="AnonymousTrialId">
/// The blind trial this rating belongs to (<see cref="BlindTrial.AnonymousTrialId"/>) — ties together
/// every candidate rated within one randomized-order review session without exposing which candidate
/// was shown first.
/// </param>
/// <param name="CandidateId">The real candidate identity this one rating is actually about.</param>
/// <param name="Musicality">1-5 (<see cref="MinRatingScore"/>-<see cref="MaxRatingScore"/>): does the choreography track the music.</param>
/// <param name="Clarity">1-5: is the pattern readable/uncluttered.</param>
/// <param name="Contrast">1-5: do sections/phrases read as visually distinct.</param>
/// <param name="Excitement">1-5: subjective overall excitement.</param>
/// <param name="CleanupMinutes">
/// Real, reported minutes the reviewer estimates they would need to manually clean up this candidate
/// before it is show-ready. Zero is a legitimate rating (needs no cleanup); must not be negative.
/// </param>
/// <param name="RecordedUtc">
/// Phase-local additive field (not in the Section 3 proposed shape; disclosed, same additive pattern
/// Step 098 used for <see cref="TestEvidence.Detail"/>): when this rating was actually recorded. Step
/// 099's own user-visible outcome, verbatim, names "timestamp provenance" — the Section-3-proposed
/// shape alone carries none, so this is required to satisfy that text. Defaulted so any
/// Section-3-proposed positional construction still compiles unchanged; <see cref="FileAestheticTrialCollector"/>
/// always overwrites whatever value a caller supplies here with its own real, observed UTC clock reading
/// at the moment of persistence — this field records when the rating was actually stored, never a
/// caller-asserted, possibly stale or clock-skewed value.
/// </param>
public sealed record AestheticRating(
    string AnonymousTrialId,
    string CandidateId,
    int Musicality,
    int Clarity,
    int Contrast,
    int Excitement,
    double CleanupMinutes,
    DateTimeOffset RecordedUtc = default)
{
    public const int MinRatingScore = 1;
    public const int MaxRatingScore = 5;

    /// <exception cref="AestheticRatingInvalidException">
    /// <see cref="AnonymousTrialId"/> or <see cref="CandidateId"/> is blank; any of
    /// <see cref="Musicality"/>/<see cref="Clarity"/>/<see cref="Contrast"/>/<see cref="Excitement"/> is
    /// outside <see cref="MinRatingScore"/>-<see cref="MaxRatingScore"/>; or <see cref="CleanupMinutes"/>
    /// is negative or not a finite number.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AnonymousTrialId))
        {
            throw new AestheticRatingInvalidException("AestheticRating.AnonymousTrialId must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(CandidateId))
        {
            throw new AestheticRatingInvalidException("AestheticRating.CandidateId must not be blank.");
        }

        ValidateScore(nameof(Musicality), Musicality);
        ValidateScore(nameof(Clarity), Clarity);
        ValidateScore(nameof(Contrast), Contrast);
        ValidateScore(nameof(Excitement), Excitement);

        if (double.IsNaN(CleanupMinutes) || double.IsInfinity(CleanupMinutes) || CleanupMinutes < 0)
        {
            throw new AestheticRatingInvalidException(
                $"AestheticRating.CleanupMinutes must be a non-negative finite number (was {CleanupMinutes}).");
        }
    }

    private static void ValidateScore(string fieldName, int value)
    {
        if (value < MinRatingScore || value > MaxRatingScore)
        {
            throw new AestheticRatingInvalidException(
                $"AestheticRating.{fieldName} must be between {MinRatingScore} and {MaxRatingScore} inclusive (was {value}).");
        }
    }
}
