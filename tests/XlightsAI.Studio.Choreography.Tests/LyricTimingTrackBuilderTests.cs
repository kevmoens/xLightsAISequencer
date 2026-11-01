using XlightsAI.Studio.Choreography;
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
        // Each half-word span (150,000 ticks = 15ms) combines with its partner into a 300,000-tick
        // (30ms) word -- comfortably above the 25ms xLights frame grid minimum, so neither word gets
        // merged into the other by the minimum-duration safety net (see
        // Words_RealisticShortTrailingWord_IsAbsorbedRatherThanExportFailing below for that case).
        var marks = new List<VisemeMark>
        {
            new("AI", 0, 150_000),
            new("E", 150_000, 300_000),
            new("MBP", 300_000, 310_000), // valley -- closes the first word.
            new("O", 310_000, 460_000),
            new("U", 460_000, 610_000),
            new(VisemeApproximator.RestCode, 610_000, 620_000), // valley -- closes the second word.
        };

        var track = LyricTimingTrackBuilder.Words(marks);

        Assert.Equal(2, track.Marks.Count);
        Assert.Equal((0L, 300_000L), (track.Marks[0].StartTicks, track.Marks[0].EndTicks));
        Assert.Equal((310_000L, 610_000L), (track.Marks[1].StartTicks, track.Marks[1].EndTicks));
    }

    [Fact]
    public void Phonemes_OneMarkPerVisemeWindow_LabelledWithItsOwnCode()
    {
        // Each window (300,000 ticks = 30ms) is already above the 25ms frame-grid minimum on its own,
        // so the minimum-duration safety net never needs to merge them.
        var marks = new List<VisemeMark> { new("AI", 0, 300_000), new("E", 300_000, 600_000) };

        var track = LyricTimingTrackBuilder.Phonemes(marks);

        Assert.Equal(LyricTimingTrackBuilder.PhonemesTrackName, track.Name);
        Assert.Equal(2, track.Marks.Count);
        Assert.Equal("AI", track.Marks[0].Label);
        Assert.Equal("E", track.Marks[1].Label);
    }

    [Fact]
    public void Phonemes_RealisticShortTrailingPhoneme_IsAbsorbedRatherThanExportFailing()
    {
        // Regression test for a real-song export failure: "Timing track 'Lyrics_Phonemes' mark
        // (489986395-490000000 ticks) rounds to a non-positive duration at the 25ms xLights frame
        // grid; no file was written." A normal-length phoneme followed by a genuinely short trailing
        // one (13,605 ticks here, matching the real failure's own magnitude) must not surface as its
        // own unexportable mark.
        var marks = new List<VisemeMark> { new("AI", 0, 300_000), new("E", 300_000, 313_605) };

        var track = LyricTimingTrackBuilder.Phonemes(marks);

        Assert.All(track.Marks, mark => Assert.True(mark.EndTicks - mark.StartTicks >= FrameClock.FrameIntervalTicks));
        Assert.Equal(0, track.Marks[0].StartTicks);
        Assert.Equal(313_605, track.Marks[^1].EndTicks);
    }

    [Fact]
    public void Words_RealisticShortTrailingWord_IsAbsorbedRatherThanExportFailing()
    {
        // Same failure shape as the Phonemes regression above, but for a short final WORD run (not an
        // individual phoneme) -- a one-phoneme trailing word can itself be shorter than the frame grid.
        var marks = new List<VisemeMark>
        {
            new("AI", 0, 150_000),
            new("E", 150_000, 300_000),
            new("MBP", 300_000, 310_000), // valley -- closes the first word.
            new("O", 310_000, 323_605), // short trailing word -- only 13,605 ticks wide.
        };

        var track = LyricTimingTrackBuilder.Words(marks);

        Assert.All(track.Marks, mark => Assert.True(mark.EndTicks - mark.StartTicks >= FrameClock.FrameIntervalTicks));
        Assert.Equal(0, track.Marks[0].StartTicks);
        Assert.Equal(323_605, track.Marks[^1].EndTicks);
    }
}
