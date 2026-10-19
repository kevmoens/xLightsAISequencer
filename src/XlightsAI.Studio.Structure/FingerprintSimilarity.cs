namespace XlightsAI.Studio.Structure;

/// <summary>
/// Compares two already-computed <see cref="BarFingerprint"/>s. Step 052's own "retain enough features
/// to explain matches" implementation guidance is satisfied by keeping every component feature
/// inspectable on <see cref="BarFingerprint.Features"/> itself; this type is the one declared way two
/// fingerprints are actually judged similar, so a later step (053+ motif matching) has one pinned
/// definition rather than each caller inventing its own distance metric.
/// </summary>
public static class FingerprintSimilarity
{
    /// <summary>
    /// Cosine similarity in <c>[-1, 1]</c> between two fingerprints' <see cref="BarFingerprint.Features"/>
    /// vectors -- scale-invariant by construction, which is what makes two gain-differing recordings of
    /// the same musical idea compare consistently (Step 052's own required acceptance, verbatim).
    /// Returns exactly <c>0.0</c>, never <see cref="double.NaN"/>, when either fingerprint is a zero
    /// vector (e.g. two silent bars) -- cosine similarity against a zero vector is mathematically
    /// undefined, and <c>0.0</c> is this step's own explicit choice rather than a fabricated "identical."
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> or <paramref name="b"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="a"/> and <paramref name="b"/> have a different <see cref="BarFingerprint.Features"/> dimensionality.</exception>
    public static double Cosine(BarFingerprint a, BarFingerprint b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Features.Count != b.Features.Count)
        {
            throw new ArgumentException(
                $"Fingerprints have different feature dimensionality ({a.Features.Count} vs {b.Features.Count}) and cannot be compared.");
        }

        double dot = 0.0, magA = 0.0, magB = 0.0;
        for (var i = 0; i < a.Features.Count; i++)
        {
            dot += a.Features[i] * b.Features[i];
            magA += a.Features[i] * a.Features[i];
            magB += b.Features[i] * b.Features[i];
        }

        if (magA <= 0.0 || magB <= 0.0)
        {
            return 0.0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
