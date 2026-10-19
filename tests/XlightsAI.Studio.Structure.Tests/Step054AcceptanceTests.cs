using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-08 / Step 054 -- "Assign candidate intro/verse/chorus/bridge labels and support overrides."
/// Required acceptance: "UI shows provenance/confidence; overrides survive restart." Failure/edge case:
/// "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit" (this step's own
/// substitute for "no-improvement" is the explicit <see cref="SectionKind.Unknown"/> fallback -- see
/// <see cref="SectionLabelClassifier"/>'s own remarks).
/// </summary>
public sealed class Step054AcceptanceTests : IDisposable
{
    private static readonly SectionLabelSettings PermissiveSettings = new(
        RepetitionSimilarityThreshold: 0.95,
        MinConfidenceToLabel: 0.4,
        RecipeId: SectionLabelClassifier.RepetitionAndPositionRecipeId);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step054-{Guid.NewGuid():N}");

    public Step054AcceptanceTests()
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

    private static BarFingerprint Bar(int index, bool isSilent, params double[] features) =>
        new(index, new TimeWindow(index * 20_000_000L, (index + 1) * 20_000_000L), features, Confidence: 1.0, IsSilent: isSilent);

    private static SongFingerprintSet BuildFingerprintSet(SongId songId, string sourceHash, IReadOnlyList<BarFingerprint> bars) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceBeatGridVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new BarFingerprintSettings(ChromaBins: 2, RecipeId: "test-fixture-recipe/1.0.0"),
            Bars: bars);

    private static SectionBoundaryAnalysis BuildBoundaryAnalysis(
        SongId songId, string sourceHash, Guid fingerprintSetVersionId, IReadOnlyList<SectionBoundaryCandidate> boundaries) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceFingerprintSetVersionId: fingerprintSetVersionId,
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new SectionBoundarySettings(0.5, 0.05, 1, 6, "test-fixture-recipe/1.0.0"),
            Boundaries: boundaries,
            ErrorReport: new SectionBoundaryErrorReport(1, true, false, []));

    private static SectionBoundaryCandidate Boundary(int barIndex) =>
        new(Guid.NewGuid(), barIndex, barIndex * 20_000_000L, NoveltyScore: 1.0, Confidence: 1.0, SnappedToBeatGrid: true, UserApproved: false);

    /// <summary>
    /// Six one-bar segments: Intro, Verse, Chorus, Bridge, a repeat of the same Chorus, Outro -- the
    /// phase gate's own scenario, verbatim: "Analyze a song with repeated chorus; show two matching
    /// phrases highlighted." Bars 2 and 4 are numerically identical (cosine similarity 1.0); every other
    /// pair is well below <see cref="PermissiveSettings"/>'s own 0.95 threshold.
    /// </summary>
    private static (SongFingerprintSet FingerprintSet, SectionBoundaryAnalysis BoundaryAnalysis) BuildRepeatedChorusFixture(
        SongId songId, string sourceHash)
    {
        var bars = new[]
        {
            Bar(0, false, 0.2, 0.0, 0.0),  // Intro
            Bar(1, false, 0.5, 0.5, 0.0),  // Verse
            Bar(2, false, 1.0, 0.0, 1.0),  // Chorus (A)
            Bar(3, false, 0.3, 0.9, 0.3),  // Bridge
            Bar(4, false, 1.0, 0.0, 1.0),  // Chorus (A repeat)
            Bar(5, false, 0.1, 0.1, 0.1),  // Outro
        };
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, bars);
        var boundaries = Enumerable.Range(0, 6).Select(Boundary).ToArray();
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, fingerprintSet.VersionId, boundaries);
        return (fingerprintSet, boundaryAnalysis);
    }

    [Fact]
    public async Task Step054_Acceptance_Passes_ShowsProvenanceAndConfidence_OverridesSurviveRestart()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, boundaryAnalysis) = BuildRepeatedChorusFixture(songId, sourceHash);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var labelCacheDir = Path.Combine(_tempDir, "sl-cache");
        var overrideDir = Path.Combine(_tempDir, "sl-overrides");

        var classifier = new SectionLabelClassifier(new JsonSectionLabelStore(labelCacheDir), new JsonSectionLabelOverrideStore(overrideDir));
        var analysis = await classifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None);

        // Candidate intro/verse/chorus/bridge labels, verbatim.
        Assert.Equal(6, analysis.Labels.Count);
        Assert.Equal(SectionKind.Intro, analysis.Labels[0].Kind);
        Assert.Equal(SectionKind.Verse, analysis.Labels[1].Kind);
        Assert.Equal(SectionKind.Chorus, analysis.Labels[2].Kind);
        Assert.Equal(SectionKind.Bridge, analysis.Labels[3].Kind);
        Assert.Equal(SectionKind.Chorus, analysis.Labels[4].Kind);
        Assert.Equal(SectionKind.Outro, analysis.Labels[5].Kind);

        // "with confidence and evidence": every candidate carries an inspectable confidence and a
        // non-empty, human-readable reason -- never a bare, unexplained label.
        Assert.All(analysis.Labels, l => Assert.NotEmpty(l.Evidence));
        Assert.All(analysis.Labels, l => Assert.InRange(l.Confidence, 0.0, 1.0));
        Assert.Equal(1.0, analysis.Labels[2].Confidence, precision: 6);
        Assert.Equal(1.0, analysis.Labels[4].Confidence, precision: 6);
        Assert.Contains("Recurs", analysis.Labels[2].Evidence[0]);
        Assert.All(analysis.Labels, l => Assert.False(l.UserCorrected));

        // "UI shows provenance": every link in the chain is recorded (Section 4).
        Assert.Equal(sourceHash, analysis.SourceHash);
        Assert.Equal(boundaryAnalysis.VersionId, analysis.SourceSectionBoundaryVersionId);
        Assert.Equal(fingerprintSet.VersionId, analysis.SourceFingerprintSetVersionId);
        Assert.Equal(SectionLabelClassifier.Version, analysis.AnalyzerVersion);
        Assert.Equal(PermissiveSettings, analysis.Settings);
        Assert.True(File.Exists(Path.Combine(labelCacheDir, $"section-labels-{songId.Value:N}.json")));

        // "support overrides": a human reclassifies the Verse segment (index 1) as a Solo.
        var verseBoundaryId = analysis.Labels[1].BoundaryId;
        var afterOverride = await classifier.SetOverrideAsync(context, PermissiveSettings, verseBoundaryId, SectionKind.Solo, CancellationToken.None);
        Assert.Equal(SectionKind.Solo, afterOverride.Labels[1].Kind);
        Assert.True(afterOverride.Labels[1].UserCorrected);
        Assert.Equal(1.0, afterOverride.Labels[1].Confidence);
        Assert.True(File.Exists(Path.Combine(overrideDir, $"section-label-overrides-{songId.Value:N}.json")));

        // "overrides survive restart": brand-new store/classifier instances over the same on-disk
        // directories (simulating the application closing and reopening) must still surface the override.
        var reopenedClassifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(labelCacheDir), new JsonSectionLabelOverrideStore(overrideDir));
        var reopened = await reopenedClassifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None);
        Assert.Equal(SectionKind.Solo, reopened.Labels[1].Kind);
        Assert.True(reopened.Labels[1].UserCorrected);
        // The other, never-overridden segments are unaffected by the override.
        Assert.Equal(SectionKind.Intro, reopened.Labels[0].Kind);
        Assert.Equal(SectionKind.Chorus, reopened.Labels[2].Kind);
        Assert.False(reopened.Labels[2].UserCorrected);
    }

    [Fact]
    public async Task ClassifyAsync_ConfidenceBelowMinConfidenceToLabel_LabelsUnknown_NeverForcesClassicalStructure()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, boundaryAnalysis) = BuildRepeatedChorusFixture(songId, sourceHash);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        // A strict confidence floor only the (1.0-confidence) repeated-chorus segments can clear.
        var strictSettings = PermissiveSettings with { MinConfidenceToLabel = 0.9 };
        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        var analysis = await classifier.ClassifyAsync(context, strictSettings, CancellationToken.None);

        // "Unknown is valid; never force classical song structure": position-only heuristics (0.6/0.5
        // confidence) must not be dressed up as confident labels once they fall below the declared floor.
        Assert.Equal(SectionKind.Unknown, analysis.Labels[0].Kind);
        Assert.Equal(SectionKind.Unknown, analysis.Labels[1].Kind);
        Assert.Equal(SectionKind.Chorus, analysis.Labels[2].Kind);
        Assert.Equal(SectionKind.Unknown, analysis.Labels[3].Kind);
        Assert.Equal(SectionKind.Chorus, analysis.Labels[4].Kind);
        Assert.Equal(SectionKind.Unknown, analysis.Labels[5].Kind);
        Assert.Contains(analysis.Labels[0].Evidence, e => e.Contains("MinConfidenceToLabel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClassifyAsync_SingleSegmentSong_LabelsUnknown_NeverForcesClassicalStructure()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, [Bar(0, false, 1.0, 0.0)]);
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Boundary(0)]);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        var analysis = await classifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None);

        Assert.Single(analysis.Labels);
        Assert.Equal(SectionKind.Unknown, analysis.Labels[0].Kind);
        Assert.Equal(0.0, analysis.Labels[0].Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_SilentSegment_LabelsUnknown_NeverFabricatesEvidence()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash,
        [
            Bar(0, false, 1.0, 0.0),
            Bar(1, true, 0.0, 0.0),
        ]);
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Boundary(0), Boundary(1)]);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        var analysis = await classifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None);

        Assert.Equal(SectionKind.Unknown, analysis.Labels[1].Kind);
        Assert.Equal(0.0, analysis.Labels[1].Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_EmptyBoundaries_ReturnsEmptyLabels_HandlesSilenceExplicitly()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, []);
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, fingerprintSet.VersionId, []);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        var analysis = await classifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None);

        Assert.Empty(analysis.Labels);
    }

    [Fact]
    public async Task ClassifyAsync_OutOfOrderBoundaries_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash,
        [
            Bar(0, false, 1.0, 0.0),
            Bar(1, false, 0.0, 1.0),
            Bar(2, false, 1.0, 1.0),
        ]);
        // Malformed/corrupt upstream boundary analysis: not strictly increasing by BarIndex.
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Boundary(0), Boundary(2), Boundary(1)]);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var storeDir = Path.Combine(_tempDir, "sl-cache");
        var classifier = new SectionLabelClassifier(new JsonSectionLabelStore(storeDir), new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        var ex = await Assert.ThrowsAsync<SectionLabelInvalidException>(
            () => classifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None));
        Assert.Contains("not strictly increasing", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(storeDir, $"section-labels-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task SetOverrideAsync_UnknownBoundaryId_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, boundaryAnalysis) = BuildRepeatedChorusFixture(songId, sourceHash);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var overrideDir = Path.Combine(_tempDir, "sl-overrides");
        var classifier = new SectionLabelClassifier(new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")), new JsonSectionLabelOverrideStore(overrideDir));

        var ex = await Assert.ThrowsAsync<SectionLabelInvalidException>(
            () => classifier.SetOverrideAsync(context, PermissiveSettings, Guid.NewGuid(), SectionKind.Solo, CancellationToken.None));
        Assert.Contains("does not reference any segment", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(overrideDir, $"section-label-overrides-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task ClassifyAsync_EmptySourceHash_ThrowsSectionLabelInvalidException_MissingMediaNegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var fingerprintSet = BuildFingerprintSet(songId, "", [Bar(0, false, 1.0, 0.0)]);
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, "", fingerprintSet.VersionId, [Boundary(0)]);
        var context = new SectionLabelContext(songId, "", boundaryAnalysis, fingerprintSet);

        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        var ex = await Assert.ThrowsAsync<SectionLabelInvalidException>(
            () => classifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None));
        Assert.Contains("missing media", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClassifyAsync_MismatchedProvenanceVersionIds_ThrowsSectionLabelInvalidException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, [Bar(0, false, 1.0, 0.0)]);
        // Boundary analysis claims a different (stale) fingerprint set version.
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, Guid.NewGuid(), [Boundary(0)]);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        var ex = await Assert.ThrowsAsync<SectionLabelInvalidException>(
            () => classifier.ClassifyAsync(context, PermissiveSettings, CancellationToken.None));
        Assert.Contains("SourceFingerprintSetVersionId", ex.Reason);
    }

    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(1.5, 0.5)]
    public async Task ClassifyAsync_InvalidRepetitionSimilarityThreshold_ThrowsSectionLabelInvalidException(double threshold, double minConfidence)
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, [Bar(0, false, 1.0, 0.0)]);
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Boundary(0)]);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));
        var badSettings = PermissiveSettings with { RepetitionSimilarityThreshold = threshold, MinConfidenceToLabel = minConfidence };

        var ex = await Assert.ThrowsAsync<SectionLabelInvalidException>(
            () => classifier.ClassifyAsync(context, badSettings, CancellationToken.None));
        Assert.Contains("RepetitionSimilarityThreshold", ex.Reason);
    }

    [Fact]
    public async Task ClassifyAsync_NullArguments_ThrowArgumentNullException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, [Bar(0, false, 1.0, 0.0)]);
        var boundaryAnalysis = BuildBoundaryAnalysis(songId, sourceHash, fingerprintSet.VersionId, [Boundary(0)]);
        var context = new SectionLabelContext(songId, sourceHash, boundaryAnalysis, fingerprintSet);

        var classifier = new SectionLabelClassifier(
            new JsonSectionLabelStore(Path.Combine(_tempDir, "sl-cache")),
            new JsonSectionLabelOverrideStore(Path.Combine(_tempDir, "sl-overrides")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => classifier.ClassifyAsync(null!, PermissiveSettings, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => classifier.ClassifyAsync(context, null!, CancellationToken.None));
    }
}
