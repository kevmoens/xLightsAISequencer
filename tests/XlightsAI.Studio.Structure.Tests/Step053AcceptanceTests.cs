using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-08 / Step 053 -- "Detect candidate novelty/section boundaries with confidence." Required
/// acceptance: "Annotated song has useful boundaries and error report." Failure/edge case: "Malformed
/// proposal, no-improvement and exhausted retry budget must not auto-commit."
/// </summary>
public sealed class Step053AcceptanceTests : IDisposable
{
    private static readonly SectionBoundarySettings Settings = new(
        InitialNoveltyThreshold: 0.5,
        MinNoveltyThreshold: 0.05,
        MinBoundaryCount: 1,
        MaxRefinementIterations: 6,
        RecipeId: SectionBoundaryDetector.AdjacentCosineNoveltyRecipeId);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step053-{Guid.NewGuid():N}");

    public Step053AcceptanceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static BarFingerprint Bar(int index, params double[] features) =>
        new(index, new TimeWindow(index * 20_000_000L, (index + 1) * 20_000_000L), features, Confidence: 1.0, IsSilent: false);

    private static SongFingerprintSet BuildFingerprintSet(SongId songId, string sourceHash, IReadOnlyList<BarFingerprint> bars) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceBeatGridVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new BarFingerprintSettings(ChromaBins: 2, RecipeId: "test-fixture-recipe/1.0.0"),
            Bars: bars);

    [Fact]
    public async Task Step053_Acceptance_Passes_AnnotatedSongHasUsefulBoundariesAndErrorReport()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";

        // Four bars: 0 and 1 share an identical feature vector (no real change -- not a boundary);
        // bar 2 is a deliberately different motif (a real novelty event -- a useful boundary); bar 3
        // repeats bar 2's own content (no further change from its own immediate predecessor).
        var bars = new[]
        {
            Bar(0, 1.0, 0.0, 0.0),
            Bar(1, 1.0, 0.0, 0.0),
            Bar(2, 0.0, 0.0, 1.0),
            Bar(3, 0.0, 0.0, 1.0),
        };
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var context = new SectionBoundaryContext(songId, sourceHash, fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));
        var analysis = await detector.DetectAsync(context, Settings, CancellationToken.None);

        // "useful boundaries": the trivial song-start entry (bar 0) plus the one real novelty event
        // (bar 2) -- never bar 1 or bar 3, which are each identical to their own immediate predecessor.
        Assert.Equal([0, 2], analysis.Boundaries.Select(b => b.BarIndex).ToArray());
        Assert.Equal(1.0, analysis.Boundaries[0].Confidence);
        Assert.Equal(0.0, analysis.Boundaries[0].NoveltyScore);
        Assert.True(analysis.Boundaries[1].NoveltyScore > 0.9, $"expected a strong novelty score at bar 2; got {analysis.Boundaries[1].NoveltyScore}.");
        Assert.All(analysis.Boundaries, b => Assert.True(b.SnappedToBeatGrid));
        Assert.All(analysis.Boundaries, b => Assert.False(b.UserApproved));

        // "...and error report": converged on the first iteration, since the initial threshold already
        // found MinBoundaryCount's own single required non-trivial candidate.
        Assert.True(analysis.ErrorReport.Converged);
        Assert.False(analysis.ErrorReport.RetryBudgetExhausted);
        Assert.Equal(1, analysis.ErrorReport.IterationsRun);
        Assert.Empty(analysis.ErrorReport.Warnings);

        // Provenance recorded (Section 4's "every cache key includes algorithm/model/settings versions").
        Assert.Equal(sourceHash, analysis.SourceHash);
        Assert.Equal(fingerprintSet.VersionId, analysis.SourceFingerprintSetVersionId);
        Assert.Equal(SectionBoundaryDetector.Version, analysis.AnalyzerVersion);
        Assert.Equal(Settings, analysis.Settings);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sb-cache", $"section-boundaries-{songId.Value:N}.json")));

        // Caching ("with caching," mirroring Step 052's own established convention): rebuilding from the
        // exact same context/settings reopens the cached analysis rather than recomputing a new VersionId.
        var cached = await detector.DetectAsync(context, Settings, CancellationToken.None);
        Assert.Equal(analysis.VersionId, cached.VersionId);
    }

    [Fact]
    public async Task DetectAsync_NoRealNoveltyEverAnywhere_ReportsExhaustedRetryBudget_DoesNotFabricateBoundaries()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";

        // Every bar is numerically identical -- no real change anywhere in the song. No threshold,
        // however far relaxed, can ever find a genuine novelty event here.
        var bars = new[]
        {
            Bar(0, 1.0, 0.0),
            Bar(1, 1.0, 0.0),
            Bar(2, 1.0, 0.0),
            Bar(3, 1.0, 0.0),
        };
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var context = new SectionBoundaryContext(songId, sourceHash, fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));
        var analysis = await detector.DetectAsync(context, Settings, CancellationToken.None);

        // "no-improvement and exhausted retry budget must not auto-commit": the search must not
        // fabricate a confident-looking boundary set when none of the actual evidence supports one --
        // only the trivial song-start entry is reported, honestly flagged as not converged.
        Assert.Equal([0], analysis.Boundaries.Select(b => b.BarIndex).ToArray());
        Assert.False(analysis.ErrorReport.Converged);
        Assert.True(analysis.ErrorReport.RetryBudgetExhausted);
        Assert.NotEmpty(analysis.ErrorReport.Warnings);
    }

    [Fact]
    public async Task DetectAsync_OutOfOrderBarsProduceNonMonotonicCandidates_ThrowsSectionBoundaryInvalidException_MalformedProposalNeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";

        // A corrupt/mismatched upstream fingerprint set whose own Bars list is not ordered by
        // BarIndex -- bar 2 appears before bar 1. Both transitions carry real novelty (every threshold
        // in this fixture accepts them), so the resulting candidate sequence is 0 -> 2 -> 1: not
        // strictly increasing. This must be rejected, not silently persisted as a valid analysis.
        var bars = new[]
        {
            Bar(0, 1.0, 0.0),
            Bar(2, 0.0, 1.0),
            Bar(1, 1.0, 0.0),
        };
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var context = new SectionBoundaryContext(songId, sourceHash, fingerprintSet);

        var lenientSettings = Settings with { InitialNoveltyThreshold = 0.01, MinNoveltyThreshold = 0.001 };
        var storeDir = Path.Combine(_tempDir, "sb-cache");
        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(storeDir));

        var ex = await Assert.ThrowsAsync<SectionBoundaryInvalidException>(
            () => detector.DetectAsync(context, lenientSettings, CancellationToken.None));
        Assert.Contains("not strictly increasing", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(storeDir, $"section-boundaries-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task DetectAsync_EmptyFingerprintSet_ReturnsEmptyBoundariesTriviallyConverged_HandlesSilenceExplicitly()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, []);
        var context = new SectionBoundaryContext(songId, sourceHash, fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));
        var analysis = await detector.DetectAsync(context, Settings, CancellationToken.None);

        Assert.Empty(analysis.Boundaries);
        Assert.True(analysis.ErrorReport.Converged);
        Assert.False(analysis.ErrorReport.RetryBudgetExhausted);
    }

    [Fact]
    public async Task DetectAsync_EmptySourceHash_ThrowsSectionBoundaryInvalidException_MissingMediaNegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var fingerprintSet = BuildFingerprintSet(songId, "", [Bar(0, 1.0, 0.0)]);
        var context = new SectionBoundaryContext(songId, "", fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));

        var ex = await Assert.ThrowsAsync<SectionBoundaryInvalidException>(
            () => detector.DetectAsync(context, Settings, CancellationToken.None));
        Assert.Contains("missing media", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_tempDir, "sb-cache", $"section-boundaries-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task DetectAsync_MismatchedSourceHash_ThrowsSectionBoundaryInvalidException()
    {
        var songId = new SongId(Guid.NewGuid());
        var fingerprintSet = BuildFingerprintSet(songId, "hash-a", [Bar(0, 1.0, 0.0)]);
        var context = new SectionBoundaryContext(songId, "hash-b", fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));

        var ex = await Assert.ThrowsAsync<SectionBoundaryInvalidException>(
            () => detector.DetectAsync(context, Settings, CancellationToken.None));
        Assert.Contains("SourceHash", ex.Reason);
    }

    [Theory]
    [InlineData(0.0, 0.05)]
    [InlineData(1.5, 0.05)]
    public async Task DetectAsync_InvalidInitialNoveltyThreshold_ThrowsSectionBoundaryInvalidException(double initial, double min)
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, [Bar(0, 1.0, 0.0)]);
        var context = new SectionBoundaryContext(songId, sourceHash, fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));
        var badSettings = Settings with { InitialNoveltyThreshold = initial, MinNoveltyThreshold = min };

        var ex = await Assert.ThrowsAsync<SectionBoundaryInvalidException>(
            () => detector.DetectAsync(context, badSettings, CancellationToken.None));
        Assert.Contains("InitialNoveltyThreshold", ex.Reason);
    }

    [Fact]
    public async Task DetectAsync_NonPositiveMaxRefinementIterations_ThrowsSectionBoundaryInvalidException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, [Bar(0, 1.0, 0.0)]);
        var context = new SectionBoundaryContext(songId, sourceHash, fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));

        var ex = await Assert.ThrowsAsync<SectionBoundaryInvalidException>(
            () => detector.DetectAsync(context, Settings with { MaxRefinementIterations = 0 }, CancellationToken.None));
        Assert.Contains("MaxRefinementIterations", ex.Reason);
    }

    [Fact]
    public async Task DetectAsync_NullArguments_ThrowArgumentNullException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, [Bar(0, 1.0, 0.0)]);
        var context = new SectionBoundaryContext(songId, sourceHash, fingerprintSet);

        var detector = new SectionBoundaryDetector(new JsonSectionBoundaryStore(Path.Combine(_tempDir, "sb-cache")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => detector.DetectAsync(null!, Settings, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => detector.DetectAsync(context, null!, CancellationToken.None));
    }
}
