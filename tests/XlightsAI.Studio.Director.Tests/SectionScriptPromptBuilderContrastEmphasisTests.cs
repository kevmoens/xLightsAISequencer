using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 22 §2b (contrast-driven emphasis): strong, repeated, worked-example Choreographer
/// prompt guidance for dim/off-before-bright contrast at every real emphasis instant -- lives in the
/// STABLE prefix (never varies per section), plus the real transient-hit timestamps this phase surfaces
/// in the volatile suffix for the first time (vocal-note emphasis/timed cues were already surfaced).
/// </summary>
public sealed class SectionScriptPromptBuilderContrastEmphasisTests
{
    [Fact]
    public void BuildStablePrefix_IncludesContrastDrivenEmphasisGuidance_WithAWorkedExample()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var bible = ShowBibleContentFixture.BuildValid();
        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var prefix = SectionScriptPromptBuilder.BuildStablePrefix(bible, knowledge, "Traditional / Classic");

        Assert.Contains("Contrast-driven emphasis", prefix);
        Assert.Contains("PRIMARY mechanism", prefix);
        Assert.Contains("WORKED EXAMPLE", prefix);
        Assert.Contains("dim-then-bright", prefix);
    }

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
    public void BuildVolatileSuffix_RendersTransientHitLine_WhenHitsAreProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var hits = new[] { new TransientHit(10_000_000, 0.9f), new TransientHit(25_000_000, 0.8f) };

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, transientHits: hits);

        Assert.Contains("Transient hits", suffix);
        Assert.Contains("0:01", suffix);
        Assert.Contains("0:02", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_OmitsTransientHitLine_WhenNoHitsAreProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.DoesNotContain("Transient hits", suffix);
    }
}
