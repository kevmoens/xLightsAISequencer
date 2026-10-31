using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 12 §3b task 2: the Choreographer's own per-section volatile suffix should surface the
/// section's own measured target change-rate (<see cref="SectionDensityMapper"/>) when one is declared --
/// never an invented line when a caller predates this phase and leaves <see cref="SectionMusicContext.TargetChangeRatePerSecond"/>
/// at its own unconstrained default.
/// </summary>
public sealed class SectionScriptPromptBuilderDensityTargetTests
{
    private static SectionLabelCandidate BuildSection(Guid id) =>
        new(id, Guid.NewGuid(), 0, 2, SectionKind.Verse, 1.0, ["test"], false);

    private static BarGrid BuildBarGrid(int beatCount, long beatSpacingTicks, int beatsPerBar)
    {
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(60, 0.9, "4/4"),
            Enumerable.Range(0, beatCount).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false)).ToArray(),
            [], 0);
        return BarGrid.FromBeatGrid(beatGrid, beatCount * beatSpacingTicks);
    }

    [Fact]
    public void BuildVolatileSuffix_RendersTargetChangeRateLine_WhenDeclared()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var ceiling = 0.20;
        var context = new SectionMusicContext(
            window, SectionKind.Verse, ceiling, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false,
            TargetChangeRatePerSecond: SectionDensityMapper.TargetChangeRatePerSecond(ceiling));
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.Contains("Target change-rate for this section:", suffix);
        Assert.Contains("long base layer spanning most of this section", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_OmitsTargetChangeRateLine_WhenUnconstrainedDefault()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = new SectionMusicContext(window, SectionKind.Verse, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.DoesNotContain("Target change-rate for this section:", suffix);
    }
}
