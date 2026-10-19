namespace XlightsAI.Studio.Release;

/// <summary>
/// Default <see cref="IBlindTrialOrderingService"/>. Uses a real Fisher-Yates shuffle
/// (<see cref="Random.Shuffle{T}(Span{T})"/>) over a fresh copy of the supplied candidate ids, never
/// the input list itself (the caller's own list is never mutated). A <see cref="Random"/> instance can
/// be injected for reproducible tests; the parameterless constructor uses <see cref="Random.Shared"/>
/// for real, actually-random review sessions.
/// </summary>
public sealed class BlindTrialOrderingService : IBlindTrialOrderingService
{
    private readonly Random _random;

    public BlindTrialOrderingService()
        : this(Random.Shared)
    {
    }

    public BlindTrialOrderingService(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    public BlindTrial CreateTrial(IReadOnlyList<string> candidateIds)
    {
        ArgumentNullException.ThrowIfNull(candidateIds);

        if (candidateIds.Count < 2)
        {
            throw new BlindTrialInvalidException(
                $"A blind trial requires at least two candidates (got {candidateIds.Count}).");
        }

        if (candidateIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new BlindTrialInvalidException("A blind trial's candidate ids must not be blank.");
        }

        var distinctCount = candidateIds.Distinct(StringComparer.Ordinal).Count();
        if (distinctCount != candidateIds.Count)
        {
            throw new BlindTrialInvalidException("A blind trial's candidate ids must not contain duplicates.");
        }

        var shuffled = candidateIds.ToArray();
        _random.Shuffle(shuffled);

        return new BlindTrial(Guid.NewGuid().ToString("N"), shuffled);
    }
}
