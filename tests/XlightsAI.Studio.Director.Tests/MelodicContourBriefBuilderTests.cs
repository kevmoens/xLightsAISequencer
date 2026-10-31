using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 10 §3b: real <see cref="MelodyTrack"/> data (pitch contour, computed by
/// <c>XlightsAI.Studio.Melody</c> but previously only consumed by the bar-fingerprint/section-detection
/// path and the Piano Roll UI) should join against each section's own bar range into a compact
/// <see cref="SectionMelodicBrief"/> -- never raw frame-by-frame pitch data, and never silently wrong when
/// a song has no melody data at all.
/// </summary>
public sealed class MelodicContourBriefBuilderTests
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

    private static NoteEvent Note(long startTicks, long endTicks, int midiNote) =>
        new(Guid.NewGuid(), startTicks, endTicks, midiNote, Velocity: 0.8, Confidence: 1.0, UserCorrected: false);

    /// <summary>Real <see cref="MelodicContourClassifier"/> output -- never hand-authored gestures, so this test exercises the exact same join the production path uses.</summary>
    private static MelodyTrack BuildMelody(string sourceHash, params NoteEvent[] notes) =>
        MelodicContourClassifier.Classify(new MelodyTrack(Guid.NewGuid(), sourceHash, notes, []), GestureClassificationSettings.Default);

    private static MusicModel BuildMusicModel(BeatGrid beatGrid, SectionLabelAnalysis sections, MelodyTrack? melody, long durationTicks) =>
        new(beatGrid.SongId, durationTicks, beatGrid, sections, BuildPhrases(beatGrid.SongId), BandEnergy: null, VocalActivity: null, Melody: melody);

    [Fact]
    public void ComputeSectionBriefs_ClassifiesRising_WhenNotesStepUpwardAcrossTheSection()
    {
        var songId = new SongId(Guid.NewGuid());
        // 1s/beat, 4 beats/bar -> bar 0 spans [0s,4s).
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        var sections = BuildSections(songId, (sectionId, 0, 2, SectionKind.Verse));

        // C4 -> D4 -> E4 -> G4, each a real step/leap upward; touching (zero-gap) notes so the gap never
        // reaches GestureClassificationSettings.Default.RestGapTicks and gets reclassified as a Rest.
        var melody = BuildMelody("hash",
            Note(0, 10_000_000, 60),
            Note(10_000_000, 20_000_000, 62),
            Note(20_000_000, 30_000_000, 64),
            Note(30_000_000, 40_000_000, 67));

        var music = BuildMusicModel(beatGrid, sections, melody, 8 * 10_000_000);

        var briefs = MelodicContourBriefBuilder.ComputeSectionBriefs(music);

        var brief = Assert.Single(briefs);
        Assert.Equal(sectionId, brief.SectionId);
        Assert.Equal(MelodicContourShape.Rising, brief.ContourShape);
        Assert.True(brief.NoteDensity > 0);
    }

    [Fact]
    public void ComputeSectionBriefs_ClassifiesFalling_WhenNotesStepDownwardAcrossTheSection()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        var sections = BuildSections(songId, (sectionId, 0, 2, SectionKind.Verse));

        // G4 -> E4 -> D4 -> C4, each a real step/leap downward; touching (zero-gap) notes, same reasoning as above.
        var melody = BuildMelody("hash",
            Note(0, 10_000_000, 67),
            Note(10_000_000, 20_000_000, 64),
            Note(20_000_000, 30_000_000, 62),
            Note(30_000_000, 40_000_000, 60));

        var music = BuildMusicModel(beatGrid, sections, melody, 8 * 10_000_000);

        var brief = Assert.Single(MelodicContourBriefBuilder.ComputeSectionBriefs(music));

        Assert.Equal(MelodicContourShape.Falling, brief.ContourShape);
    }

    [Fact]
    public void ComputeSectionBriefs_ReportsStaticSparse_ForALongHeldSingleNote()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        var sections = BuildSections(songId, (sectionId, 0, 2, SectionKind.Verse));

        // One long held note spanning the whole section -- no real pitch movement at all.
        var melody = BuildMelody("hash", Note(0, 39_000_000, 65));

        var music = BuildMusicModel(beatGrid, sections, melody, 8 * 10_000_000);

        var brief = Assert.Single(MelodicContourBriefBuilder.ComputeSectionBriefs(music));

        Assert.Equal(MelodicContourShape.StaticSparse, brief.ContourShape);
    }

    [Fact]
    public void ComputeSectionBriefs_ReportsPeakNoteBeatOffset_AtTheSection_sHighestPitchedNote()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        var sections = BuildSections(songId, (sectionId, 0, 2, SectionKind.Verse));

        // Notes land on beats 0,1,2,3 (ticks 0,10M,20M,30M); the peak (midi 72) lands on beat 2.
        var melody = BuildMelody("hash",
            Note(0, 10_000_000, 60),
            Note(10_000_000, 20_000_000, 64),
            Note(20_000_000, 30_000_000, 72),
            Note(30_000_000, 40_000_000, 67));

        var music = BuildMusicModel(beatGrid, sections, melody, 8 * 10_000_000);

        var brief = Assert.Single(MelodicContourBriefBuilder.ComputeSectionBriefs(music));

        Assert.Equal(2, brief.PeakNoteBeatOffset);
    }

    [Fact]
    public void ComputeSectionBriefs_ReturnsStaticSparseZeroed_WhenNoMelodyIsAvailable()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sections = BuildSections(songId, (Guid.NewGuid(), 0, 2, SectionKind.Verse));
        var music = BuildMusicModel(beatGrid, sections, melody: null, 8 * 10_000_000);

        var brief = Assert.Single(MelodicContourBriefBuilder.ComputeSectionBriefs(music));

        Assert.Equal(MelodicContourShape.StaticSparse, brief.ContourShape);
        Assert.Equal(0, brief.NoteDensity);
        Assert.Equal(0, brief.PeakNoteBeatOffset);
    }

    [Fact]
    public void ComputeSectionBriefs_ReturnsStaticSparseZeroed_WhenSectionHasNoSoundingNotes()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 16, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        // Section spans bar 1 ([4s,8s)); every real note is in bar 0 ([0s,4s)) instead.
        var sections = BuildSections(songId, (sectionId, 1, 2, SectionKind.Verse));
        var melody = BuildMelody("hash", Note(0, 9_000_000, 60), Note(10_000_000, 19_000_000, 64));

        var music = BuildMusicModel(beatGrid, sections, melody, 16 * 10_000_000);

        var brief = Assert.Single(MelodicContourBriefBuilder.ComputeSectionBriefs(music));

        Assert.Equal(MelodicContourShape.StaticSparse, brief.ContourShape);
        Assert.Equal(0, brief.NoteDensity);
    }
}
