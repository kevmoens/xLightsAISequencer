namespace XlightsAI.Studio.Pipeline;

/// <summary>PHASE-2b (build plan §2b.9; architecture §8b): the full set of retrievable exemplars plus cosine-similarity retrieval by query fingerprint. See <see cref="ExemplarIndexBuilder"/> for how this is built.</summary>
public sealed record ExemplarIndex(DateTimeOffset GeneratedAtUtc, IReadOnlyList<ExemplarEntry> Entries)
{
    /// <summary>
    /// Returns the <paramref name="count"/> entries whose <see cref="ExemplarFingerprint.ToComparisonVector"/>
    /// is most cosine-similar to <paramref name="query"/>'s, descending. Cosine was chosen over Euclidean
    /// distance because the comparison vector mixes an equally-weighted normalized-tempo and
    /// energy-percentile axis (both already in <c>[0,1]</c>) — cosine similarity here reduces to comparing
    /// the vectors' *direction* (relative balance of the two axes) rather than raw magnitude, which is the
    /// more natural notion of "similar" when every axis is already unit-scaled and equally weighted; either
    /// choice is defensible at this dimensionality, and this is a disclosed, swappable choice, not a derived
    /// requirement.
    /// </summary>
    public IReadOnlyList<ExemplarEntry> FindSimilar(ExemplarFingerprint query, int count)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (count <= 0)
        {
            return [];
        }

        var queryVector = query.ToComparisonVector();
        return Entries
            .Select(entry => (Entry: entry, Similarity: CosineSimilarity(queryVector, entry.Fingerprint.ToComparisonVector())))
            .OrderByDescending(x => x.Similarity)
            .Take(count)
            .Select(x => x.Entry)
            .ToList();
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
