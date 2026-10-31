using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>
/// PHASE-08 / Step 055 -- "Segment sections into phrase units with manual corrections." Required
/// acceptance: "Phrase boundaries align to bars or user-chosen events." Implementation guidance: "Segment
/// into phrases with bar-based hypothesis and melodic cadences; permit irregular length and phrase
/// overlaps only by deliberate policy." Failure/edge case: "Invalid input, cancellation or stale persisted
/// state must not corrupt the project."
/// </summary>
public sealed class Step055AcceptanceTests : IDisposable
{
    private const long BarTicks = 20_000_000L;

    private static readonly PhraseSegmentationSettings DefaultSettings = new(
        TargetPhraseLengthBars: 4,
        CadenceSearchRadiusBars: 1,
        MinCadenceGapTicks: 5_000_000L,
        RecipeId: PhraseSegmenter.BarHypothesisWithCadenceRecipeId);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step055-{Guid.NewGuid():N}");

    public Step055AcceptanceTests()
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

    private static BarFingerprint Bar(int index) =>
        new(index, new TimeWindow(index * BarTicks, (index + 1) * BarTicks), [1.0, 0.0], Confidence: 1.0, IsSilent: false);

    private static SongFingerprintSet BuildFingerprintSet(SongId songId, string sourceHash, int barCount) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceBeatGridVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new BarFingerprintSettings(ChromaBins: 1, RecipeId: "test-fixture-recipe/1.0.0"),
            Bars: Enumerable.Range(0, barCount).Select(Bar).ToArray());

    private static SectionLabelCandidate SectionLabel(int startBar, int endBarExclusive, SectionKind kind) =>
        new(Guid.NewGuid(), Guid.NewGuid(), startBar, endBarExclusive, kind, Confidence: 1.0, Evidence: ["test fixture"], UserCorrected: false);

    private static SectionLabelAnalysis BuildLabelAnalysis(
        SongId songId, string sourceHash, Guid fingerprintSetVersionId, IReadOnlyList<SectionLabelCandidate> labels) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: sourceHash,
            SourceSectionBoundaryVersionId: Guid.NewGuid(),
            SourceFingerprintSetVersionId: fingerprintSetVersionId,
            AnalyzerVersion: "test-fixture/1.0.0",
            Settings: new SectionLabelSettings(0.95, 0.4, "test-fixture-recipe/1.0.0"),
            Labels: labels);

    private static NoteEvent Note(long startTicks, long endTicks) =>
        new(Guid.NewGuid(), startTicks, endTicks, MidiNote: 60, Velocity: 1.0, Confidence: 1.0, UserCorrected: false);

    private static MelodyTrack BuildMelodyTrack(string sourceHash, IReadOnlyList<NoteEvent> notes) =>
        new(Guid.NewGuid(), sourceHash, notes, Gestures: []);

    /// <summary>
    /// Verse (bars [0,4), shorter than or equal to TargetPhraseLengthBars -- a single irregular-length
    /// phrase) followed by Chorus (bars [4,12), long enough for the bar-hypothesis to propose an internal
    /// split at bar 8, which a melodic cadence at bar 7 snaps to -- "with bar-based hypothesis and
    /// melodic cadences," verbatim). A melody rest (gap) starts exactly at bar 7's own downbeat
    /// (140,000,000 ticks) and the next note starts well inside bar 8 (165,000,000 ticks), a gap of
    /// 25,000,000 ticks -- comfortably above <see cref="DefaultSettings"/>'s own 5,000,000-tick floor and
    /// within its own one-bar CadenceSearchRadiusBars of the bar-8 hypothesis.
    /// </summary>
    private static (SongFingerprintSet FingerprintSet, SectionLabelAnalysis LabelAnalysis, MelodyTrack MelodyTrack, Guid VerseId, Guid ChorusId)
        BuildVerseChorusFixture(SongId songId, string sourceHash)
    {
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 12);
        var verse = SectionLabel(0, 4, SectionKind.Verse);
        var chorus = SectionLabel(4, 12, SectionKind.Chorus);
        var labelAnalysis = BuildLabelAnalysis(songId, sourceHash, fingerprintSet.VersionId, [verse, chorus]);

        var melodyTrack = BuildMelodyTrack(sourceHash,
        [
            Note(0, 7 * BarTicks),               // ends exactly at bar 7's own downbeat -- the cadence gap start.
            Note(7 * BarTicks + 25_000_000L, 12 * BarTicks), // resumes well inside bar 8; runs to the song's own end (no trailing cadence noise).
        ]);

        return (fingerprintSet, labelAnalysis, melodyTrack, verse.Id, chorus.Id);
    }

    [Fact]
    public async Task Step055_Acceptance_Passes_PhraseBoundariesAlignToBarsOrUserChosenEvents()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, labelAnalysis, melodyTrack, verseId, chorusId) = BuildVerseChorusFixture(songId, sourceHash);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, melodyTrack, melodyTrack.SourceAssetHash);

        var cacheDir = Path.Combine(_tempDir, "ps-cache");
        var overrideDir = Path.Combine(_tempDir, "ps-overrides");

        var segmenter = new PhraseSegmenter(new JsonPhraseSegmentationStore(cacheDir), new JsonPhraseBoundaryOverrideStore(overrideDir));
        var analysis = await segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None);

        // "Phrase boundaries align to bars" -- every phrase is an exact, non-overlapping bar range, and the
        // irregular-length Verse plus the bar-hypothesis/melodic-cadence-split Chorus are both present.
        Assert.Equal(3, analysis.Phrases.Count);

        Assert.Equal(verseId, analysis.Phrases[0].SectionLabelId);
        Assert.Equal((0, 4), (analysis.Phrases[0].StartBarIndex, analysis.Phrases[0].EndBarIndexExclusive));
        Assert.Equal(4, analysis.Phrases[0].Bars);
        Assert.Equal(PhraseBoundarySource.BarHypothesis, analysis.Phrases[0].Source);
        Assert.Contains("irregular-length", analysis.Phrases[0].Evidence[0]);

        Assert.Equal(chorusId, analysis.Phrases[1].SectionLabelId);
        Assert.Equal((4, 7), (analysis.Phrases[1].StartBarIndex, analysis.Phrases[1].EndBarIndexExclusive));
        Assert.Equal(PhraseBoundarySource.BarHypothesis, analysis.Phrases[1].Source);

        Assert.Equal(chorusId, analysis.Phrases[2].SectionLabelId);
        Assert.Equal((7, 12), (analysis.Phrases[2].StartBarIndex, analysis.Phrases[2].EndBarIndexExclusive));
        Assert.Equal(PhraseBoundarySource.MelodicCadence, analysis.Phrases[2].Source);
        Assert.Contains("melodic cadence", analysis.Phrases[2].Evidence[0], StringComparison.OrdinalIgnoreCase);

        // Never overlap, never gap: every phrase's own end is the next phrase's own start (within a
        // section) or the section's own already-approved end.
        Assert.All(analysis.Phrases, p => Assert.InRange(p.Confidence, 0.0, 1.0));
        Assert.All(analysis.Phrases, p => Assert.NotEmpty(p.Evidence));
        Assert.All(analysis.Phrases, p => Assert.NotEmpty(p.SignatureHash));
        Assert.All(analysis.Phrases, p => Assert.False(p.UserCorrected));

        // Provenance (Section 4).
        Assert.Equal(sourceHash, analysis.SourceHash);
        Assert.Equal(labelAnalysis.VersionId, analysis.SourceSectionLabelVersionId);
        Assert.Equal(fingerprintSet.VersionId, analysis.SourceFingerprintSetVersionId);
        Assert.Equal(melodyTrack.Id, analysis.SourceMelodyTrackId);
        Assert.Equal(PhraseSegmenter.Version, analysis.AnalyzerVersion);
        Assert.True(File.Exists(Path.Combine(cacheDir, $"phrase-segmentation-{songId.Value:N}.json")));

        // "Phrase boundaries align to ... user-chosen events": a human replaces the Chorus's own internal
        // split with bar 9 instead of the automatically-proposed bar 7.
        var afterOverride = await segmenter.SetManualBoundariesAsync(context, DefaultSettings, chorusId, [9], CancellationToken.None);
        var chorusPhrases = afterOverride.Phrases.Where(p => p.SectionLabelId == chorusId).OrderBy(p => p.StartBarIndex).ToList();
        Assert.Equal(2, chorusPhrases.Count);
        Assert.Equal((4, 9), (chorusPhrases[0].StartBarIndex, chorusPhrases[0].EndBarIndexExclusive));
        Assert.Equal((9, 12), (chorusPhrases[1].StartBarIndex, chorusPhrases[1].EndBarIndexExclusive));
        Assert.Equal(PhraseBoundarySource.UserChosen, chorusPhrases[1].Source);
        Assert.All(chorusPhrases, p => Assert.True(p.UserCorrected));
        // The never-overridden Verse section is unaffected.
        Assert.Contains(afterOverride.Phrases, p => p.SectionLabelId == verseId && !p.UserCorrected);
        Assert.True(File.Exists(Path.Combine(overrideDir, $"phrase-boundary-overrides-{songId.Value:N}.json")));

        // Overrides survive restart: brand-new store/segmenter instances over the same on-disk directories.
        var reopened = new PhraseSegmenter(new JsonPhraseSegmentationStore(cacheDir), new JsonPhraseBoundaryOverrideStore(overrideDir));
        var afterRestart = await reopened.SegmentAsync(context, DefaultSettings, CancellationToken.None);
        var reopenedChorusPhrases = afterRestart.Phrases.Where(p => p.SectionLabelId == chorusId).OrderBy(p => p.StartBarIndex).ToList();
        Assert.Equal((9, 12), (reopenedChorusPhrases[1].StartBarIndex, reopenedChorusPhrases[1].EndBarIndexExclusive));
        Assert.True(reopenedChorusPhrases[1].UserCorrected);
    }

    [Fact]
    public async Task SegmentAsync_NoMelodyTrack_FallsBackToBarHypothesis()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 8);
        var chorus = SectionLabel(0, 8, SectionKind.Chorus);
        var labelAnalysis = BuildLabelAnalysis(songId, sourceHash, fingerprintSet.VersionId, [chorus]);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, MelodyTrack: null);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var analysis = await segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None);

        Assert.Equal(2, analysis.Phrases.Count);
        Assert.Equal((0, 4), (analysis.Phrases[0].StartBarIndex, analysis.Phrases[0].EndBarIndexExclusive));
        Assert.Equal((4, 8), (analysis.Phrases[1].StartBarIndex, analysis.Phrases[1].EndBarIndexExclusive));
        Assert.Equal(PhraseBoundarySource.BarHypothesis, analysis.Phrases[1].Source);
        Assert.Contains("No melodic cadence found", analysis.Phrases[1].Evidence[0]);
        Assert.Null(analysis.SourceMelodyTrackId);
    }

    [Fact]
    public async Task SegmentAsync_EmptyLabels_ReturnsEmptyPhrases_HandlesSilenceExplicitly()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 0);
        var labelAnalysis = BuildLabelAnalysis(songId, sourceHash, fingerprintSet.VersionId, []);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, MelodyTrack: null);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var analysis = await segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None);

        Assert.Empty(analysis.Phrases);
    }

    [Fact]
    public async Task SegmentAsync_MalformedSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 4);
        // Malformed/corrupt upstream label: EndBarIndexExclusive <= StartBarIndex.
        var malformed = SectionLabel(2, 2, SectionKind.Verse);
        var labelAnalysis = BuildLabelAnalysis(songId, sourceHash, fingerprintSet.VersionId, [malformed]);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, MelodyTrack: null);

        var cacheDir = Path.Combine(_tempDir, "ps-cache");
        var segmenter = new PhraseSegmenter(new JsonPhraseSegmentationStore(cacheDir), new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("malformed proposal", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(cacheDir, $"phrase-segmentation-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task SetManualBoundariesAsync_UnknownSectionLabelId_ThrowsPhraseSegmentationInvalidException_NeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, labelAnalysis, melodyTrack, _, _) = BuildVerseChorusFixture(songId, sourceHash);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, melodyTrack, melodyTrack.SourceAssetHash);

        var overrideDir = Path.Combine(_tempDir, "ps-overrides");
        var segmenter = new PhraseSegmenter(new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")), new JsonPhraseBoundaryOverrideStore(overrideDir));

        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SetManualBoundariesAsync(context, DefaultSettings, Guid.NewGuid(), [6], CancellationToken.None));
        Assert.Contains("does not reference any section", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(overrideDir, $"phrase-boundary-overrides-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task SetManualBoundariesAsync_BoundaryOutsideSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, labelAnalysis, melodyTrack, _, chorusId) = BuildVerseChorusFixture(songId, sourceHash);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, melodyTrack, melodyTrack.SourceAssetHash);

        var overrideDir = Path.Combine(_tempDir, "ps-overrides");
        var segmenter = new PhraseSegmenter(new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")), new JsonPhraseBoundaryOverrideStore(overrideDir));

        // Chorus spans [4,12); bar 2 lies in the Verse section, not Chorus.
        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SetManualBoundariesAsync(context, DefaultSettings, chorusId, [2], CancellationToken.None));
        Assert.Contains("outside section", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(overrideDir, $"phrase-boundary-overrides-{songId.Value:N}.json")));
    }

    [Fact]
    public async Task SegmentAsync_EmptySourceHash_ThrowsPhraseSegmentationInvalidException_MissingMediaNegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var fingerprintSet = BuildFingerprintSet(songId, "", barCount: 4);
        var labelAnalysis = BuildLabelAnalysis(songId, "", fingerprintSet.VersionId, [SectionLabel(0, 4, SectionKind.Verse)]);
        var context = new PhraseSegmentationContext(songId, "", labelAnalysis, fingerprintSet, MelodyTrack: null);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("missing media", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SegmentAsync_MismatchedProvenanceVersionIds_ThrowsPhraseSegmentationInvalidException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 4);
        // Label analysis claims a different (stale) fingerprint set version.
        var labelAnalysis = new SectionLabelAnalysis(
            Guid.NewGuid(), songId, sourceHash, Guid.NewGuid(), Guid.NewGuid(),
            "test-fixture/1.0.0", new SectionLabelSettings(0.95, 0.4, "test-fixture-recipe/1.0.0"),
            [SectionLabel(0, 4, SectionKind.Verse)]);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, MelodyTrack: null);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("SourceFingerprintSetVersionId", ex.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SegmentAsync_InvalidTargetPhraseLengthBars_ThrowsPhraseSegmentationInvalidException(int targetPhraseLengthBars)
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 4);
        var labelAnalysis = BuildLabelAnalysis(songId, sourceHash, fingerprintSet.VersionId, [SectionLabel(0, 4, SectionKind.Verse)]);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, MelodyTrack: null);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));
        var badSettings = DefaultSettings with { TargetPhraseLengthBars = targetPhraseLengthBars };

        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SegmentAsync(context, badSettings, CancellationToken.None));
        Assert.Contains("TargetPhraseLengthBars", ex.Reason);
    }

    /// <summary>
    /// PHASE-03 (Analysis extensions) Task 4: a real <see cref="MelodyTrack"/> derived from an isolated
    /// lead *stem* -- whose own hash is legitimately never the song's full-mix <c>SourceHash</c> -- must
    /// still enable melodic-cadence snapping when its real hash is threaded through as
    /// <see cref="PhraseSegmentationContext.MelodySourceAssetHash"/>, instead of silently falling back to
    /// bar-hypothesis-only phrasing the way the pre-fix "exact match against SourceHash" check always
    /// forced for a stem-derived melody.
    /// </summary>
    [Fact]
    public async Task SegmentAsync_MelodyHashDiffersFromSongHash_StillSnapsToMelodicCadence_WhenMelodySourceAssetHashMatches()
    {
        var songId = new SongId(Guid.NewGuid());
        const string songSourceHash = "song-full-mix-hash";
        const string leadStemHash = "lead-stem-hash-never-equal-to-song-hash";

        var fingerprintSet = BuildFingerprintSet(songId, songSourceHash, barCount: 12);
        var verse = SectionLabel(0, 4, SectionKind.Verse);
        var chorus = SectionLabel(4, 12, SectionKind.Chorus);
        var labelAnalysis = BuildLabelAnalysis(songId, songSourceHash, fingerprintSet.VersionId, [verse, chorus]);

        var melodyTrack = BuildMelodyTrack(leadStemHash,
        [
            Note(0, 7 * BarTicks),
            Note((7 * BarTicks) + 25_000_000L, 12 * BarTicks),
        ]);

        var context = new PhraseSegmentationContext(songId, songSourceHash, labelAnalysis, fingerprintSet, melodyTrack, leadStemHash);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var analysis = await segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None);

        var chorusSplit = analysis.Phrases.Single(p => p.SectionLabelId == chorus.Id && p.StartBarIndex == 7);
        Assert.Equal(PhraseBoundarySource.MelodicCadence, chorusSplit.Source);
        Assert.Equal(melodyTrack.Id, analysis.SourceMelodyTrackId);
    }

    [Fact]
    public async Task SegmentAsync_MelodyTrackSupplied_WithoutMelodySourceAssetHash_ThrowsPhraseSegmentationInvalidException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, labelAnalysis, melodyTrack, _, _) = BuildVerseChorusFixture(songId, sourceHash);
        // Deliberately omit MelodySourceAssetHash -- malformed provenance, must not be silently accepted.
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, melodyTrack);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("MelodySourceAssetHash", ex.Reason);
    }

    [Fact]
    public async Task SegmentAsync_MelodyTrackHash_DoesNotMatchMelodySourceAssetHash_ThrowsPhraseSegmentationInvalidException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var (fingerprintSet, labelAnalysis, melodyTrack, _, _) = BuildVerseChorusFixture(songId, sourceHash);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, melodyTrack, "a-completely-different-hash");

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        var ex = await Assert.ThrowsAsync<PhraseSegmentationInvalidException>(
            () => segmenter.SegmentAsync(context, DefaultSettings, CancellationToken.None));
        Assert.Contains("stale or mismatched", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SegmentAsync_NullArguments_ThrowArgumentNullException()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 4);
        var labelAnalysis = BuildLabelAnalysis(songId, sourceHash, fingerprintSet.VersionId, [SectionLabel(0, 4, SectionKind.Verse)]);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, MelodyTrack: null);

        var segmenter = new PhraseSegmenter(
            new JsonPhraseSegmentationStore(Path.Combine(_tempDir, "ps-cache")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => segmenter.SegmentAsync(null!, DefaultSettings, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => segmenter.SegmentAsync(context, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => segmenter.SetManualBoundariesAsync(context, DefaultSettings, Guid.NewGuid(), null!, CancellationToken.None));
    }

    [Fact]
    public async Task SegmentAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted()
    {
        var songId = new SongId(Guid.NewGuid());
        const string sourceHash = "deadbeef";
        var fingerprintSet = BuildFingerprintSet(songId, sourceHash, barCount: 4);
        var labelAnalysis = BuildLabelAnalysis(songId, sourceHash, fingerprintSet.VersionId, [SectionLabel(0, 4, SectionKind.Verse)]);
        var context = new PhraseSegmentationContext(songId, sourceHash, labelAnalysis, fingerprintSet, MelodyTrack: null);

        var cacheDir = Path.Combine(_tempDir, "ps-cache");
        var segmenter = new PhraseSegmenter(new JsonPhraseSegmentationStore(cacheDir), new JsonPhraseBoundaryOverrideStore(Path.Combine(_tempDir, "ps-overrides")));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => segmenter.SegmentAsync(context, DefaultSettings, cts.Token));
        Assert.False(File.Exists(Path.Combine(cacheDir, $"phrase-segmentation-{songId.Value:N}.json")));
    }
}
