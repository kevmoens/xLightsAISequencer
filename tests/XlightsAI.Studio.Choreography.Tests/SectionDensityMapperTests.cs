namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 12 §3a: <see cref="SectionDensityMapper"/> should turn architecture §10's own declared
/// energy-ceiling range into a measurably wider change-rate spread than the ~1.2x the 2026-10-30 real-run
/// comparison measured, approaching its ~4.7x real-world reference spread -- the one real, checkable
/// property this whole phase exists to produce.
/// </summary>
public sealed class SectionDensityMapperTests
{
    [Fact]
    public void TargetChangeRatePerSecond_IsMonotonicallyIncreasingWithCeiling()
    {
        var low = SectionDensityMapper.TargetChangeRatePerSecond(0.10);
        var mid = SectionDensityMapper.TargetChangeRatePerSecond(0.50);
        var high = SectionDensityMapper.TargetChangeRatePerSecond(0.85);

        Assert.True(low < mid);
        Assert.True(mid < high);
    }

    [Fact]
    public void TargetChangeRatePerSecond_ArchitectureSection10Range_SpreadApproachesRealWorldFiveX()
    {
        // Architecture §10's own declared ceiling table spans [0.10 (Outro), 0.85 (Chorus)] --
        // DefaultEnergyCeilings.ArchitectureSection10.
        var quietest = SectionDensityMapper.TargetChangeRatePerSecond(0.10);
        var loudest = SectionDensityMapper.TargetChangeRatePerSecond(0.85);

        var spread = loudest / quietest;

        Assert.True(spread >= 4.0, $"spread {spread:0.00}x should approach the real-world ~4.7x reference, not the pre-Phase-12 ~1.2x flat baseline.");
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void TargetChangeRatePerSecond_ClampsOutOfRangeCeiling_NeverThrows(double ceiling)
    {
        var rate = SectionDensityMapper.TargetChangeRatePerSecond(ceiling);

        Assert.True(rate > 0);
        Assert.True(double.IsFinite(rate));
    }
}
