using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="PatternLibraryBuilder"/>, <see cref="ExemplarIndexBuilder"/> and
/// <see cref="CraftRuleInducer"/> against hand-built <see cref="CorpusSongAnalysis"/> fixtures — no real
/// audio decode, no real xLights install, so these run fast and everywhere. Real end-to-end wiring
/// (real decoded audio through the full Rhythm/Structure chain into these same builders) is covered by
/// <see cref="CorpusSongAnalyzerTests"/>.
/// </summary>
public sealed class StatisticsBuilderTests
{
    [Fact]
    public void PatternLibraryBuilder_ExcludesExternalReferenceSong_FromAggregateStatistics()
    {
        var mineable = BuildSong("Mineable Song", CorpusProvenanceTag.UserAuthored, effectName: "On", isModeA: false);
        var reference = BuildSong("Dubstep Christmas Light Show", CorpusProvenanceTag.ExternalReference, effectName: "Shockwave", isModeA: true);

        var library = PatternLibraryBuilder.Build([mineable, reference]);

        Assert.Equal(1, library.SongCount);
        Assert.True(library.EffectFrequency.ContainsKey("On"));
        Assert.False(library.EffectFrequency.ContainsKey("Shockwave"));
    }

    [Fact]
    public void PatternLibraryBuilder_ClassifiesModeAByTimingTrackKey()
    {
        var song = BuildSong("Song", CorpusProvenanceTag.UserAuthored, effectName: "Shockwave", isModeA: true);
        var library = PatternLibraryBuilder.Build([song]);

        Assert.Equal(4, library.ModeACount);
        Assert.Equal(0, library.ModeBCount);
        Assert.Equal(1.0, library.ModeARatio);
    }

    [Fact]
    public void PatternLibraryBuilder_CountsActivePaletteSlotsFromCheckboxes()
    {
        var entry = new ParsedEffectInstance("Model1", "model", "Twinkle", 0, 1000, 0, 0,
            SettingsString: "E_SLIDER_Twinkle_Count=30",
            PaletteString: "C_BUTTON_Palette1=#FFFFFF,C_CHECKBOX_Palette1=1,C_CHECKBOX_Palette3=1");
        var song = BuildSongFromEffects("Song", CorpusProvenanceTag.UserAuthored, [entry]);

        var library = PatternLibraryBuilder.Build([song]);

        Assert.Equal(1, library.PaletteActiveSlotDistribution[2]);
    }

    [Fact]
    public void ExemplarIndexBuilder_ComputesEnergyPercentileWithinOwnSong_AndFindSimilarReturnsClosestFirst()
    {
        var song = BuildSong("Song", CorpusProvenanceTag.UserAuthored, effectName: "On", isModeA: false);
        var index = ExemplarIndexBuilder.Build([song]);

        Assert.NotEmpty(index.Entries);
        foreach (var entry in index.Entries)
        {
            Assert.InRange(entry.Fingerprint.EnergyPercentile, 0.0, 1.0);
        }

        var query = index.Entries[0].Fingerprint;
        var retrieval = index.FindSimilar(query, 1);
        Assert.Single(retrieval.Entries);
        Assert.False(retrieval.IsFallback);
        Assert.Equal(query, retrieval.Entries[0].Fingerprint);
    }

    [Fact]
    public void ExemplarIndexBuilder_ExcludesExternalReferenceSong()
    {
        var reference = BuildSong("Dubstep Christmas Light Show", CorpusProvenanceTag.ExternalReference, effectName: "Shockwave", isModeA: true);
        var index = ExemplarIndexBuilder.Build([reference]);
        Assert.Empty(index.Entries);
    }

    [Fact]
    public void CraftRuleInducer_PromotesBlendModeDefaultRule_WhenEveryEffectOmitsExplicitLayerMethod()
    {
        var songs = Enumerable.Range(0, 6)
            .Select(i => BuildSong($"Song {i}", CorpusProvenanceTag.UserAuthored, effectName: "On", isModeA: false))
            .ToList();

        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        var blendRule = rules.Promoted.Concat(rules.RejectedCandidates).Single(r => r.Id == "blend-mode-left-at-default");
        Assert.Equal(1.0, blendRule.Confidence);
        Assert.Contains(rules.Promoted, r => r.Id == "blend-mode-left-at-default");
    }

