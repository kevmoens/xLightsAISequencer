using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 10 §3b tasks 2/3: the Choreographer's own volatile per-section suffix should render a
/// real melodic contour digest line when one is available, and its stable prefix should carry the §2c
/// craft rules verbatim -- never a placeholder/invented line when there is no real melody data.
/// </summary>
public sealed class SectionScriptPromptBuilderMelodicContourTests
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

    private static SectionMusicContext BuildContext(BarGrid barGrid, TimeWindow window) =>
        new(window, SectionKind.Verse, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);

    [Fact]
    public void BuildVolatileSuffix_RendersMelodicContourLine_WhenBriefIsProvided()
    {
        var sectionId = Guid.NewGuid();
        var section = BuildSection(sectionId);
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var brief = new SectionMelodicBrief(sectionId, MelodicContourShape.Rising, NoteDensity: 1.5, PeakNoteBeatOffset: 2);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, melodicBrief: brief);

        Assert.Contains("Melodic contour: Rising, high note density, peak at beat 3 of 8.", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_OmitsMelodicContourLine_WhenNoBriefIsProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.DoesNotContain("Melodic contour:", suffix);
    }

    [Fact]
    public void BuildStablePrefix_ContainsThe2cMelodicContourGuidance()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var bible = ShowBibleContentFixture.BuildValid();
        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var prefix = SectionScriptPromptBuilder.BuildStablePrefix(bible, knowledge, "Traditional / Classic");

        Assert.Contains("Melodic contour guidance", prefix);
        Assert.Contains("Rising contour", prefix);
        Assert.Contains("Falling contour", prefix);
        Assert.Contains("High note density", prefix);
        Assert.Contains("Static/Sparse contour", prefix);
    }
}
