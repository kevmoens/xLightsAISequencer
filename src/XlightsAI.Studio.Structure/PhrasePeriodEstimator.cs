namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 3: replaces <c>PhraseSegmentationSettings.TargetPhraseLengthBars</c>'s
/// previous hardcoded value of <c>8</c> with a statistical fit per song, using the exact methodology
/// already written up and validated in <c>C:\xLights\MUSIC_FLOW_ANALYSIS.md</c> §1 ("Phrase structure -
/// the 8-bar assumption fails"): bar-to-bar energy jumps whose magnitude exceeds <c>mean + 1 standard
/// deviation</c> are tested against each candidate grid size with a one-sided binomial test (does a jump
/// land on a multiple of the grid size more often than chance alone would predict?); the grid with the
/// smallest p-value below <see cref="DefaultSignificanceThreshold"/> wins. <see cref="DefaultFallbackBars"/>
/// (the previous hardcoded constant) is kept as the explicit fallback for a song with too little evidence
/// (too few bars, or no jump significant enough to favor any one grid) -- this is the same "irregular
/// input must not corrupt the result" convention used throughout Structure, applied to a statistical
/// fit instead of a parsed document.
/// </summary>
public static class PhrasePeriodEstimator
{
    /// <summary>The previous hardcoded <c>PhraseSegmentationSettings.TargetPhraseLengthBars</c> value, kept as the explicit fallback when no candidate grid clears the significance bar.</summary>
    public const int DefaultFallbackBars = 8;

    /// <summary>The grid sizes tested by default -- the same two <c>MUSIC_FLOW_ANALYSIS.md</c> §1 itself compares (4-bar vs. 8-bar).</summary>
    public static readonly IReadOnlyList<int> DefaultCandidateGridSizes = [4, 8];

    /// <summary>A candidate grid's p-value must fall strictly below this to be considered a real structural fit rather than noise.</summary>
    public const double DefaultSignificanceThreshold = 0.05;

    /// <summary>One candidate grid size's statistical fit against a song's own significant energy jumps.</summary>
    /// <param name="GridSizeBars">The grid size tested, in bars.</param>
    /// <param name="SignificantJumpCount">Total bar-to-bar energy jumps whose magnitude exceeded the mean+1&#963; threshold -- the same for every candidate grid (the jump set itself does not depend on grid size).</param>
    /// <param name="OnGridCount">How many of those jumps land on a multiple of <see cref="GridSizeBars"/>.</param>
    /// <param name="PValue">One-sided binomial survival probability <c>P(X &gt;= OnGridCount)</c> under the null hypothesis that jumps land uniformly at random (chance <c>1/GridSizeBars</c> per jump). Smaller means the grid explains the jumps better than chance.</param>
    public sealed record GridFit(int GridSizeBars, int SignificantJumpCount, int OnGridCount, double PValue);

    /// <summary>
    /// Picks the best-fitting <see cref="GridFit.GridSizeBars"/> among <paramref name="candidateGridSizes"/>
    /// (ordered by bar-index, as <see cref="IBarFingerprintBuilder"/> already produces them), falling back to
    /// <paramref name="fallbackBars"/> when no candidate's p-value clears <paramref name="significanceThreshold"/>.
    /// </summary>
    public static int EstimateTargetPhraseLengthBars(
        IReadOnlyList<BarFingerprint> bars,
        IReadOnlyList<int>? candidateGridSizes = null,
        double significanceThreshold = DefaultSignificanceThreshold,
        int fallbackBars = DefaultFallbackBars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        var fits = Fit(bars, candidateGridSizes ?? DefaultCandidateGridSizes);

        GridFit? best = null;
        foreach (var fit in fits)
        {
            if (fit.PValue < significanceThreshold && (best is null || fit.PValue < best.PValue))
            {
                best = fit;
            }
        }

        return best?.GridSizeBars ?? fallbackBars;
    }

    /// <summary>
    /// Computes one <see cref="GridFit"/> per candidate grid size, ordered to match <paramref name="candidateGridSizes"/>.
    /// Exposed separately from <see cref="EstimateTargetPhraseLengthBars"/> so a caller (or a test reproducing
    /// <c>MUSIC_FLOW_ANALYSIS.md</c>'s own table) can inspect every candidate's full fit, not just the winner.
    /// </summary>
    public static IReadOnlyList<GridFit> Fit(IReadOnlyList<BarFingerprint> bars, IReadOnlyList<int> candidateGridSizes)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(candidateGridSizes);