    [Fact]
    public void CraftRuleInducer_RejectsRule_WhenFewerThanMinSongCountHaveApplicableInstances()
    {
        var songs = Enumerable.Range(0, 2)
            .Select(i => BuildSong($"Song {i}", CorpusProvenanceTag.UserAuthored, effectName: "On", isModeA: false))
            .ToList();

        var rules = CraftRuleInducer.InduceAndVerify(songs, minConfidence: 0.70, minSongCount: 5);

        Assert.DoesNotContain(rules.Promoted, r => r.Id == "blend-mode-left-at-default");
        Assert.Contains(rules.RejectedCandidates, r => r.Id == "blend-mode-left-at-default");
    }

    private static CorpusSongAnalysis BuildSong(string title, CorpusProvenanceTag provenance, string effectName, bool isModeA)
    {
        var settings = isModeA ? "E_CHOICE_Shockwave_TimingTrack=Beats" : "E_SLIDER_Twinkle_Count=30";
        var effects = Enumerable.Range(0, 4)
            .Select(i => new ParsedEffectInstance(
                "Model1", "model", effectName,
                StartTimeMs: i * 1000, EndTimeMs: (i * 1000) + 900,
                LayerIndex: 0, PaletteIndex: 0,
                SettingsString: settings,
                PaletteString: "C_BUTTON_Palette1=#FFFFFF"))
            .ToList();

        return BuildSongFromEffects(title, provenance, effects);
    }

    private static CorpusSongAnalysis BuildSongFromEffects(string title, CorpusProvenanceTag provenance, IReadOnlyList<ParsedEffectInstance> effects)
    {
        var entry = new CorpusEntry($@"C:\fake\{title}.xsq", $@"C:\fake\{title}.mp3", title, provenance, [], "fixture");

        const int barCount = 8;
        const long ticksPerBar = 20_000_000L; // 2 seconds/bar at a nominal tempo
        var bars = Enumerable.Range(0, barCount)
            .Select(i => new BarFingerprint(
                BarIndex: i,
                Window: new TimeWindow(i * ticksPerBar, (i + 1) * ticksPerBar),
                Features: [i / (double)(barCount - 1), 0.0],
                Confidence: 1.0,
                IsSilent: false))
            .ToList();

        var fingerprintSet = new SongFingerprintSet(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), "fixture/1.0",
            new BarFingerprintSettings(12, "fixture-recipe"), bars);

        var beats = Enumerable.Range(0, barCount * 4)
            .Select(i => new BeatMarker(Guid.NewGuid(), i * (ticksPerBar / 4), i % 4, i / 4, 1.0, false))
            .ToArray();
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", Guid.NewGuid(), Guid.NewGuid(), "fixture/1.0",
            new BeatGridSettings(4, 1000), new TempoHypothesis(120, 1.0, "4/4"), beats, [new TempoHypothesis(120, 1.0, "4/4")], 0);

        var label = new SectionLabelCandidate(Guid.NewGuid(), Guid.NewGuid(), 0, barCount, SectionKind.Verse, 1.0, ["fixture"], false);
        var labels = new SectionLabelAnalysis(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", Guid.NewGuid(), fingerprintSet.VersionId, "fixture/1.0",
            new SectionLabelSettings(0.85, 0.5, "fixture-recipe"), [label]);

        var phrase = new PhraseCandidate(
            Guid.NewGuid(), label.Id, 0, barCount, 0, barCount * ticksPerBar, barCount,
            PhraseBoundarySource.BarHypothesis, 1.0, ["fixture"], "sig", false);
        var phrases = new PhraseSegmentationAnalysis(
            Guid.NewGuid(), fingerprintSet.SongId, "hash", labels.VersionId, fingerprintSet.VersionId, null, "fixture/1.0",
            new PhraseSegmentationSettings(8, 1, 2_000_000, "fixture-recipe"), [phrase]);

        var aligned = EffectAligner.Align(effects, beatGrid, fingerprintSet, labels, phrases);

        return new CorpusSongAnalysis(entry, beatGrid, fingerprintSet, labels, phrases, aligned);
    }
}
