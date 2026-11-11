using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// User-requested real investigation (2026-11-08): real, isolated-vocal-stem, YIN-pitch-tracked note
/// onsets (<see cref="MelodyTrack"/>) should join against each section's own bar range into a compact,
/// CAPPED <see cref="SectionVocalNoteEmphasis"/> (the loudest few by real measured
/// <see cref="NoteEvent.Velocity"/>) -- never the raw, hundreds-of-notes-per-song note list, and never a
/// fabricated entry when a song/section has no melody data at all.
/// </summary>
public sealed class VocalNoteEmphasisBriefBuilderTests
{
    private static BeatGrid BuildBeatGrid(SongId songId, double bpm, int beatCount, long beatSpacingTicks, int beatsPerBar) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(bpm, 0.9, "4/4"),
        Enumerable.Range(0, beatCount).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false)).ToArray(),
        [], 0);

    private static SectionLabelAnalysis BuildSections(SongId songId, params (Guid Id, int Start, int EndExclusive, SectionKind Kind)[] sections) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new SectionLabelSettings(0.95, 0.4, "t/1"),
        sections.Select(s => new SectionLabelCandidate(s.Id, Guid.NewGuid(), s.Start, s.EndExclusive, s.Kind, 1.0, ["test"], false)).ToList());

    private static PhraseSegmentationAnalysis BuildPhrases(SongId songId) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
        new PhraseSegmentationSettings(4, 1, 0, "t/1"), []);

    private static NoteEvent Note(long startTicks, long endTicks, double velocity) =>
        new(Guid.NewGuid(), startTicks, endTicks, MidiNote: 60, velocity, Confidence: 1.0, UserCorrected: false);

    private static MusicModel BuildMusicModel(BeatGrid beatGrid, SectionLabelAnalysis sections, MelodyTrack? melody, long durationTicks) =>
        new(beatGrid.SongId, durationTicks, beatGrid, sections, BuildPhrases(beatGrid.SongId), BandEnergy: null, VocalActivity: null, Melody: melody);

    [Fact]
    public void ComputeSectionBriefs_SelectsOnlyTheLoudestNotes_CappedAtMaxNotesPerSection()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 40, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        var sections = BuildSections(songId, (sectionId, 0, 10, SectionKind.Chorus));

        // 20 notes, one per beat, with velocity equal to beat index / 20.0 -- the loudest 8 are the LAST 8 (beats 12-19).
        var notes = Enumerable.Range(0, 20)
            .Select(i => Note(i * 10_000_000L, (i + 1) * 10_000_000L, velocity: i / 20.0))
            .ToArray();
        var melody = new MelodyTrack(Guid.NewGuid(), "hash", notes, []);
        var music = BuildMusicModel(beatGrid, sections, melody, 40 * 10_000_000);

        var brief = Assert.Single(VocalNoteEmphasisBriefBuilder.ComputeSectionBriefs(music));

        Assert.Equal(sectionId, brief.SectionId);
        Assert.Equal(VocalNoteEmphasisBriefBuilder.MaxNotesPerSection, brief.SalientNotes.Count);
        // Re-ordered back into time order for display -- never emitted loudest-first.
        for (var i = 1; i < brief.SalientNotes.Count; i++)
        {
            Assert.True(brief.SalientNotes[i].StartTicks > brief.SalientNotes[i - 1].StartTicks);
        }
        // The quietest (earliest, lowest-velocity) notes must have been excluded.
        Assert.DoesNotContain(brief.SalientNotes, n => n.StartTicks == 0);
    }

    [Fact]
    public void ComputeSectionBriefs_OmitsTheSection_WhenNoMelodyIsAvailable()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sections = BuildSections(songId, (Guid.NewGuid(), 0, 2, SectionKind.Verse));
        var music = BuildMusicModel(beatGrid, sections, melody: null, 8 * 10_000_000);

        var briefs = VocalNoteEmphasisBriefBuilder.ComputeSectionBriefs(music);

        Assert.Empty(briefs);
    }

    [Fact]
    public void ComputeSectionBriefs_OmitsTheSection_WhenItHasNoSoundingNotes()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 16, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        // Section spans bar 1 ([4s,8s)); the only real note is in bar 0 ([0s,4s)) instead.
        var sections = BuildSections(songId, (sectionId, 1, 2, SectionKind.Verse));
        var melody = new MelodyTrack(Guid.NewGuid(), "hash", [Note(0, 9_000_000, velocity: 0.9)], []);

        var music = BuildMusicModel(beatGrid, sections, melody, 16 * 10_000_000);

        var briefs = VocalNoteEmphasisBriefBuilder.ComputeSectionBriefs(music);

        Assert.Empty(briefs);
    }
}
