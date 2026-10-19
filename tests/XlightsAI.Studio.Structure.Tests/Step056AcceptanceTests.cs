using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-08 / Step 056 -- "Cluster exact and similar motifs with tolerance for variation." Required
/// acceptance: "Repeated eight-bar fixture shares motif ID; distinct passage does not." Implementation
/// guidance: "Use self-similarity and robust time-scaling to propose repeated motifs; score separately from
/// section label." Failure/edge case: "Invalid input, cancellation or stale persisted state must not
/// corrupt the project."
/// </summary>
public sealed class Step056AcceptanceTests : IDisposable
{
    private const long BarTicks = 20_000_000L;

    private static readonly MotifClusteringSettings DefaultSettings = new(
        CanonicalSampleCount: 4,
        SimilarityThreshold: 0.9,
        RecipeId: MotifClusterer.TimeScaledCosineRecipeId);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step056-{Guid.NewGuid():N}");

    public Step056AcceptanceTests()
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

    /// <summary>A deterministic, 4-dimensional per-bar feature pattern that cycles every 2 bars -- "chorus" and "verse" fixtures each use a different, fixed pattern function so repeated phrases of the same pattern are bit-identical and a distinct passage is clearly different (not merely noisy).</summary>
    private static BarFingerprint PatternBar(int index, Func<int, double[]> pattern) =>
        new(index, new TimeWindow(index * BarTicks, (index + 1) * BarTicks), pattern(index), Confidence: 1.0, IsSilent: false);

    private static double[] ChorusPattern(int offsetWithinPhrase) =>
        (offsetWithinPhrase % 2 == 0) ? [1.0, 0.0, 0.2, 0.1] : [0.2, 1.0, 0.0, 0.3];

    private static double[] VersePattern(int offsetWithinPhrase) =>
        (offsetWithinPhrase % 2 == 0) ? [0.0, 0.1, 1.0, 0.0] : [0.1, 0.0, 0.3, 1.0];

