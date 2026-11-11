using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Build-plan Phase 19 §2c: hermetic unit tests for the three new corpus-mined signals this phase adds
/// to <see cref="CraftRuleInducer"/> -- real simultaneous Tree/Matrix layer depth, cross-prop effect-choice
/// variety, and musical-key-change-correlated effect activity. Same hand-built <see cref="CorpusSongAnalysis"/>
/// fixture convention as <see cref="StatisticsBuilderTests"/> -- no real audio decode, no real xLights
/// install.
/// </summary>
public sealed class CraftRuleInducerCorpusSignalTests
{
    private const long TicksPerBar = 20_000_000L; // 2 seconds/bar at a nominal tempo.
    private const int BarCount = 8;

    [Fact]
    public void HighEnergyTreeMatrixLayerFloor_Promoted_WhenRealSongsReachTheFloorDuringChorus()
    {
        // 3 fully overlapping placements on "Matrix1" during the song's own Chorus section -- a real
        // 3-layers-deep moment, the exact shape the floor rule checks for.
        var effects = new[]
        {
            Effect("Matrix1", "On", start: 0, end: 10_000),
            Effect("Matrix1", "Twinkle", start: 0, end: 10_000),
            Effect("Matrix1", "Sparkles", start: 0, end: 10_000),
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        var rule = rules.Promoted.Concat(rules.RejectedCandidates).Single(r => r.Id == "high-energy-tree-matrix-layer-floor");
        Assert.Equal(1.0, rule.Confidence);
        Assert.Contains(rules.Promoted, r => r.Id == "high-energy-tree-matrix-layer-floor");
    }

    [Fact]
    public void HighEnergyTreeMatrixLayerFloor_Rejected_WhenRealSongsNeverOverlapOnTheRichestProp()
    {
        // Same model, same section kind, but each placement runs one at a time -- never 2, let alone 3,
        // concurrent layers.
        var effects = new[]
        {
            Effect("Matrix1", "On", start: 0, end: 10_000),
            Effect("Matrix1", "Twinkle", start: 10_000, end: 20_000),
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        var rule = rules.Promoted.Concat(rules.RejectedCandidates).Single(r => r.Id == "high-energy-tree-matrix-layer-floor");
        Assert.Equal(0.0, rule.Confidence);
        Assert.DoesNotContain(rules.Promoted, r => r.Id == "high-energy-tree-matrix-layer-floor");
    }

    [Fact]
    public void ConcurrentPropsVaryEffectChoice_Promoted_WhenOverlappingPropsUseDifferentEffects()
    {
        var effects = new[]
        {
            Effect("Matrix1", "On", start: 0, end: 10_000),
            Effect("Outline1", "Twinkle", start: 0, end: 10_000), // overlaps Matrix1, different name.
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        var rule = rules.Promoted.Concat(rules.RejectedCandidates).Single(r => r.Id == "concurrent-props-vary-effect-choice");
        Assert.Equal(1.0, rule.Confidence);
        Assert.Contains(rules.Promoted, r => r.Id == "concurrent-props-vary-effect-choice");
    }

    [Fact]
    public void ConcurrentPropsVaryEffectChoice_Rejected_WhenOverlappingPropsAlwaysRunTheIdenticalEffect()
    {
        var effects = new[]
        {
            Effect("Matrix1", "On", start: 0, end: 10_000),
            Effect("Outline1", "On", start: 0, end: 10_000), // overlaps Matrix1, same name.
        };

        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus)).ToList();
        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        var rule = rules.Promoted.Concat(rules.RejectedCandidates).Single(r => r.Id == "concurrent-props-vary-effect-choice");
        Assert.Equal(0.0, rule.Confidence);
        Assert.DoesNotContain(rules.Promoted, r => r.Id == "concurrent-props-vary-effect-choice");
    }

    [Fact]
    public void KeyChangeCorrelatesWithEffectActivity_Promoted_WhenKeyChangesAreFollowedByABurstOfNewEffects()
    {
        // A sparse song overall (one placement every 4 seconds), except for a dense burst of 5 distinct
        // short placements right after the detected key change -- a real rate increase against this same
        // song's own sparse baseline.
        var effects = new List<ParsedEffectInstance>();
        for (var i = 0; i < 10; i++)
        {
            effects.Add(Effect("Matrix1", "On", start: i * 4_000, end: (i * 4_000) + 500));
        }

        var keyChangeMs = 40_000L;
        for (var i = 0; i < 5; i++)
        {
            effects.Add(Effect("Matrix1", "Twinkle", start: keyChangeMs + (i * 300), end: keyChangeMs + (i * 300) + 250));
        }

        var keyChanges = new[] { new KeyChange(keyChangeMs * TimeSpan.TicksPerMillisecond, "C Major", 0.8) };
        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus, keyChanges)).ToList();
        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        var rule = rules.Promoted.Concat(rules.RejectedCandidates).Single(r => r.Id == "key-change-correlates-with-elevated-effect-activity");
        Assert.Equal(1.0, rule.Confidence);
        Assert.Contains(rules.Promoted, r => r.Id == "key-change-correlates-with-elevated-effect-activity");
    }

    [Fact]
    public void KeyChangeCorrelatesWithEffectActivity_ExcludedFromStatistics_WhenSongHasNoDetectedKeyChange()
    {
        var effects = new[] { Effect("Matrix1", "On", start: 0, end: 10_000) };
        var songs = Enumerable.Range(0, 6).Select(i => BuildSong($"Song {i}", effects, SectionKind.Chorus, keyChanges: [])).ToList();

        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        var rule = rules.Promoted.Concat(rules.RejectedCandidates).Single(r => r.Id == "key-change-correlates-with-elevated-effect-activity");
        Assert.Equal(0, rule.SongCount); // No song had an applicable key change -- never counted as a false.
    }

    private static ParsedEffectInstance Effect(string modelName, string effectName, long start, long end) => new(
        modelName, "model", effectName, StartTimeMs: start, EndTimeMs: end,
        LayerIndex: 0, PaletteIndex: 0, SettingsString: "", PaletteString: "");

    private static CorpusSongAnalysis BuildSong(
        string title, IReadOnlyList<ParsedEffectInstance> effects, SectionKind sectionKind, IReadOnlyList<KeyChange>? keyChanges = null)
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

        return new CorpusSongAnalysis(entry, beatGrid, fingerprintSet, labels, phrases, aligned, KeyChanges: keyChanges);
    }
}
