namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 2 ("Gap filler draws from a rotating pool of ≥6 distinct states -- a single
/// repeated filler once occupied 36 seconds of one show," architecture §7). A fixed, deterministic
/// round-robin over a declared recipe-id pool -- never random (a reproducible compile must produce the
/// identical filler sequence every run) and never fewer than <see cref="MinimumDistinctStates"/> distinct
/// recipes.
/// </summary>
public sealed class FillerPool
{
    public const int MinimumDistinctStates = 6;

    private readonly IReadOnlyList<string> _recipeIds;

    /// <summary>
    /// <paramref name="recipeIds"/> may repeat an id -- a caller can list a disliked-but-not-forbidden
    /// effect (user feedback: "use Butterfly sparingly") once and the others two or three times each to
    /// dilute how often the rotation lands on it, rather than giving every state an equal 1-in-N share.
    /// Repeats are kept verbatim (not deduplicated) for that reason; only the *distinct* count is checked
    /// against <see cref="MinimumDistinctStates"/>, so "≥6 distinct states" still holds regardless of how
    /// many total slots the caller supplies.
    /// </summary>
    public FillerPool(IReadOnlyList<string> recipeIds)
    {
        ArgumentNullException.ThrowIfNull(recipeIds);
        var distinctCount = recipeIds.Distinct(StringComparer.Ordinal).Count();
        if (distinctCount < MinimumDistinctStates)
        {
            throw new ArgumentException(
                $"A filler pool needs at least {MinimumDistinctStates} distinct recipe ids (architecture §7's own 'rotating pool of ≥6 distinct states'); got {distinctCount}.",
                nameof(recipeIds));
        }

        _recipeIds = recipeIds.ToList();
    }

    public int Count => _recipeIds.Count;

    /// <summary>The recipe id at <paramref name="cursor"/>'s own rotating position (<c>cursor % Count</c>) -- deterministic for a given cursor value, never random.</summary>
    public string RecipeIdAt(int cursor) => _recipeIds[((cursor % _recipeIds.Count) + _recipeIds.Count) % _recipeIds.Count];
}
