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
        var (craftRules, exemplarIndex) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(null, CancellationToken.None);

        Assert.Empty(craftRules.Promoted);
        Assert.Empty(craftRules.RejectedCandidates);
        Assert.Empty(exemplarIndex.Entries);
    }

    [Fact]
    public async Task KnowledgeDirectoryWithNoPersistedFiles_FallsBackToEmptyDefaults_NegativePath()
    {
        // Directory exists (a real composition root's own %AppData%/corpus-knowledge folder, created
        // but never populated) -- distinct from the "not configured at all" null case above, and from
        // the "directory does not exist on disk at all" case below: both stores' own LoadAsync throws
        // FileNotFoundException in either case, and both must be caught independently.
        Directory.CreateDirectory(_root);

        var (craftRules, exemplarIndex) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(_root, CancellationToken.None);

        Assert.Empty(craftRules.Promoted);
        Assert.Empty(craftRules.RejectedCandidates);
        Assert.Empty(exemplarIndex.Entries);
    }

    [Fact]
    public async Task KnowledgeDirectoryDoesNotExistOnDisk_FallsBackToEmptyDefaults_NegativePath()
    {
        var neverCreated = Path.Combine(_root, "never-created");

        var (craftRules, exemplarIndex) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(neverCreated, CancellationToken.None);

        Assert.Empty(craftRules.Promoted);
        Assert.Empty(exemplarIndex.Entries);
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

        var (craftRules, exemplarIndex) = await AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync(_root, CancellationToken.None);

        Assert.Single(craftRules.Promoted);
        Assert.Equal("rule.test.001", craftRules.Promoted[0].Id);
        Assert.Single(exemplarIndex.Entries);
        Assert.Equal("Test Song", exemplarIndex.Entries[0].Fingerprint.SongTitle);
    }
}
