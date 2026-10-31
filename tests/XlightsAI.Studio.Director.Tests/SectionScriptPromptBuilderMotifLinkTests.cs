using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 10 §3c tasks 2/3: the Choreographer's own volatile per-section suffix should name the
/// specific motif-linked predecessor section when one is available and differs from the existing same-Kind
/// occurrence text, and its stable prefix should carry the §2d leitmotif-and-escalate guidance verbatim.
/// </summary>
public sealed class SectionScriptPromptBuilderMotifLinkTests
{
    private static SectionLabelCandidate BuildSection(Guid id, SectionKind kind = SectionKind.Bridge) =>
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

    private static SectionMusicContext BuildContext(BarGrid barGrid, TimeWindow window, SectionKind kind = SectionKind.Bridge) =>
        new(window, kind, 1.0, barGrid, new Dictionary<string, TimingTrack>(), VocalActive: false);

    [Fact]
    public void BuildVolatileSuffix_RendersMotifLinkLine_WhenLinkIsProvidedAndKindDiffers()
    {
        var section = BuildSection(Guid.NewGuid(), SectionKind.Bridge);
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window, SectionKind.Bridge);
        var layout = new LayoutSummary(1, 100, [], []);

        var link = new SectionMotifLink(section.Id, Guid.NewGuid(), SectionKind.Chorus, 16, 20, 0.9);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, motifLink: link);

        Assert.Contains("Motif link: this section's melodic motif also appears in the Chorus at bars [16,20)", suffix);
        Assert.Contains("echo a recognizable visual signature", suffix);
        Assert.Contains("escalate", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_OmitsMotifLinkLine_WhenNoLinkIsProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.DoesNotContain("Motif link:", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_RendersMotifLinkLine_WhenLinkedPredecessorSharesThisSection_sKind()
    {
        // §2d's own primary example is exactly this case -- "chorus repeats, a recurring hook" -- so a
        // same-Kind motif link is still rendered: the existing "repeat N of M" occurrence text never names
        // a SPECIFIC earlier section by bar range, so this is never fully redundant with it.
        var section = BuildSection(Guid.NewGuid(), SectionKind.Chorus);
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window, SectionKind.Chorus);
        var layout = new LayoutSummary(1, 100, [], []);

        var link = new SectionMotifLink(section.Id, Guid.NewGuid(), SectionKind.Chorus, 0, 4, 0.9);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, motifLink: link);

        Assert.Contains("Motif link: this section's melodic motif also appears in the Chorus at bars [0,4)", suffix);
    }

    [Fact]
    public void BuildStablePrefix_ContainsThe2dLeitmotifGuidance()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var bible = ShowBibleContentFixture.BuildValid();
        var vocabulary = RealXLightsFixture.BuildCatalogs().Vocabulary;
        var knowledge = new DirectorKnowledgeContext(vocabulary, new CraftRules(DateTimeOffset.UtcNow, 0.7, 3, [], []), new ExemplarIndex(DateTimeOffset.UtcNow, []));

        var prefix = SectionScriptPromptBuilder.BuildStablePrefix(bible, knowledge, "Traditional / Classic");

        Assert.Contains("Motif leitmotif guidance", prefix);
        Assert.Contains("leitmotif", prefix);
        Assert.Contains("escalate", prefix);
    }
}
