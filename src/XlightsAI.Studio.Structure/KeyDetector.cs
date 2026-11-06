namespace XlightsAI.Studio.Structure;

/// <param name="MinConfidence">The winning key profile's own correlation must clear this before a bar's own key is trusted at all -- below it, the bar is simply skipped (treated as "no confident key reading"), never forced to pick the best of a bad lot.</param>
/// <param name="MinStableBars">A newly-detected key must hold for at least this many consecutive bars before it is accepted as a real change -- a single-bar flicker (one bar's chroma briefly favoring a different key, common on a passing chord) is treated as noise, not a modulation.</param>
public sealed record KeyDetectionSettings(double MinConfidence = 0.15, int MinStableBars = 2);

/// <summary>
/// Build-plan "richer analysis tools" Phase 7 (2026-11-02): the Krumhansl-Kessler (1990) key-profile
/// correlation algorithm -- a standard, published music-theory technique, not a novel heuristic invented
/// for this codebase -- applied to each bar's own 12-bin chroma histogram already computed by
/// <see cref="BarFingerprintBuilder"/> (<c>BarFingerprintBuilder.EnergyPlusChromaRecipeId</c>). For each of
/// the 24 major/minor key profiles (12 pitch-class rotations x 2 modes), correlates the profile against
/// the bar's own chroma vector and keeps the best match; a real key CHANGE is only reported once the newly
/// winning key holds stable for <see cref="KeyDetectionSettings.MinStableBars"/> consecutive bars, so a
/// single passing chord never reads as a modulation. Only meaningful for a 12-bin chroma fingerprint
/// (standard chromatic pitch classes) -- any other <see cref="BarFingerprintSettings.ChromaBins"/> value
/// returns no key changes at all, rather than guessing against a profile shape that does not apply.
/// </summary>
public static class KeyDetector
{
    private const int ChromaticPitchClasses = 12;

    /// <summary>Krumhansl-Kessler (1990) major-key profile, tonic-relative (index 0 = tonic weight).</summary>
    private static readonly double[] MajorProfile = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88];

    /// <summary>Krumhansl-Kessler (1990) minor-key profile, tonic-relative (index 0 = tonic weight).</summary>
    private static readonly double[] MinorProfile = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17];

    private static readonly string[] PitchClassNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static readonly KeyDetectionSettings Default = new();

    public static IReadOnlyList<KeyChange> DetectKeyChanges(SongFingerprintSet fingerprints, KeyDetectionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);
        var effective = settings ?? Default;

        if (fingerprints.Settings.ChromaBins != ChromaticPitchClasses || fingerprints.Bars.Count == 0)
        {
            return [];
        }

        var perBar = fingerprints.Bars
            .Select(bar => bar.IsSilent ? (Key: string.Empty, Confidence: 0.0) : BestKey(bar.Features))
            .ToList();

        var changes = new List<KeyChange>();
        string? currentKey = null;
        for (var i = 0; i < perBar.Count; i++)
        {
            var (key, confidence) = perBar[i];
            if (key.Length == 0 || confidence < effective.MinConfidence || key == currentKey)
            {
                continue;
            }

            var stableThrough = Math.Min(perBar.Count, i + effective.MinStableBars);
            var stable = true;
            for (var j = i; j < stableThrough; j++)
            {
                if (perBar[j].Key != key)
                {
                    stable = false;
                    break;
                }
            }

            if (!stable)
            {
                continue;
            }

            currentKey = key;
            changes.Add(new KeyChange(fingerprints.Bars[i].Window.StartTicks, key, confidence));
        }

        return changes;
    }

    private static (string Key, double Confidence) BestKey(IReadOnlyList<double> features)
    {
        var chroma = features.Skip(2).Take(ChromaticPitchClasses).ToArray();

        var bestKey = string.Empty;
        var bestScore = double.NegativeInfinity;
        for (var root = 0; root < ChromaticPitchClasses; root++)
        {
            var majorScore = Correlate(chroma, Rotate(MajorProfile, root));
            if (majorScore > bestScore)
            {
                bestScore = majorScore;
                bestKey = $"{PitchClassNames[root]} Major";
            }

            var minorScore = Correlate(chroma, Rotate(MinorProfile, root));
            if (minorScore > bestScore)
            {
                bestScore = minorScore;
                bestKey = $"{PitchClassNames[root]} Minor";
            }
        }

        return (bestKey, bestScore);
    }

    private static double[] Rotate(double[] profile, int root)
    {
        var rotated = new double[ChromaticPitchClasses];
        for (var i = 0; i < ChromaticPitchClasses; i++)
        {
            rotated[i] = profile[(i - root + ChromaticPitchClasses) % ChromaticPitchClasses];
        }

        return rotated;
    }

    private static double Correlate(double[] a, double[] b)
    {
        var meanA = a.Average();
        var meanB = b.Average();

        double numerator = 0, denomA = 0, denomB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var da = a[i] - meanA;
            var db = b[i] - meanB;
            numerator += da * db;
            denomA += da * da;
            denomB += db * db;
        }

        var denom = Math.Sqrt(denomA * denomB);
        return denom > 0 ? numerator / denom : 0.0;
    }
}
