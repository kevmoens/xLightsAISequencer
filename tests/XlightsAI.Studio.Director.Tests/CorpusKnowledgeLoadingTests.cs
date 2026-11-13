using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 10 §3f ("Activate corpus mining"): proves
/// <see cref="AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync"/> -- the fallback logic
/// <see cref="AiChoreographyWholeSongGenerator.GenerateAsync"/> now uses in place of the old permanently
/// hardcoded empty <see cref="CraftRules"/>/<see cref="ExemplarIndex"/> -- loads the real persisted
/// values when a <see cref="CorpusMiningRunner"/> run has written them, and falls back to the same empty
/// defaults (never throwing) whenever no knowledge directory is configured or corpus mining has simply
/// never been run yet.
/// </summary>
public sealed class CorpusKnowledgeLoadingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-corpus-knowledge-loading-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task NullKnowledgeDirectory_FallsBackToEmptyDefaults()
    {
        var (craftRules, exemplarIndex, patternLibrary, wholeCanvasCoOccurrence, layerComplexity) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(null, CancellationToken.None);

        Assert.Empty(craftRules.Promoted);
        Assert.Empty(craftRules.RejectedCandidates);
        Assert.Empty(exemplarIndex.Entries);
        Assert.Equal(0, patternLibrary.SongCount);
        Assert.Null(wholeCanvasCoOccurrence);
        Assert.Null(layerComplexity);
    }

    [Fact]
    public async Task KnowledgeDirectoryWithNoPersistedFiles_FallsBackToEmptyDefaults_NegativePath()
    {
        // Directory exists (a real composition root's own %AppData%/corpus-knowledge folder, created
        // but never populated) -- distinct from the "not configured at all" null case above, and from
        // the "directory does not exist on disk at all" case below: both stores' own LoadAsync throws
        // FileNotFoundException in either case, and both must be caught independently.
        Directory.CreateDirectory(_root);

        var (craftRules, exemplarIndex, patternLibrary, wholeCanvasCoOccurrence, layerComplexity) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(_root, CancellationToken.None);

        Assert.Empty(craftRules.Promoted);
        Assert.Empty(craftRules.RejectedCandidates);
        Assert.Empty(exemplarIndex.Entries);
        Assert.Equal(0, patternLibrary.SongCount);
        Assert.Null(wholeCanvasCoOccurrence);
        Assert.Null(layerComplexity);
    }

    [Fact]
    public async Task KnowledgeDirectoryDoesNotExistOnDisk_FallsBackToEmptyDefaults_NegativePath()
    {
        var neverCreated = Path.Combine(_root, "never-created");

        var (craftRules, exemplarIndex, patternLibrary, wholeCanvasCoOccurrence, layerComplexity) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(neverCreated, CancellationToken.None);

        Assert.Empty(craftRules.Promoted);
        Assert.Empty(exemplarIndex.Entries);
        Assert.Equal(0, patternLibrary.SongCount);
        Assert.Null(wholeCanvasCoOccurrence);
        Assert.Null(layerComplexity);
    }

    [Fact]
    public async Task KnowledgeDirectoryWithRealPersistedFiles_LoadsRealNonEmptyContent()
    {
        Directory.CreateDirectory(_root);

        var realCraftRules = new CraftRules(
            DateTimeOffset.UtcNow,
            MinConfidenceThreshold: 0.7,
            MinSongCountThreshold: 3,
            Promoted:
            [
                new CraftRule(
                    Id: "rule.test.001",
                    Statement: "A real mined rule from corpus mining.",
                    ApplicableInstanceCount: 10,
                    TrueInstanceCount: 9,
                    SongCount: 5,
                    HasCodePredicate: true)
            ],
            RejectedCandidates: []);
        await new JsonCraftRulesStore(_root).SaveAsync(realCraftRules, CancellationToken.None);

        var realExemplarIndex = new ExemplarIndex(
            DateTimeOffset.UtcNow,
            [
                new ExemplarEntry(
                    new ExemplarFingerprint(
                        SongTitle: "Test Song",
                        Provenance: CorpusProvenanceTag.UserAuthored,
                        StartBarIndex: 0,
                        EndBarIndexExclusive: 8,
                        TempoBpm: 120,
                        EnergyPercentile: 0.5,
                        SectionKind: SectionKind.Chorus,
                        BandBalance: null,
                        VocalActivityHint: null),
                    Effects: [])
            ]);
        await new JsonExemplarIndexStore(_root).SaveAsync(realExemplarIndex, CancellationToken.None);

        var realPatternLibrary = new PatternLibrary(
            GeneratedAtUtc: DateTimeOffset.UtcNow, SongCount: 31, TotalEffectCount: 66568,
            EffectFrequency: new Dictionary<string, int> { ["On"] = 100 },
            EffectFrequencyBySection: new Dictionary<SectionKind, IReadOnlyDictionary<string, int>>(),
            DensityBySection: new Dictionary<SectionKind, double> { [SectionKind.Chorus] = 13.05, [SectionKind.Verse] = 5.19 },
            LayerCountPerModelDistribution: new Dictionary<int, int> { [1] = 200, [2] = 80 },
            BlendModeUsage: new Dictionary<string, int>(),
            PaletteActiveSlotDistribution: new Dictionary<int, int>(),
            ModeACount: 500, ModeBCount: 300);
        await new JsonPatternLibraryStore(_root).SaveAsync(realPatternLibrary, CancellationToken.None);

        var realTrainingReport = new CorpusTrainingReport(
            GeneratedAtUtc: DateTimeOffset.UtcNow, ExternalSequencesRoot: @"C:\fake\external", DiscoveredSequenceCount: 10,
            ExcludedSequenceCount: 2, AnalyzedSequenceCount: 10, FailedSequenceCount: 0, FailureReasons: [],
            CatchAllCoverageRatioDistribution: new CoverageRatioDistribution(0, 0, null, null, null, null, new Dictionary<string, int>()),
            WholeCanvasCoOccurrence: new WholeCanvasCoOccurrenceStats(
                WindowSampleCount: 40, SongsWithWholeCanvasCount: 4,
                FoundationCoFireRatio: 0.2, SubjectCoFireRatio: 0.7, AccentCoFireRatio: 0.3,
                SectionKindHistogram: new Dictionary<string, int> { ["Chorus"] = 24 },
                EnergyTierHistogram: new Dictionary<string, int> { ["High"] = 30 },
                MedianDurationBars: 1.5, P90DurationBars: 2.0),
            LayerComplexity: new LayerComplexityStats(
                ThreePlus: new LayerComplexityTierStats(12, new Dictionary<string, int> { ["Chorus"] = 9, ["Solo"] = 3 }, 2.0, 4.0),
                FourPlus: new LayerComplexityTierStats(3, new Dictionary<string, int> { ["Chorus"] = 3 }, 1.5, 2.0),
                FivePlus: new LayerComplexityTierStats(0, new Dictionary<string, int>(), null, null)),
            ReuseRate: new ReuseRateStats(0, 0, 0, 0, null, null, null, new Dictionary<string, int>()));
        await new JsonCorpusTrainingReportStore(_root).SaveAsync(realTrainingReport, CancellationToken.None);

        var (craftRules, exemplarIndex, patternLibrary, wholeCanvasCoOccurrence, layerComplexity) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(_root, CancellationToken.None);

        Assert.Single(craftRules.Promoted);
        Assert.Equal("rule.test.001", craftRules.Promoted[0].Id);
        Assert.Single(exemplarIndex.Entries);
        Assert.Equal("Test Song", exemplarIndex.Entries[0].Fingerprint.SongTitle);
        Assert.Equal(31, patternLibrary.SongCount);
        Assert.Equal(13.05, patternLibrary.DensityBySection[SectionKind.Chorus]);
        Assert.NotNull(wholeCanvasCoOccurrence);
        Assert.Equal(40, wholeCanvasCoOccurrence.WindowSampleCount);
        Assert.Equal(0.7, wholeCanvasCoOccurrence.SubjectCoFireRatio);
        Assert.NotNull(layerComplexity);
        Assert.Equal(12, layerComplexity.ThreePlus.WindowCount);
        Assert.Equal(3, layerComplexity.FourPlus.WindowCount);
    }
}
