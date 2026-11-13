using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 25 Part B.4: hermetic unit tests for <see cref="WholeCanvasCoOccurrenceMiner"/>,
/// same hand-built <see cref="CorpusSongAnalysis"/> fixture convention as <see cref="MultiLayerStackMinerTests"/>.
/// </summary>
public sealed class WholeCanvasCoOccurrenceMinerTests
{
    private const long TicksPerBar = 20_000_000L;
    private const int BarCount = 8;

    [Fact]
    public void NoWholeCanvasGroup_ReturnsNoSamples()
    {
        var effects = new[] { Effect("Everything", "group", "On", start: 0, end: 10_000) };
        var song = BuildSong(effects, SectionKind.Chorus);

        // "Everything" covers only half the layout here -- below the 0.90 threshold, so it never
        // classifies as whole-canvas and this song contributes nothing.
        var layout = BuildLayout(everythingCoversHalfTheLayout: true);

        var samples = WholeCanvasCoOccurrenceMiner.Mine(song, layout);

        Assert.Empty(samples);
    }

    [Fact]
    public void WholeCanvasWindow_RecordsCoFiringSubjectButNotFoundationOrAccent()
    {
        var effects = new[]
        {
            Effect("Everything", "group", "On", start: 0, end: 10_000),
            Effect("MegaTree-Center", "model", "Twinkle", start: 2_000, end: 8_000), // Subject (keyword "tree")
        };
        var song = BuildSong(effects, SectionKind.Chorus);
        var layout = BuildLayout(everythingCoversHalfTheLayout: false); // "Everything" covers 100% here.

        var samples = WholeCanvasCoOccurrenceMiner.Mine(song, layout);

        var sample = Assert.Single(samples);
        Assert.Equal(SectionKind.Chorus, sample.SectionKind);
        Assert.True(sample.CoFiresWithSubject);
        Assert.False(sample.CoFiresWithFoundation);
        Assert.False(sample.CoFiresWithAccent);
    }

    [Fact]
    public void NonOverlappingSubjectEffect_NeverRecordedAsCoFiring()
    {
        var effects = new[]
        {
            Effect("Everything", "group", "On", start: 0, end: 5_000),
            Effect("MegaTree-Center", "model", "Twinkle", start: 6_000, end: 8_000), // starts after the whole-canvas window ends.
        };
        var song = BuildSong(effects, SectionKind.Chorus);
        var layout = BuildLayout(everythingCoversHalfTheLayout: false);

        var samples = WholeCanvasCoOccurrenceMiner.Mine(song, layout);

        var sample = Assert.Single(samples);
        Assert.False(sample.CoFiresWithSubject);
    }

    [Fact]
    public void WholeCanvasGroupEffect_WrittenAsRealXLightsModelType_StillMatches()
    {
        // Build-plan Phase 25 Part C real-corpus finding: confirmed across every real .xsq in the
        // C:\xLights 31-sequence training corpus, xLights NEVER writes type="group" -- a Model Group's own
        // directly-placed effect is written exactly like a real model's, type="model", same as every other
        // element. This miner must match whole-canvas membership by name alone, never by the (now known to
        // be unrealistic) "group" type string, or a real catch-all group's own effect is silently invisible
        // no matter how many songs place one.
        var effects = new[]
        {
            Effect("Everything", "model", "On", start: 0, end: 10_000), // real xLights never writes "group" here.
            Effect("MegaTree-Center", "model", "Twinkle", start: 2_000, end: 8_000),
        };
        var song = BuildSong(effects, SectionKind.Chorus);
        var layout = BuildLayout(everythingCoversHalfTheLayout: false); // "Everything" covers 100% here.

        var samples = WholeCanvasCoOccurrenceMiner.Mine(song, layout);

        var sample = Assert.Single(samples);
        Assert.True(sample.CoFiresWithSubject);
    }

    [Fact]
    public void NoAddressablePixels_ReturnsNoSamples()
    {
        var effects = new[] { Effect("Everything", "group", "On", start: 0, end: 10_000) };
        var song = BuildSong(effects, SectionKind.Chorus);
        var layout = new DisplayGraph("hash", [], [], []);

        var samples = WholeCanvasCoOccurrenceMiner.Mine(song, layout);

        Assert.Empty(samples);
    }

    private static ParsedEffectInstance Effect(string modelName, string modelType, string effectName, long start, long end) => new(
        modelName, modelType, effectName, StartTimeMs: start, EndTimeMs: end,
        LayerIndex: 0, PaletteIndex: 0, SettingsString: "", PaletteString: "");

    /// <summary>
    /// Two models of equal pixel weight ("MegaTree-Center" classifies Subject via the "tree" keyword,
    /// "Arch-01" is unclassified by name). "Everything" groups both -- 100% coverage (whole-canvas) by
    /// default, or just the tree (50%, below threshold) when <paramref name="everythingCoversHalfTheLayout"/>.
    /// </summary>
    private static DisplayGraph BuildLayout(bool everythingCoversHalfTheLayout)
    {
        var tree = new DisplayModel(new ModelId("MegaTree-Center"), "MegaTree-Center", "Tree 360", [], [], NodeCount: 800);
        var arch = new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", [], [], NodeCount: 800);

        var members = everythingCoversHalfTheLayout
            ? new[] { new DisplayGroupMember("MegaTree-Center", DisplayGroupMemberKind.Model) }
            : new[]
            {
                new DisplayGroupMember("MegaTree-Center", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch-01", DisplayGroupMemberKind.Model),
            };

        var everything = new DisplayModelGroup("Everything", members);

        return new DisplayGraph("hash", [tree, arch], [everything], []);
    }

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