    private static SongFingerprintSet BuildFingerprintSet(SongId songId, string sourceHash, IReadOnlyList<BarFingerprint> bars) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceBeatGridVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new BarFingerprintSettings(ChromaBins: 2, RecipeId: "test-fixture-recipe/1.0.0"),
            Bars: bars);

    private static PhraseCandidate Phrase(int startBar, int endBarExclusive) =>
        new(
            Id: Guid.NewGuid(),
            SectionLabelId: Guid.NewGuid(),
            StartBarIndex: startBar,
            EndBarIndexExclusive: endBarExclusive,
            StartTicks: startBar * BarTicks,
            EndTicks: endBarExclusive * BarTicks,
            Bars: endBarExclusive - startBar,
            Source: PhraseBoundarySource.BarHypothesis,
            Confidence: 1.0,
            Evidence: ["test fixture"],
            SignatureHash: "unused-in-this-step",
            UserCorrected: false);

    private static PhraseSegmentationAnalysis BuildPhraseAnalysis(
        SongId songId, string sourceHash, Guid fingerprintSetVersionId, IReadOnlyList<PhraseCandidate> phrases) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceSectionLabelVersionId: Guid.NewGuid(),
            SourceFingerprintSetVersionId: fingerprintSetVersionId,
            SourceMelodyTrackId: null,
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new PhraseSegmentationSettings(8, 1, 5_000_000L, "test-fixture-recipe/1.0.0"),
            Phrases: phrases);

    /// <summary>
    /// Three contiguous 8-bar phrases: Chorus A [0,8), Verse [8,16) (a genuinely distinct passage), Chorus
    /// B [16,24) (bit-identical pattern to Chorus A -- the "repeated eight-bar fixture," this step's own
    /// required acceptance, verbatim).
    /// </summary>
    private static (SongFingerprintSet FingerprintSet, PhraseSegmentationAnalysis PhraseAnalysis, PhraseCandidate ChorusA, PhraseCandidate Verse, PhraseCandidate ChorusB)
        BuildRepeatedChorusFixture(SongId songId, string sourceHash)
    {
        var bars = new List<BarFingerprint>();
        for (var i = 0; i < 8; i++) bars.Add(PatternBar(i, ChorusPattern));
        for (var i = 8; i < 16; i++) bars.Add(PatternBar(i, offset => VersePattern(offset - 8)));
        for (var i = 16; i < 24; i++) bars.Add(PatternBar(i, offset => ChorusPattern(offset - 16)));

        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);

        var chorusA = Phrase(0, 8);
        var verse = Phrase(8, 16);
        var chorusB = Phrase(16, 24);
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [chorusA, verse, chorusB]);

        return (fingerprintSet, phraseAnalysis, chorusA, verse, chorusB);
    }

    [Fact]
    public async Task Step056_Acceptance_Passes_RepeatedEightBarFixtureSharesMotifId_DistinctPassageDoesNot()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, phraseAnalysis, chorusA, verse, chorusB) = BuildRepeatedChorusFixture(songId, sourceHash);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var cacheDir = Path.Combine(_tempDir, "mc-cache");
        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(cacheDir));

        var analysis = await clusterer.ClusterAsync(context, DefaultSettings, CancellationToken.None);

        // "Repeated eight-bar fixture shares motif ID": Chorus A and Chorus B land in exactly one group together.
        Assert.Single(analysis.Groups);
        var group = analysis.Groups[0];
        Assert.Equal(2, group.PhraseIds.Count);
        Assert.Contains(chorusA.Id, group.PhraseIds);
        Assert.Contains(chorusB.Id, group.PhraseIds);
        Assert.Equal(chorusA.Id, group.RepresentativePhraseId);
        Assert.InRange(group.Confidence, 0.9, 1.0);
        Assert.NotEmpty(group.Evidence);

        // "...distinct passage does not [share a motif ID]": the Verse never appears in any group.
        Assert.DoesNotContain(analysis.Groups, g => g.PhraseIds.Contains(verse.Id));

        // The underlying pairwise link is itself inspectable.
        Assert.Contains(analysis.Links, l => l.SourcePhraseId == chorusA.Id && l.TargetPhraseId == chorusB.Id);
        Assert.DoesNotContain(analysis.Links, l =>
            (l.SourcePhraseId == verse.Id || l.TargetPhraseId == verse.Id));

        // Provenance (Section 4).
        Assert.Equal(sourceHash, analysis.SourceHash);
        Assert.Equal(phraseAnalysis.VersionId, analysis.SourcePhraseSegmentationVersionId);
        Assert.Equal(fingerprintSet.VersionId, analysis.SourceFingerprintSetVersionId);
        Assert.Equal(MotifClusterer.Version, analysis.AnalyzerVersion);
        Assert.True(File.Exists(Path.Combine(cacheDir, $"motif-clustering-{songId.Value:N}.json")));

        // Stale-cache handling: a brand-new clusterer/store pair over the same on-disk directory reuses the
        // cached result (same VersionId) rather than silently recomputing.
        var reopened = new MotifClusterer(new JsonMotifClusteringStore(cacheDir));
        var reopenedAnalysis = await reopened.ClusterAsync(context, DefaultSettings, CancellationToken.None);
        Assert.Equal(analysis.VersionId, reopenedAnalysis.VersionId);
    }

    [Fact]
    public async Task ClusterAsync_RobustTimeScaling_DifferentBarCountsStillLink()
    {
        // Chorus A is 8 bars; Chorus B is 9 bars using the same repeating pattern -- "robust time-scaling"
        // (this step's own implementation guidance, verbatim) must still resample both to the same
        // canonical length and link them.
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";

        var bars = new List<BarFingerprint>();
        for (var i = 0; i < 8; i++) bars.Add(PatternBar(i, ChorusPattern));
        for (var i = 8; i < 17; i++) bars.Add(PatternBar(i, offset => ChorusPattern(offset - 8)));

        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var chorusA = Phrase(0, 8);
        var chorusBNineBars = Phrase(8, 17);
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [chorusA, chorusBNineBars]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(Path.Combine(_tempDir, "mc-cache")));
        var analysis = await clusterer.ClusterAsync(context, DefaultSettings, CancellationToken.None);

        Assert.Single(analysis.Groups);
        Assert.Equal(2, analysis.Groups[0].PhraseIds.Count);
    }

    [Fact]
    public async Task ClusterAsync_FewerThanTwoPhrases_ReturnsEmptyResult_HandlesExplicitly()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var bars = Enumerable.Range(0, 8).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Phrase(0, 8)]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(Path.Combine(_tempDir, "mc-cache")));
        var analysis = await clusterer.ClusterAsync(context, DefaultSettings, CancellationToken.None);

        Assert.Empty(analysis.Groups);
        Assert.Empty(analysis.Links);
    }

    [Fact]
    public async Task ClusterAsync_PhraseReferencesMissingBar_ThrowsMotifClusteringInvalidException_NeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var bars = Enumerable.Range(0, 4).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        // Phrase references bars [0,8) but the fingerprint set only has bars [0,4) -- malformed/mismatched.
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Phrase(0, 4), Phrase(4, 8)]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var cacheDir = Path.Combine(_tempDir, "mc-cache");
        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(cacheDir));

        var ex = await Assert.ThrowsAsync<MotifClusteringInvalidException>(
            () => clusterer.ClusterAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("does not exist in the source fingerprint set", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(cacheDir, $"motif-clustering-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task ClusterAsync_EmptySourceHash_ThrowsMotifClusteringInvalidException_MissingMediaNegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var bars = Enumerable.Range(0, 8).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, "", bars);
        var phraseAnalysis = BuildPhraseAnalysis(songId, "", fingerprintSet.VersionId, [Phrase(0, 4), Phrase(4, 8)]);
        var context = new MotifClusteringContext(songId, "", phraseAnalysis, fingerprintSet);

        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(Path.Combine(_tempDir, "mc-cache")));

        var ex = await Assert.ThrowsAsync<MotifClusteringInvalidException>(
            () => clusterer.ClusterAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("missing media", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClusterAsync_MismatchedProvenanceVersionIds_ThrowsMotifClusteringInvalidException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var bars = Enumerable.Range(0, 8).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        // Phrase analysis claims a different (stale) fingerprint set version.
        var phraseAnalysis = new PhraseSegmentationAnalysis(
            Guid.NewGuid(), songId, sourceHash, Guid.NewGuid(), Guid.NewGuid(), null,
            "test-fixture/1.0.0", new PhraseSegmentationSettings(8, 1, 5_000_000L, "test-fixture-recipe/1.0.0"),
            [Phrase(0, 4), Phrase(4, 8)]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(Path.Combine(_tempDir, "mc-cache")));

        var ex = await Assert.ThrowsAsync<MotifClusteringInvalidException>(
            () => clusterer.ClusterAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("SourceFingerprintSetVersionId", ex.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ClusterAsync_InvalidCanonicalSampleCount_ThrowsMotifClusteringInvalidException(int canonicalSampleCount)
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var bars = Enumerable.Range(0, 8).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Phrase(0, 4), Phrase(4, 8)]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(Path.Combine(_tempDir, "mc-cache")));
        var badSettings = DefaultSettings with { CanonicalSampleCount = canonicalSampleCount };

        var ex = await Assert.ThrowsAsync<MotifClusteringInvalidException>(
            () => clusterer.ClusterAsync(context, badSettings, CancellationToken.None));
        Assert.Contains("CanonicalSampleCount", ex.Reason);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task ClusterAsync_InvalidSimilarityThreshold_ThrowsMotifClusteringInvalidException(double similarityThreshold)
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var bars = Enumerable.Range(0, 8).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Phrase(0, 4), Phrase(4, 8)]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(Path.Combine(_tempDir, "mc-cache")));
        var badSettings = DefaultSettings with { SimilarityThreshold = similarityThreshold };

        var ex = await Assert.ThrowsAsync<MotifClusteringInvalidException>(
            () => clusterer.ClusterAsync(context, badSettings, CancellationToken.None));
        Assert.Contains("SimilarityThreshold", ex.Reason);
    }

    [Fact]
    public async Task ClusterAsync_NullArguments_ThrowArgumentNullException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var bars = Enumerable.Range(0, 8).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Phrase(0, 4), Phrase(4, 8)]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(Path.Combine(_tempDir, "mc-cache")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => clusterer.ClusterAsync(null!, DefaultSettings, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => clusterer.ClusterAsync(context, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ClusterAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var bars = Enumerable.Range(0, 8).Select(i => PatternBar(i, ChorusPattern)).ToList();
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var phraseAnalysis = BuildPhraseAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Phrase(0, 4), Phrase(4, 8)]);
        var context = new MotifClusteringContext(songId, sourceHash, phraseAnalysis, fingerprintSet);

        var cacheDir = Path.Combine(_tempDir, "mc-cache");
        var clusterer = new MotifClusterer(new JsonMotifClusteringStore(cacheDir));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => clusterer.ClusterAsync(context, DefaultSettings, cts.Token));
        Assert.False(File.Exists(Path.Combine(cacheDir, $"motif-clustering-{songId.Value:N}.json")));
    }
}
