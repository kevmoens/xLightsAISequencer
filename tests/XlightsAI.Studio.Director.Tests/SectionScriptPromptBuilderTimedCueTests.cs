using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 1 (2026-11-01): <see cref="SectionScriptPromptBuilder.BuildVolatileSuffix"/>'s own real-time/timed-cue rendering.</summary>
public sealed class SectionScriptPromptBuilderTimedCueTests
{
    private static SectionLabelCandidate BuildSection(Guid id, SectionKind kind = SectionKind.Chorus) =>
        new(id, Guid.NewGuid(), 0, 2, kind, 1.0, ["test"], false);

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
    public void BuildVolatileSuffix_AlwaysRendersRealTimeLine()
    {
        var section = BuildSection(Guid.NewGuid());
        // 1 second/beat, 4 beats/bar -> 4 seconds/bar; bars [0,2) -> real time [0:00, 0:08).
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = new SectionMusicContext(window, section.Kind, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.Contains("Real time: 0:00-0:08", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_RendersOverlappingTimedCues_WithInstructionAndTolerance()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value; // real time [0,8)
        var context = new SectionMusicContext(window, section.Kind, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);
        var layout = new LayoutSummary(1, 100, [], []);

        var cue = new TimedCue(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(5), "full on and off to hit the word bang", TimeSpan.FromSeconds(1.5));

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, timedCues: [cue]);

        Assert.Contains("Timed cues from the user's own creative direction", suffix);
        Assert.Contains("0:04-0:05", suffix);
        Assert.Contains("full on and off to hit the word bang", suffix);
        Assert.Contains("±1.5s", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_OmitsTimedCueBlock_WhenNoCuesSupplied()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = new SectionMusicContext(window, section.Kind, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.DoesNotContain("Timed cues", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_SingleInstantCue_RendersJustOneTimestamp_NotARange()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = new SectionMusicContext(window, section.Kind, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);
        var layout = new LayoutSummary(1, 100, [], []);

        var cue = new TimedCue(TimeSpan.FromSeconds(4), null, "big reveal here", TimeSpan.FromSeconds(1.5));

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, timedCues: [cue]);

        Assert.Contains("Around 0:04 (within about ±1.5s): big reveal here", suffix);
    }
}
