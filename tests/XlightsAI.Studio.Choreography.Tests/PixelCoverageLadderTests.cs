using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 4 task 3 and acceptance criterion: "Pixel coverage ladder is monotone and matches
/// hand-computed expectations for at least 3 energy levels." Uses a small synthetic layout whose pixel
/// spread (20px MiniStar up to 1488px Matrix) mirrors the real reference house's own 74x spread
/// (EXPORT_AUDIT.md finding #2) so the ladder's behavior is realistic, not merely monotone-by-construction
/// on trivially equal-sized models.
/// </summary>
public sealed class PixelCoverageLadderTests
{
    /// <summary>MiniStar x4 (20px each, 80 total), Arch x2 (200px each, 400 total), Tree (900px), Matrix (1488px) -- total 2868px, modeled on the reference house's own real spread.</summary>
    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("MiniStar-1"), "MiniStar-1", "Star", [], [], NodeCount: 20),
            new DisplayModel(new ModelId("MiniStar-2"), "MiniStar-2", "Star", [], [], NodeCount: 20),
            new DisplayModel(new ModelId("MiniStar-3"), "MiniStar-3", "Star", [], [], NodeCount: 20),
            new DisplayModel(new ModelId("MiniStar-4"), "MiniStar-4", "Star", [], [], NodeCount: 20),
            new DisplayModel(new ModelId("Arch-1"), "Arch-1", "Arches", [], [], NodeCount: 200),
            new DisplayModel(new ModelId("Arch-2"), "Arch-2", "Arches", [], [], NodeCount: 200),
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            new DisplayModel(new ModelId("Matrix"), "Matrix", "Matrix", [], [], NodeCount: 1488),
        ],
        Groups: [],
        Warnings: []);

    [Fact]
    public void Build_ProducesAMonotoneSupersetChain()
    {
        var rungs = PixelCoverageLadder.Build(BuildLayout(), rungCount: 7);

        Assert.Equal(7, rungs.Count);
        Assert.True(PixelCoverageLadder.IsMonotoneSupersetChain(rungs));

        // Strictly non-decreasing coverage, and the last rung lights every model (100%).
        for (var i = 1; i < rungs.Count; i++)
        {
            Assert.True(rungs[i].CumulativePixels >= rungs[i - 1].CumulativePixels);
        }

        Assert.Equal(2868, rungs[^1].TotalPixels);
        Assert.Equal(2868, rungs[^1].CumulativePixels);
        Assert.Equal(8, rungs[^1].Models.Count);
    }

    [Fact]
    public void Build_LightestModelsPopulateEarlyRungs_PixelHeaviestOnlyJoinAtHighRungs()
    {
        var rungs = PixelCoverageLadder.Build(BuildLayout(), rungCount: 7);

        // The Matrix (1488px, 51.9% of the house alone) cannot appear until a rung whose own cumulative
        // target can actually accommodate it -- never in rung 1 (the lowest-energy rung).
        var firstRungWithMatrix = rungs.First(r => r.Models.Contains(new ModelId("Matrix")));
        Assert.True(firstRungWithMatrix.Index > 1, $"Matrix (the single heaviest prop) joined too early, at rung {firstRungWithMatrix.Index}.");

        // The four MiniStars (lightest) are all present by the second rung.
        var secondRung = rungs[1];
        Assert.Equal(4, secondRung.Models.Count(m => m.Value.StartsWith("MiniStar", StringComparison.Ordinal)));
    }

    /// <summary>
    /// "Matches hand-computed expectations for at least 3 energy levels" -- exact hand-computed
    /// cumulative pixel counts for rungs 1, 2 and 4 of the fixture layout above (models added
    /// lightest-first, in order [20,20,20,20,200,200,900,1488]; target_k = 2868*k/7):
    /// rung 1 (target 409.7px): add until cumulative &gt;= target -&gt; 20+20+20+20+200+200 = 480.
    /// rung 2 (target 819.4px): 480 &lt; target, add Tree (900) -&gt; 480+900 = 1380.
    /// rung 4 (target 1638.9px): 1380 &lt; target, add Matrix (1488) -&gt; 1380+1488 = 2868 (everything).
    /// </summary>
    [Theory]
    [InlineData(1, 480)]
    [InlineData(2, 1380)]
    [InlineData(4, 2868)]
    public void Build_MatchesHandComputedCumulativePixelsAtKnownEnergyLevels(int rungIndex, int expectedCumulativePixels)
    {
        var rungs = PixelCoverageLadder.Build(BuildLayout(), rungCount: 7);
        var rung = rungs[rungIndex - 1];

        Assert.Equal(expectedCumulativePixels, rung.CumulativePixels);
    }

    [Fact]
    public void RungForEnergy_PicksSmallestRungAtOrAboveTheRequestedPercent()
    {
        var rungs = PixelCoverageLadder.Build(BuildLayout(), rungCount: 7);

        Assert.Equal(1, PixelCoverageLadder.RungForEnergy(rungs, 0.0).Index);
        Assert.Equal(7, PixelCoverageLadder.RungForEnergy(rungs, 1.0).Index);

        var midRung = PixelCoverageLadder.RungForEnergy(rungs, 0.5);
        Assert.True(midRung.TargetCoveragePercent >= 50.0);
    }
}
