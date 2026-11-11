using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 20 §2a/§2b: hermetic unit tests for <see cref="MultiLayerStackMiner"/> -- the real,
/// auto-extracted (never hand-curated) multi-layer stack mining this phase adds. Same hand-built
/// <see cref="CorpusSongAnalysis"/> fixture convention as <see cref="CraftRuleInducerCorpusSignalTests"/>.
/// </summary>
public sealed class MultiLayerStackMinerTests
{
    private const long TicksPerBar = 20_000_000L;
    private const int BarCount = 8;

    [Fact]
    public void RealOverlappingStack_Promoted_WhenSeenInEnoughDistinctSongs()
    {
        var effects = new[]
        {
            Effect("Matrix1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Matrix1", "Twinkle", layerIndex: 1, start: 0, end: 10_000),
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var catalog = MultiLayerStackMiner.Build(songs, minSongCount: 5);

        var template = Assert.Single(catalog.Templates);
        Assert.Equal("high", template.EnergyBand);
        Assert.Equal(2, template.Layers.Count);
        Assert.Equal("On", template.Layers[0].Effect);
        Assert.Equal("subject", template.Layers[0].Role);
        Assert.Equal("Twinkle", template.Layers[1].Effect);
        Assert.Equal("secondary", template.Layers[1].Role);
        Assert.Contains("6 distinct real", template.SourceNote);
        Assert.Contains("Song 0", template.SourceNote);
    }

    [Fact]
    public void RealOverlappingStack_NeverPromoted_WhenBelowTheMinSongCountThreshold()
    {
        var effects = new[]
        {
            Effect("Matrix1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Matrix1", "Twinkle", layerIndex: 1, start: 0, end: 10_000),
        };

        // Only 3 distinct songs -- below the default 5-song "never forced" threshold.
        var songs = Enumerable.Range(0, 3).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var catalog = MultiLayerStackMiner.Build(songs, minSongCount: 5);

        Assert.Empty(catalog.Templates);
    }

    [Fact]
    public void NonOverlappingPlacements_NeverMined_EvenAcrossManySongs()
    {
        // Sequential, never concurrent -- never a real multi-layer stack.
        var effects = new[]
        {
            Effect("Matrix1", "On", layerIndex: 0, start: 0, end: 5_000),
            Effect("Matrix1", "Twinkle", layerIndex: 1, start: 5_000, end: 10_000),
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var catalog = MultiLayerStackMiner.Build(songs, minSongCount: 5);

        Assert.Empty(catalog.Templates);
    }

    [Fact]
    public void NonTreeMatrixModel_NeverMined_EvenWhenGenuinelyOverlapping()
    {
        var effects = new[]
        {
            Effect("Outline1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Outline1", "Twinkle", layerIndex: 1, start: 0, end: 10_000),
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var catalog = MultiLayerStackMiner.Build(songs, minSongCount: 5);

        Assert.Empty(catalog.Templates);
    }

    [Fact]
    public void LowEnergySection_ClassifiedAsLowTier()
    {
        var effects = new[]
        {
            Effect("Tree1", "Twinkle", layerIndex: 0, start: 0, end: 10_000),
            Effect("Tree1", "Shockwave", layerIndex: 1, start: 0, end: 10_000),
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Intro)).ToList();
        var catalog = MultiLayerStackMiner.Build(songs, minSongCount: 5);

        var template = Assert.Single(catalog.Templates);
        Assert.Equal("low", template.EnergyBand);
    }

    private static ParsedEffectInstance Effect(string modelName, string effectName, int layerIndex, long start, long end) => new(
        modelName, "model", effectName, StartTimeMs: start, EndTimeMs: end,
        LayerIndex: layerIndex, PaletteIndex: 0, SettingsString: "", PaletteString: "");

    private static CorpusSongAnalysis BuildSong(string title, IReadOnlyList<ParsedEffectInstance> effects, SectionKind sectionKind)
    {
        var entry = new CorpusEntry($@"C:\fake\{title}.xsq", $@"C:\fake\{title}.mp3", title, CorpusProvenanceTag.UserAuthored, [], "fixture");

        var bars = Enumerable.Range(0, BarCount)
            .Select(i => new BarFingerprint(
                BarIndex: i, Window: new TimeWindow(i * TicksPerBar, (i + 1) * TicksPerBar),
                Features: [i / (double)(BarCount - 1), 0.0], Confidence: 1.0, IsSilent: false))
            .ToList();
        var fingerprintSet = new SongFingerprintSet(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), "fixture/1.0",
            new BarFingerprintSettings(12, "fixture-recipe"), bars);

        var beats = Enumerable.Range(0, BarCount * 4)
            .Select(i => new BeatMarker(Guid.NewGuid(), i * (TicksPerBar / 4), i % 4, i / 4, 1.0, false))
            .ToArray();
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", Guid.NewGuid(), Guid.NewGuid(), "fixture/1.0",
            new BeatGridSettings(4, 1000), new TempoHypothesis(120, 1.0, "4/4"), beats, [new TempoHypothesis(120, 1.0, "4/4")], 0);

        var label = new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 0, BarCount, sectionKind, 1.0, ["fixture"], false);
        var labels = new SectionLabelAnalysis(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", Guid.NewGuid(), fingerprintSet.VersionId, "fixture/1.0",
            new SectionLabelSettings(0.85, 0.5, "fixture-recipe"), [label]);

        var phrase = new PhraseCandidate(
            Guid.NewGuid(), label.Id, 0, BarCount, 0, BarCount * TicksPerBar, BarCount,
            PhraseBoundarySource.BarHypothesis, 1.0, ["fixture"], "sig", false);
        var phrases = new PhraseSegmentationAnalysis(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", labels.VersionId, fingerprintSet.VersionId, null, "fixture/1.0",
            new PhraseSegmentationSettings(8, 1, 2_000_000, "fixture-recipe"), [phrase]);

        var aligned = EffectAligner.Align(effects, beatGrid, fingerprintSet, labels, phrases);

        return new CorpusSongAnalysis(entry, beatGrid, fingerprintSet, labels, phrases, aligned);
    }
}
