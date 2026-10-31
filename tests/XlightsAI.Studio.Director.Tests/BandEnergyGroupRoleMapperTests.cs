using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 10 §3d: real <see cref="BandEnergyEnvelope"/> data (wired through by §3a) should
/// resolve into (1) a per-section dominant-band profile and (2) a classification of the real imported
/// layout's own groups/models into §2b's foundation/subject/accent roles -- never a generic abstraction,
/// and never silently wrong when a song has no band-energy data at all.
/// </summary>
public sealed class BandEnergyGroupRoleMapperTests
{
    private static BeatGrid BuildBeatGrid(SongId songId, double bpm, int beatCount, long beatSpacingTicks, int beatsPerBar)
    {
        var beats = Enumerable.Range(0, beatCount)
            .Select(i => new BeatMarker(Guid.NewGuid(), i * beatSpacingTicks, i % beatsPerBar, i / beatsPerBar, 0.9, false))
            .ToArray();
        return new BeatGrid(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new BeatGridSettings(beatsPerBar, 1000), new TempoHypothesis(bpm, 0.9, "4/4"), beats, [], 0);
    }

    private static SectionLabelAnalysis BuildSections(SongId songId, params (int Start, int EndExclusive, SectionKind Kind)[] sections) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
        new SectionLabelSettings(0.95, 0.4, "t/1"),
        sections.Select(s => new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), s.Start, s.EndExclusive, s.Kind, 1.0, ["test"], false)).ToList());

    private static PhraseSegmentationAnalysis BuildPhrases(SongId songId) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
        new PhraseSegmentationSettings(4, 1, 0, "t/1"), []);

    private static BandEnergyEnvelope BuildBandEnergy(SongId songId, int sampleRate, params (long StartSample, long EndSample, float Bass, float Mid, float High)[] frames) => new(
        Guid.NewGuid(), songId, "hash", "t/1", "t/1",
        new BandEnergySettings(new RhythmSettings(sampleRate, 2048, 512), 150, 4000),
        frames.Select(f => new BandEnergyFrame(f.StartSample, f.EndSample, f.Bass, f.Mid, f.High)).ToArray());

    [Fact]
    public void ComputeSectionProfiles_ReportsRealMeasuredShares_NeverAWholeSongAverage()
    {
        var songId = new SongId(Guid.NewGuid());
        // 4/4, 1 second per beat (10_000_000 ticks), 4 beats per bar -> bar 0 = [0s,4s), bar 1 = [4s,8s).
        var beatGrid = BuildBeatGrid(songId, bpm: 60, beatCount: 8, beatSpacingTicks: 10_000_000, beatsPerBar: 4);
        var sections = BuildSections(songId, (0, 1, SectionKind.Verse), (1, 2, SectionKind.Chorus));
        var phrases = BuildPhrases(songId);

        const int sampleRate = 44100;
        // Bar 0 window [0s,4s) is pure bass; bar 1 window [4s,8s) is pure highs.
        var bandEnergy = BuildBandEnergy(songId, sampleRate,
            (0, 4L * sampleRate, 1.0f, 0.0f, 0.0f),
            (4L * sampleRate, 8L * sampleRate, 0.0f, 0.0f, 1.0f));

        var music = new MusicModel(songId, 8 * 10_000_000, beatGrid, sections, phrases, bandEnergy, null);

        var profiles = BandEnergyGroupRoleMapper.ComputeSectionProfiles(music);

        Assert.Equal(2, profiles.Count);
        Assert.Equal(MusicalRole.Bass, profiles[0].DominantBand);
        Assert.Equal(1.0, profiles[0].BassShare, precision: 3);
        Assert.Equal(MusicalRole.Highs, profiles[1].DominantBand);
        Assert.Equal(1.0, profiles[1].HighShare, precision: 3);
    }

    [Fact]
    public void ComputeSectionProfiles_ReturnsZeroShares_WhenNoBandEnergyAvailable()
    {
        var songId = new SongId(Guid.NewGuid());
        var beatGrid = BuildBeatGrid(songId, bpm: 120, beatCount: 8, beatSpacingTicks: 5_000_000, beatsPerBar: 4);
        var sections = BuildSections(songId, (0, 1, SectionKind.Verse));
        var music = new MusicModel(songId, 4_000_000_0, beatGrid, sections, BuildPhrases(songId), BandEnergy: null, VocalActivity: null);

        var profiles = BandEnergyGroupRoleMapper.ComputeSectionProfiles(music);

        var only = Assert.Single(profiles);
        Assert.Null(only.DominantBand);
        Assert.Equal(0, only.BassShare);
        Assert.Equal(0, only.MidShare);
        Assert.Equal(0, only.HighShare);
    }

    [Fact]
    public void Classify_NamesRealFoundationSubjectAccentGroups_FromTheImportedLayout()
    {
        var layout = new LayoutSummary(
            ModelCount: 3,
            TotalPixels: 1000,
            Groups:
            [
                new LayoutSummaryEntry("group:Outline", 400, "high-resolution"),
                new LayoutSummaryEntry("group:Gutter", 200, "low-resolution"),
                new LayoutSummaryEntry("group:Matrixes", 300, "high-resolution"),
                new LayoutSummaryEntry("group:MiniTrees", 100, "low-resolution"),
                new LayoutSummaryEntry("group:Everything", 1000, "high-resolution"),
            ],
            TopPixelHeavyModels: [new LayoutSummaryEntry("Tree", 900, "high-resolution")]);

        var classified = BandEnergyGroupRoleMapper.Classify(layout);

        Assert.Contains(classified, c => c.Reference == "group:Outline" && c.Role == BandRole.Foundation);
        Assert.Contains(classified, c => c.Reference == "group:Gutter" && c.Role == BandRole.Foundation);
        Assert.Contains(classified, c => c.Reference == "group:Matrixes" && c.Role == BandRole.Subject);
        Assert.Contains(classified, c => c.Reference == "Tree" && c.Role == BandRole.Subject);
        Assert.Contains(classified, c => c.Reference == "group:MiniTrees" && c.Role == BandRole.Accent);
        Assert.DoesNotContain(classified, c => c.Reference == "group:Everything");
    }

    [Fact]
    public void ComputeSongWideShare_WeightsBySectionBarCount()
    {
        var profiles = new[]
        {
            new SectionBandShare(SectionKind.Verse, 0, 1, BassShare: 1.0, MidShare: 0.0, HighShare: 0.0),
            new SectionBandShare(SectionKind.Chorus, 1, 4, BassShare: 0.0, MidShare: 0.0, HighShare: 1.0),
        };

        var (bass, mid, high) = BandEnergyGroupRoleMapper.ComputeSongWideShare(profiles);

        // 1 bar of pure bass, 3 bars of pure highs -> bass should be the minority share.
        Assert.Equal(0.25, bass, precision: 3);
        Assert.Equal(0.0, mid, precision: 3);
        Assert.Equal(0.75, high, precision: 3);
    }
}
