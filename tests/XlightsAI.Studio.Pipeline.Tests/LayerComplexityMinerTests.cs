using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 25 Part C §3 bullet 3: hermetic unit tests for <see cref="LayerComplexityMiner"/>,
/// same hand-built <see cref="CorpusSongAnalysis"/> fixture convention as <see cref="MultiLayerStackMinerTests"/>/
/// <see cref="WholeCanvasCoOccurrenceMinerTests"/>.
/// </summary>
public sealed class LayerComplexityMinerTests
{
    private const long TicksPerBar = 20_000_000L;
    private const int BarCount = 8;

    [Fact]
    public void ThreeLayerStack_Mined_RegardlessOfModelNameConvention()
    {
        // "Outline1" is not a tree/matrix name -- unlike MultiLayerStackMiner's own catalog, training-only
        // layer-complexity mining is deliberately never restricted to tree/matrix models ("any model/group").
        var effects = new[]
        {
            Effect("Outline1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Outline1", "Twinkle", layerIndex: 1, start: 0, end: 10_000),
            Effect("Outline1", "Shockwave", layerIndex: 2, start: 0, end: 10_000),
        };
        var song = BuildSong(effects, SectionKind.Chorus);

        var samples = LayerComplexityMiner.Mine(song);

        var sample = Assert.Single(samples);
        Assert.Equal(3, sample.PeakConcurrentLayerCount);
        Assert.Equal(SectionKind.Chorus, sample.SectionKind);
        Assert.NotNull(sample.DurationBars);
    }

    [Fact]
    public void TwoLayerStack_NeverMined_BelowTheThreeLayerFloor()
    {
        var effects = new[]
        {
            Effect("Model1", "On", layerIndex: 0, start: 0, end: 10_000),
            Effect("Model1", "Twinkle", layerIndex: 1, start: 0, end: 10_000),
        };
        var song = BuildSong(effects, SectionKind.Chorus);

        Assert.Empty(LayerComplexityMiner.Mine(song));
    }

    [Fact]
    public void FiveLayerStack_CountsThePeakAccurately()
    {
        var effects = Enumerable.Range(0, 5)
            .Select(i => Effect("Model1", $"Effect{i}", layerIndex: i, start: 0, end: 10_000))
            .ToArray();
        var song = BuildSong(effects, SectionKind.Chorus);

        var sample = Assert.Single(LayerComplexityMiner.Mine(song));
        Assert.Equal(5, sample.PeakConcurrentLayerCount);
    }

    private static ParsedEffectInstance Effect(string modelName, string effectName, int layerIndex, long start, long end) => new(
        modelName, "model", effectName, StartTimeMs: start, EndTimeMs: end,
        LayerIndex: layerIndex, PaletteIndex: 0, SettingsString: "", PaletteString: "");

    private static CorpusSongAnalysis BuildSong(IReadOnlyList<ParsedEffectInstance> effects, SectionKind sectionKind)
    {
        var entry = new CorpusEntry(@"C:\fake\Song.xsq", @"C:\fake\Song.mp3", "Song", CorpusProvenanceTag.ExternalReference, [], "fixture");

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
