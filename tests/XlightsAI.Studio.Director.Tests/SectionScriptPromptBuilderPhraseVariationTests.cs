using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 10 §2e/§3e task 3: the Choreographer's own volatile per-section suffix should list this
/// section's own real phrases (so a declared <c>phraseVariations</c> entry's 1-based phraseIndex has
/// something real to reference), and its stable prefix should carry the §2e staging guidance -- explicitly
/// optional -- verbatim.
/// </summary>
public sealed class SectionScriptPromptBuilderPhraseVariationTests
{
    private static SectionLabelCandidate BuildSection(Guid id, SectionKind kind = SectionKind.Chorus) =>
        new(id, Guid.NewGuid(), 0, 16, kind, 1.0, ["test"], false);

    private static BarGrid BuildBarGrid(int beatCount, long beatSpacingTicks, int beatsPerBar)
    {
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(60, 0.9, "4/4"),
            Enumerable.Range(0, beatCount).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false)).ToArray(),
            [], 0);
        return BarGrid.FromBeatGrid(beatGrid, beatCount * beatSpacingTicks);
    }

    private static SectionMusicContext BuildContext(BarGrid barGrid, TimeWindow window, SectionKind kind = SectionKind.Chorus) =>
        new(window, kind, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);

    private static PhraseCandidate Phrase(Guid sectionId, int startBar, int endBarExclusive) => new(
        Guid.NewGuid(), sectionId, startBar, endBarExclusive, startBar * 1_000_000L, endBarExclusive * 1_000_000L,
        endBarExclusive - startBar, PhraseBoundarySource.BarHypothesis, 1.0, ["test"], "hash", false);

    [Fact]
    public void BuildVolatileSuffix_RendersNumberedPhraseList_WhenPhrasesAreProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 64, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(16))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);
        var phrases = new[] { Phrase(section.Id, 0, 4), Phrase(section.Id, 4, 8), Phrase(section.Id, 8, 12), Phrase(section.Id, 12, 16) };

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, phrases: phrases);

        Assert.Contains("This section has 4 phrase(s):", suffix);
        Assert.Contains("Phrase 1 of 4: bars [0,4)", suffix);
        Assert.Contains("Phrase 4 of 4: bars [12,16)", suffix);
        Assert.Contains("phraseVariations", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_OmitsPhraseList_WhenNoPhrasesAreProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.DoesNotContain("This section has", suffix);
        Assert.DoesNotContain("phrase(s):", suffix);
    }

    [Fact]
    public void BuildStablePrefix_ContainsThe2eStagingGuidance_AndStatesItIsOptional()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var bible = ShowBibleContentFixture.BuildValid();
        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var prefix = SectionScriptPromptBuilder.BuildStablePrefix(bible, knowledge, "Traditional / Classic");

        Assert.Contains("Phrase-scoped sub-section staging", prefix);
        Assert.Contains("optional", prefix);
        Assert.Contains("phraseVariations", prefix);
    }
}
