using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 10 §3c: real <see cref="MotifClusteringAnalysis.Groups"/> data (computed by
/// <c>XlightsAI.Studio.Structure</c>, previously forwarded only to the Shell's Structure Inspector and
/// never reaching either prompt) should join against each section's own phrases into a specific
/// motif-linked predecessor section -- never a placeholder, and never wrong when a song has no motif data
/// at all.
/// </summary>
public sealed class MotifLinkedPredecessorBuilderTests
{
    private static BeatGrid BuildBeatGrid(SongId songId, int beatCount, long beatSpacingTicks, int beatsPerBar) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(60, 0.9, "4/4"),
        Enumerable.Range(0, beatCount).Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false)).ToArray(),
        [], 0);

    private static SectionLabelAnalysis BuildSections(SongId songId, params (Guid Id, int Start, int EndExclusive, SectionKind Kind)[] sections) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new SectionLabelSettings(0.95, 0.4, "t/1"),
        sections.Select(s => new SectionLabelCandidate(s.Id, Guid.NewGuid(), s.Start, s.EndExclusive, s.Kind, 1.0, ["test"], false)).ToList());

    private static PhraseCandidate Phrase(Guid sectionId, int start, int endExclusive) => new(
        Guid.NewGuid(), sectionId, start, endExclusive, start * 10_000_000L, endExclusive * 10_000_000L,
        endExclusive - start, PhraseBoundarySource.BarHypothesis, 1.0, ["test"], "sig", false);

    private static PhraseSegmentationAnalysis BuildPhrases(SongId songId, params PhraseCandidate[] phrases) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
        new PhraseSegmentationSettings(4, 1, 0, "t/1"), phrases);

    private static MotifClusteringAnalysis BuildMotifs(SongId songId, params MotifGroupCandidate[] groups) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new MotifClusteringSettings(8, 0.85, "t/1"), groups, []);

    private static MotifGroupCandidate Group(double confidence, params Guid[] phraseIds) =>
        new(Guid.NewGuid(), phraseIds, phraseIds[0], confidence, ["test"]);

    private static MusicModel BuildMusicModel(BeatGrid beatGrid, SectionLabelAnalysis sections, PhraseSegmentationAnalysis phrases, MotifClusteringAnalysis? motifs) =>
        new(beatGrid.SongId, beatGrid.Beats.Count() * 10_000_000L, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: null, Motifs: motifs);

    [Fact]
    public void ComputeSectionLinks_LinksARepeatToItsOriginalSection_AcrossDifferentKinds()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, beatCount: 32, beatSpacingTicks: 10_000_000, beatsPerBar: 4);

        var chorusId = Guid.NewGuid();
        var bridgeId = Guid.NewGuid();
        var sections = BuildSections(songId,
            (chorusId, 0, 4, SectionKind.Chorus),
            (bridgeId, 16, 20, SectionKind.Bridge));

        var chorusPhrase = Phrase(chorusId, 0, 4);
        var bridgePhrase = Phrase(bridgeId, 16, 20);
        var phrases = BuildPhrases(songId, chorusPhrase, bridgePhrase);

        var motifs = BuildMotifs(songId, Group(0.9, chorusPhrase.Id, bridgePhrase.Id));
        var music = BuildMusicModel(beatGrid, sections, phrases, motifs);

        var link = Assert.Single(MotifLinkedPredecessorBuilder.ComputeSectionLinks(music));

        Assert.Equal(bridgeId, link.SectionId);
        Assert.Equal(chorusId, link.LinkedPredecessorSectionId);
        Assert.Equal(SectionKind.Chorus, link.LinkedPredecessorKind);
        Assert.Equal(0, link.LinkedPredecessorStartBarIndex);
        Assert.Equal(4, link.LinkedPredecessorEndBarIndexExclusive);
        Assert.Equal(0.9, link.Confidence);
    }

    [Fact]
    public void ComputeSectionLinks_OmitsTheOriginalOccurrence_SinceItHasNoEarlierPredecessor()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, beatCount: 32, beatSpacingTicks: 10_000_000, beatsPerBar: 4);

        var chorus1Id = Guid.NewGuid();
        var chorus2Id = Guid.NewGuid();
        var sections = BuildSections(songId,
            (chorus1Id, 0, 4, SectionKind.Chorus),
            (chorus2Id, 16, 20, SectionKind.Chorus));

        var phrase1 = Phrase(chorus1Id, 0, 4);
        var phrase2 = Phrase(chorus2Id, 16, 20);
        var phrases = BuildPhrases(songId, phrase1, phrase2);

        var motifs = BuildMotifs(songId, Group(0.9, phrase1.Id, phrase2.Id));
        var music = BuildMusicModel(beatGrid, sections, phrases, motifs);

        var link = Assert.Single(MotifLinkedPredecessorBuilder.ComputeSectionLinks(music));

        // Only the repeat gets a link -- the original (chorus1) section has nothing earlier to point to.
        Assert.Equal(chorus2Id, link.SectionId);
    }

    [Fact]
    public void ComputeSectionLinks_PicksTheEarliestMember_WhenASectionHasMultiplePhrasesInDifferentGroups()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, beatCount: 32, beatSpacingTicks: 10_000_000, beatsPerBar: 4);

        var verse1Id = Guid.NewGuid();
        var verse2Id = Guid.NewGuid();
        var laterId = Guid.NewGuid();
        var sections = BuildSections(songId,
            (verse1Id, 0, 2, SectionKind.Verse),
            (verse2Id, 4, 6, SectionKind.Verse),
            (laterId, 16, 20, SectionKind.Bridge));

        var verse1Phrase = Phrase(verse1Id, 0, 2);
        var verse2Phrase = Phrase(verse2Id, 4, 6);
        var laterPhraseA = Phrase(laterId, 16, 18);
        var laterPhraseB = Phrase(laterId, 18, 20);
        var phrases = BuildPhrases(songId, verse1Phrase, verse2Phrase, laterPhraseA, laterPhraseB);

        // laterPhraseB links back to verse2 (bars 4-6), laterPhraseA links back to verse1 (bars 0-2, earlier).
        var motifs = BuildMotifs(songId,
            Group(0.8, verse2Phrase.Id, laterPhraseB.Id),
            Group(0.95, verse1Phrase.Id, laterPhraseA.Id));
        var music = BuildMusicModel(beatGrid, sections, phrases, motifs);

        var link = Assert.Single(MotifLinkedPredecessorBuilder.ComputeSectionLinks(music));

        Assert.Equal(laterId, link.SectionId);
        Assert.Equal(verse1Id, link.LinkedPredecessorSectionId);
    }

    [Fact]
    public void ComputeSectionLinks_ReturnsEmpty_WhenNoMotifsAreAvailable()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        var sections = BuildSections(songId, (sectionId, 0, 2, SectionKind.Verse));
        var phrases = BuildPhrases(songId, Phrase(sectionId, 0, 2));

        var music = BuildMusicModel(beatGrid, sections, phrases, motifs: null);

        Assert.Empty(MotifLinkedPredecessorBuilder.ComputeSectionLinks(music));
    }

    [Fact]
    public void ComputeSectionLinks_ReturnsEmpty_WhenNoSectionSharesAMotifGroup()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sectionId = Guid.NewGuid();
        var sections = BuildSections(songId, (sectionId, 0, 2, SectionKind.Verse));
        var phrases = BuildPhrases(songId, Phrase(sectionId, 0, 2));
        var motifs = BuildMotifs(songId); // no groups at all -- nothing clustered

        var music = BuildMusicModel(beatGrid, sections, phrases, motifs);

        Assert.Empty(MotifLinkedPredecessorBuilder.ComputeSectionLinks(music));
    }
}
