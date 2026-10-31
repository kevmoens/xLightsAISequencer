using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 8 task 3: the three-level lyric timing track builder, exercised directly against hand-constructed viseme marks (no audio decoding involved).</summary>
public sealed class LyricTimingTrackBuilderTests
{
    private static PhraseSegmentationAnalysis EmptyPhrases(SongId songId) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
        new PhraseSegmentationSettings(4, 1, 0, "t/1"), []);

    [Fact]
    public void BuildAll_NoVocalActivity_ProducesNoLyricTracks()
    {
        var tracks = LyricTimingTrackBuilder.BuildAll(EmptyPhrases(new SongId(Guid.NewGuid())), vocalActivity: null, visemeMarks: null);

        Assert.Empty(tracks);
    }

    [Fact]
    public void BuildAll_VocalActivityWithNoVisemeMarks_StillProducesPhrasesAndEmptyPhonemes()
    {
        var vocalActivity = new VocalActivityEnvelope(
            Guid.NewGuid(), Guid.NewGuid(), "hash", "test", new StemActivityDetectionSettings(1000, 500, 0.1, 0.05, 1_000_000),
            [new StemActivity(StemKind.Vocals, 0, 10_000_000, 1.0)]);

        var tracks = LyricTimingTrackBuilder.BuildAll(EmptyPhrases(new SongId(Guid.NewGuid())), vocalActivity, visemeMarks: null);

        Assert.True(tracks.ContainsKey(LyricTimingTrackBuilder.PhrasesTrackName));
        Assert.True(tracks.ContainsKey(LyricTimingTrackBuilder.PhonemesTrackName));
        Assert.Empty(tracks[LyricTimingTrackBuilder.PhonemesTrackName].Marks);
        Assert.False(tracks.ContainsKey(LyricTimingTrackBuilder.WordsTrackName)); // no non-valley run exists with no viseme marks at all.
    }

    [Fact]
    public void Words_SplitsAtMbpAndRestValleys_IntoSeparateWordRuns()
    {
        var marks = new List<VisemeMark>
        {
            new("AI", 0, 100),
            new("E", 100, 200),
            new("MBP", 200, 300), // valley -- closes the first word.
            new("O", 300, 400),
            new("U", 400, 500),
            new(VisemeApproximator.RestCode, 500, 600), // valley -- closes the second word.
        };

        var track = LyricTimingTrackBuilder.Words(marks);

        Assert.Equal(2, track.Marks.Count);
        Assert.Equal((0L, 200L), (track.Marks[0].StartTicks, track.Marks[0].EndTicks));
        Assert.Equal((300L, 500L), (track.Marks[1].StartTicks, track.Marks[1].EndTicks));
    }

    [Fact]
    public void Phonemes_OneMarkPerVisemeWindow_LabelledWithItsOwnCode()
    {
        var marks = new List<VisemeMark> { new("AI", 0, 100), new("E", 100, 200) };

        var track = LyricTimingTrackBuilder.Phonemes(marks);

        Assert.Equal(LyricTimingTrackBuilder.PhonemesTrackName, track.Name);
        Assert.Equal(2, track.Marks.Count);
        Assert.Equal("AI", track.Marks[0].Label);
        Assert.Equal("E", track.Marks[1].Label);
    }
}