        if (candidateGridSizes.Count == 0)
        {
            throw new ArgumentException("At least one candidate grid size is required.", nameof(candidateGridSizes));
        }

        foreach (var gridSize in candidateGridSizes)
        {
            if (gridSize <= 0)
            {
                throw new ArgumentException($"Candidate grid size ({gridSize}) must be positive.", nameof(candidateGridSizes));
            }
        }

        var ordered = bars.OrderBy(b => b.BarIndex).ToList();
        var jumpBarIndices = FindSignificantJumpBarIndices(ordered);

        var fits = new List<GridFit>(candidateGridSizes.Count);
        foreach (var gridSize in candidateGridSizes)
        {
            var onGrid = jumpBarIndices.Count(bar => bar % gridSize == 0);
            var pValue = BinomialSurvival(onGrid, jumpBarIndices.Count, 1.0 / gridSize);
            fits.Add(new GridFit(gridSize, jumpBarIndices.Count, onGrid, pValue));
        }

        return fits;
    }

    /// <summary>
    /// Bar indices where a bar-to-bar energy jump (<c>|energy[i] - energy[i-1]|</c>, using each bar's own
    /// mean-energy feature -- <c>BarFingerprint.Features[0]</c>, the same quantity
    /// <see cref="IBarFingerprintBuilder"/> computes from <c>EnergyFrame.NormalizedEnergy</c>) exceeds
    /// <c>mean + 1 standard deviation</c> of every bar-to-bar jump magnitude in the song -- exactly
    /// <c>MUSIC_FLOW_ANALYSIS.md</c> §1's own threshold ("|drms| &gt; mean+1sigma").
    /// </summary>
    private static List<int> FindSignificantJumpBarIndices(IReadOnlyList<BarFingerprint> orderedBars)
    {
        if (orderedBars.Count < 2)
        {
            return [];
        }

        var jumps = new double[orderedBars.Count - 1];
        for (var i = 1; i < orderedBars.Count; i++)
        {
            jumps[i - 1] = Math.Abs(orderedBars[i].Features[0] - orderedBars[i - 1].Features[0]);
        }

        var mean = jumps.Average();
        var variance = jumps.Select(j => (j - mean) * (j - mean)).Average();
        var stdDev = Math.Sqrt(variance);
        var threshold = mean + stdDev;

        var result = new List<int>();
        for (var i = 0; i < jumps.Length; i++)
        {
            if (jumps[i] > threshold)
            {
                // The jump between bar i and bar i+1 is reported as landing at the later bar (the bar
                // where the energy actually changed into), matching MUSIC_FLOW_ANALYSIS.md's own
                // "where the 47 significant bar-to-bar energy jumps... land" framing.
                result.Add(orderedBars[i + 1].BarIndex);
            }
        }

        return result;
    }

    /// <summary>One-sided binomial survival probability <c>P(X &gt;= k)</c> for <c>X ~ Binomial(n, p)</c>.</summary>
    private static double BinomialSurvival(int k, int n, double p)
    {
        if (n <= 0)
        {
            return 1.0;
        }

        double sum = 0.0;
        for (var i = k; i <= n; i++)
        {
            sum += BinomialPmf(n, i, p);
        }

        return Math.Clamp(sum, 0.0, 1.0);
    }

    private static double BinomialPmf(int n, int k, double p)
    {
        if (p <= 0.0)
        {
            return k == 0 ? 1.0 : 0.0;
        }

        if (p >= 1.0)
        {
            return k == n ? 1.0 : 0.0;
        }

        var logPmf = LogChoose(n, k) + (k * Math.Log(p)) + ((n - k) * Math.Log(1.0 - p));
        return Math.Exp(logPmf);
    }

    private static double LogChoose(int n, int k) =>
        LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k);

    private static double LogFactorial(int n) => LogGamma(n + 1);

    /// <summary>Lanczos approximation of ln(Gamma(x)) for x &gt; 0 -- .NET has no built-in Gamma/LogGamma function.</summary>
    private static double LogGamma(int n)
    {
        double x = n;
        double[] g =
        [
            676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059,
            12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7,
        ];

        if (x < 0.5)
        {
            return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - LogGammaPositive(1 - x);
        }

        return LogGammaPositive(x);

        double LogGammaPositive(double y)
        {
            y -= 1;
            var a = 0.99999999999980993;
            var t = y + 7.5;
            for (var i = 0; i < g.Length; i++)
            {
                a += g[i] / (y + i + 1);
            }

            return (0.5 * Math.Log(2 * Math.PI)) + ((y + 0.5) * Math.Log(t)) - t + Math.Log(a);
        }
    }
}
