using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// User-requested real investigation (2026-11-08): the Choreographer's own volatile per-section suffix
/// should render real, measured vocal-note-onset timestamps when available (<see cref="VocalNoteEmphasisBriefBuilder"/>'s
/// output) -- never a placeholder/invented line when there is no real melody data for this section.
/// </summary>
public sealed class SectionScriptPromptBuilderVocalNoteEmphasisTests
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

    private static NoteEvent Note(long startTicks) =>
        new(Guid.NewGuid(), startTicks, startTicks + 5_000_000, MidiNote: 64, Velocity: 0.9, Confidence: 1.0, UserCorrected: false);

    [Fact]
    public void BuildVolatileSuffix_RendersVocalNoteEmphasisLine_WhenNotesAreProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var notes = new[] { Note(10_000_000), Note(25_000_000) };

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout, salientVocalNotes: notes);

        Assert.Contains("Vocal note emphasis", suffix);
        Assert.Contains("0:01", suffix);
        Assert.Contains("0:02", suffix);
        Assert.Contains("NOT lyric words", suffix);
    }

    [Fact]
    public void BuildVolatileSuffix_OmitsVocalNoteEmphasisLine_WhenNoNotesAreProvided()
    {
        var section = BuildSection(Guid.NewGuid());
        var barGrid = BuildBarGrid(beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var window = barGrid.WindowForBarRange(new BarIndex(0), new BarIndex(2))!.Value;
        var context = BuildContext(barGrid, window);
        var layout = new LayoutSummary(1, 100, [], []);

        var suffix = SectionScriptPromptBuilder.BuildVolatileSuffix(section, context, layout);

        Assert.DoesNotContain("Vocal note emphasis", suffix);
    }
}
