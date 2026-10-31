using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 3 — "replace PhraseSegmenter's hardcoded
/// TargetPhraseLengthBars: 8 with a statistical fit per song, using the same methodology documented in
/// C:\xLights\MUSIC_FLOW_ANALYSIS.md (test 4-bar vs 8-bar grids against the top energy-jump
/// boundaries)." Exercises <see cref="PhrasePeriodEstimator"/> directly against synthetic, exactly
/// controllable per-bar energy sequences -- unlike the real reference track, every "significant jump"
/// bar index here is a known, deliberately placed fixture value, so the binomial fit's result is fully
/// predictable rather than merely "probably directionally right."
/// </summary>
public sealed class Phase03Task3PhrasePeriodEstimatorAcceptanceTests
{
    private const long BarTicks = 10_000_000L;

    private static BarFingerprint Bar(int index, double meanEnergy) =>
        new(index, new TimeWindow(index * BarTicks, (index + 1) * BarTicks), [meanEnergy, 0.0], Confidence: 1.0, IsSilent: false);

    /// <summary>
    /// A square wave alternating baseline/high energy every <paramref name="gridBars"/> bars -- every
    /// rising and falling bar-to-bar jump this produces lands exactly on a multiple of
    /// <paramref name="gridBars"/> (a sustained plateau, not a single-bar spike, so there is exactly one
    /// jump per transition rather than a spurious extra jump on the plateau's own far edge). A
    /// deterministic analogue of MUSIC_FLOW_ANALYSIS.md's own real-track finding that this track's jumps
    /// cluster on 4-bar lines.
    /// </summary>
    private static IReadOnlyList<BarFingerprint> BuildGridAlignedEnergy(int barCount, int gridBars, double baseline = 0.2, double spike = 0.9)
    {
        var bars = new List<BarFingerprint>(barCount);
        for (var i = 0; i < barCount; i++)
        {
            var energy = (i / gridBars) % 2 == 1 ? spike : baseline;
            bars.Add(Bar(i, energy));
        }

        return bars;
    }

    [Fact]
    public void EstimateTargetPhraseLengthBars_JumpsClusterOn4BarLines_Picks4()
    {
        var bars = BuildGridAlignedEnergy(barCount: 96, gridBars: 4);

        var fits = PhrasePeriodEstimator.Fit(bars, [4, 8]);
        var fourBar = fits.Single(f => f.GridSizeBars == 4);
        var eightBar = fits.Single(f => f.GridSizeBars == 8);

        // Every jump lands on a multiple of 4 by construction -- a maximally significant fit.
        Assert.Equal(fourBar.SignificantJumpCount, fourBar.OnGridCount);
        Assert.True(fourBar.PValue < PhrasePeriodEstimator.DefaultSignificanceThreshold);

        // 4-bar strictly wins over 8-bar, mirroring MUSIC_FLOW_ANALYSIS.md §1's own qualitative result:
        // "this track does not have a reliable 8-bar phrase grid... its structural moves lock to 4-bar
        // lines."
        Assert.True(fourBar.PValue < eightBar.PValue);

        var target = PhrasePeriodEstimator.EstimateTargetPhraseLengthBars(bars);
        Assert.Equal(4, target);
    }

    [Fact]
    public void EstimateTargetPhraseLengthBars_JumpsClusterOn8BarLines_Picks8()
    {
        var bars = BuildGridAlignedEnergy(barCount: 96, gridBars: 8);

        var target = PhrasePeriodEstimator.EstimateTargetPhraseLengthBars(bars);

        Assert.Equal(8, target);
    }

    [Fact]
    public void EstimateTargetPhraseLengthBars_NoSignificantStructure_FallsBackToDefault()
    {
        // Every bar at the same energy: no jumps at all exceed the mean+1sigma threshold (the threshold
        // itself is 0 above a uniform mean, so nothing is "significant" -- no evidence either way).
        var bars = Enumerable.Range(0, 32).Select(i => Bar(i, 0.5)).ToList();

        var target = PhrasePeriodEstimator.EstimateTargetPhraseLengthBars(bars);

        Assert.Equal(PhrasePeriodEstimator.DefaultFallbackBars, target);
    }

    [Fact]
    public void EstimateTargetPhraseLengthBars_TooFewBars_FallsBackToDefault_NeverThrows()
    {
        Assert.Equal(PhrasePeriodEstimator.DefaultFallbackBars, PhrasePeriodEstimator.EstimateTargetPhraseLengthBars([]));
        Assert.Equal(PhrasePeriodEstimator.DefaultFallbackBars, PhrasePeriodEstimator.EstimateTargetPhraseLengthBars([Bar(0, 0.5)]));
    }

    [Fact]
    public void Fit_InvalidCandidateGridSizes_ThrowsArgumentException()
    {
        var bars = BuildGridAlignedEnergy(barCount: 16, gridBars: 4);

        Assert.Throws<ArgumentException>(() => PhrasePeriodEstimator.Fit(bars, []));
        Assert.Throws<ArgumentException>(() => PhrasePeriodEstimator.Fit(bars, [0]));
        Assert.Throws<ArgumentException>(() => PhrasePeriodEstimator.Fit(bars, [-4]));
    }

    [Fact]
    public void Fit_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PhrasePeriodEstimator.Fit(null!, [4, 8]));
        Assert.Throws<ArgumentNullException>(() => PhrasePeriodEstimator.Fit(BuildGridAlignedEnergy(16, 4), null!));
    }
}
