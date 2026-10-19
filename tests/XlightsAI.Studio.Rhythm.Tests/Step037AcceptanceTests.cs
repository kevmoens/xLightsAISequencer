using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-05 / Step 037 — "Add confidence/quality gate and diagnostics." Required acceptance:
/// "Low-confidence results are flagged and correction is possible." Failure/edge case: "Malformed
/// proposal, no-improvement and exhausted retry budget must not auto-commit." Implementation guidance:
/// "Add timing deviation histograms, first-beat offset and confidence; false positives must show
/// warning, not trigger a blind whole-song export."
/// </summary>
public sealed class Step037AcceptanceTests
{
    private const long TicksPerSecond = 10_000_000L;
    private static readonly BeatGridSettings GridSettings = new(BeatsPerBar: 4, AlignmentToleranceTicks: 10_000);
    private static readonly TempoHypothesis Tempo120 = new(Bpm: 120.0, Confidence: 1.0, MeterGuess: "4/4");
    private static readonly long PeriodTicksAt120Bpm = (long)Math.Round(60.0 / Tempo120.Bpm * TicksPerSecond); // 5,000,000

    private readonly BeatGridQualityGate _gate = new();
    private readonly BeatGridEditor _editor = new();

    private static BeatGrid MakeGrid(SongId songId, TempoHypothesis? tempo, params BeatMarker[] beats) =>
        new(
            VersionId: Guid.NewGuid(),
            SongId: songId,
            SourceHash: "deadbeef",
            SourceOnsetAnalysisVersionId: Guid.NewGuid(),
            SourceTempoAnalysisVersionId: Guid.NewGuid(),
            AnalyzerVersion: "test/1.0.0",
            Settings: GridSettings,
            SelectedTempo: tempo,
            Beats: beats,
            Candidates: tempo is null ? [] : [tempo],
            CorrectionVersion: 0);

    private static BeatMarker MakeBeat(long timeTicks, int index, double confidence, bool userCorrected = false) =>
        new(Id: Guid.NewGuid(), TimeTicks: timeTicks, BeatWithinBar: index % GridSettings.BeatsPerBar,
            BarIndex: index / GridSettings.BeatsPerBar, Confidence: confidence, UserCorrected: userCorrected);

    [Fact]
    public void Step037_Acceptance_Passes_LowConfidenceFlaggedAndCorrectionIsPossible()
    {
        var songId = new SongId(Guid.NewGuid());
        var b0 = MakeBeat(timeTicks: 1_000_000, index: 0, confidence: 0.2);
        var b1 = MakeBeat(timeTicks: 1_000_000 + PeriodTicksAt120Bpm, index: 1, confidence: 0.9);
        var grid = MakeGrid(songId, Tempo120, b0, b1);
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.6, DeviationHistogramBucketTicks: 100_000);

        // "Add... first-beat offset and confidence" + "Low-confidence results are flagged."
        var before = _gate.Evaluate(grid, settings);
        Assert.Equal(2, before.BeatCount);
        Assert.Equal(1_000_000L, before.FirstBeatOffsetTicks);
        Assert.Equal(0.55, before.OverallConfidence, 3);
        Assert.True(before.IsLowConfidence);
        Assert.NotEmpty(before.Warnings);

        // "Add timing deviation histograms": beat 1 lands exactly on its predicted position (deviation 0).
        var bucket = Assert.Single(before.TimingDeviationHistogram);
        Assert.Equal(0L, bucket.BucketStartTicks);
        Assert.Equal(100_000L, bucket.BucketEndTicksExclusive);
        Assert.Equal(1, bucket.Count);

        // "...and correction is possible": confirming the low-confidence beat (even without moving it)
        // through the already-existing, already-tested IBeatGridEditor raises it to full confidence.
        var loop = new BeatGridAutoCorrectionLoop(_editor, _gate);
        var proposals = new[] { new BeatGridCorrectionProposal(BeatGridCorrectionProposalKind.MoveBeat, b0.Id, b0.TimeTicks) };
        var outcome = loop.TryApply(grid, proposals, settings, maxAttempts: 3);

        Assert.True(outcome.Committed);
        Assert.Null(outcome.RejectionReason);
        Assert.NotNull(outcome.ReportAfter);
        Assert.Equal(0.95, outcome.ReportAfter!.OverallConfidence, 3);
        Assert.False(outcome.ReportAfter.IsLowConfidence);
        var correctedBeat = Assert.Single(outcome.Grid.Beats, beat => beat.Id == b0.Id);
        Assert.True(correctedBeat.UserCorrected);
        Assert.Equal(BeatGridEditor.UserCorrectedConfidence, correctedBeat.Confidence);
    }

    [Fact]
    public void Evaluate_EmptyGrid_FlagsLowConfidence_SilenceEdgeCase()
    {
        var songId = new SongId(Guid.NewGuid());
        var grid = MakeGrid(songId, tempo: null);
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.5, DeviationHistogramBucketTicks: 1_000);

        var report = _gate.Evaluate(grid, settings);

        Assert.Equal(0, report.BeatCount);
        Assert.Null(report.FirstBeatOffsetTicks);
        Assert.Equal(0.0, report.OverallConfidence);
        Assert.True(report.IsLowConfidence);
        Assert.NotEmpty(report.Warnings);
        Assert.Empty(report.TimingDeviationHistogram);
    }

    [Fact]
    public void Evaluate_MultipleDeviations_BucketsHistogramCorrectly()
    {
        var songId = new SongId(Guid.NewGuid());
        var period = PeriodTicksAt120Bpm; // 5,000,000
        var b0 = MakeBeat(0, 0, confidence: 1.0);
        var b1 = MakeBeat(period + 50_000, 1, confidence: 1.0); // deviation 50,000
        var b2 = MakeBeat((2 * period) - 100_000, 2, confidence: 1.0); // deviation 100,000
        var b3 = MakeBeat(3 * period, 3, confidence: 1.0); // deviation 0
        var grid = MakeGrid(songId, Tempo120, b0, b1, b2, b3);
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.5, DeviationHistogramBucketTicks: 50_000);

        var report = _gate.Evaluate(grid, settings);

        Assert.Equal(3, report.TimingDeviationHistogram.Count);
        Assert.Equal(1, report.TimingDeviationHistogram.Single(h => h.BucketStartTicks == 0).Count);
        Assert.Equal(1, report.TimingDeviationHistogram.Single(h => h.BucketStartTicks == 50_000).Count);
        Assert.Equal(1, report.TimingDeviationHistogram.Single(h => h.BucketStartTicks == 100_000).Count);
    }

    [Fact]
    public void Evaluate_NullGrid_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gate.Evaluate(null!, new BeatGridQualityGateSettings(0.5, 1_000)));
    }

    [Fact]
    public void Evaluate_InvalidThreshold_ThrowsInvalid_NegativePath()
    {
        var grid = MakeGrid(new SongId(Guid.NewGuid()), tempo: null);
        Assert.Throws<BeatGridQualityGateInvalidException>(
            () => _gate.Evaluate(grid, new BeatGridQualityGateSettings(LowConfidenceThreshold: 1.5, DeviationHistogramBucketTicks: 1_000)));
        Assert.Throws<BeatGridQualityGateInvalidException>(
            () => _gate.Evaluate(grid, new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.5, DeviationHistogramBucketTicks: 0)));
    }

    [Fact]
    public void TryApply_MalformedProposal_UnknownBeatId_DoesNotAutoCommit_NegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var b0 = MakeBeat(1_000_000, 0, confidence: 0.2);
        var grid = MakeGrid(songId, Tempo120, b0);
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var loop = new BeatGridAutoCorrectionLoop(_editor, _gate);

        var proposals = new[] { new BeatGridCorrectionProposal(BeatGridCorrectionProposalKind.MoveBeat, Guid.NewGuid(), 123) };
        var outcome = loop.TryApply(grid, proposals, settings, maxAttempts: 1);

        Assert.False(outcome.Committed);
        Assert.Null(outcome.ReportAfter);
        Assert.Equal(grid, outcome.Grid);
        Assert.Contains("malformed", outcome.RejectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, outcome.AttemptsUsed);
    }

    [Fact]
    public void TryApply_ProposalDoesNotImprove_DoesNotAutoCommit_NegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        // Already fully confident/user-corrected — no proposal can raise confidence further.
        var b0 = MakeBeat(0, 0, confidence: 1.0, userCorrected: true);
        var b1 = MakeBeat(PeriodTicksAt120Bpm, 1, confidence: 1.0, userCorrected: true);
        var grid = MakeGrid(songId, Tempo120, b0, b1);
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.5, DeviationHistogramBucketTicks: 1_000);
        var loop = new BeatGridAutoCorrectionLoop(_editor, _gate);

        var proposals = new[] { new BeatGridCorrectionProposal(BeatGridCorrectionProposalKind.SetDownbeat, b0.Id) };
        var outcome = loop.TryApply(grid, proposals, settings, maxAttempts: 1);

        Assert.False(outcome.Committed);
        Assert.Null(outcome.ReportAfter);
        Assert.Equal(grid, outcome.Grid);
        Assert.Contains("did not improve", outcome.RejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryApply_RetryBudgetExhausted_StopsBeforeTryingEveryProposal_DoesNotAutoCommit_NegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var b0 = MakeBeat(0, 0, confidence: 1.0, userCorrected: true); // already confirmed, no-improvement candidate
        var b1 = MakeBeat(PeriodTicksAt120Bpm, 1, confidence: 0.3); // low confidence — an improving candidate exists
        var grid = MakeGrid(songId, Tempo120, b0, b1);
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var loop = new BeatGridAutoCorrectionLoop(_editor, _gate);

        var proposals = new[]
        {
            new BeatGridCorrectionProposal(BeatGridCorrectionProposalKind.SetDownbeat, b0.Id), // no-improvement
            new BeatGridCorrectionProposal(BeatGridCorrectionProposalKind.SetDownbeat, b0.Id), // no-improvement again
            new BeatGridCorrectionProposal(BeatGridCorrectionProposalKind.MoveBeat, b1.Id, b1.TimeTicks), // would improve
        };

        // Budget only allows 2 of the 3 proposals — the genuinely improving third proposal is never reached.
        var outcome = loop.TryApply(grid, proposals, settings, maxAttempts: 2);

        Assert.False(outcome.Committed);
        Assert.Equal(2, outcome.AttemptsUsed);
        Assert.Equal(grid, outcome.Grid);
        Assert.Contains("retry budget exhausted", outcome.RejectionReason, StringComparison.OrdinalIgnoreCase);

        // Proof the third proposal really would have improved things, had the budget allowed it —
        // the rejection above was a real, enforced budget limit, not a fabricated "nothing would help" claim.
        var withBiggerBudget = loop.TryApply(grid, proposals, settings, maxAttempts: 3);
        Assert.True(withBiggerBudget.Committed);
    }

    [Fact]
    public void TryApply_NoProposalsSupplied_DoesNotAutoCommit_NegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var grid = MakeGrid(songId, Tempo120, MakeBeat(0, 0, confidence: 0.1));
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var loop = new BeatGridAutoCorrectionLoop(_editor, _gate);

        var outcome = loop.TryApply(grid, [], settings, maxAttempts: 3);

        Assert.False(outcome.Committed);
        Assert.Equal(0, outcome.AttemptsUsed);
        Assert.Equal(grid, outcome.Grid);
    }

    [Fact]
    public void TryApply_NonPositiveMaxAttempts_ThrowsInvalid_NegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var grid = MakeGrid(songId, Tempo120, MakeBeat(0, 0, confidence: 0.1));
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var loop = new BeatGridAutoCorrectionLoop(_editor, _gate);

        Assert.Throws<BeatGridCorrectionInvalidException>(() => loop.TryApply(grid, [], settings, maxAttempts: 0));
    }

    [Fact]
    public void TryApply_NullArguments_ThrowArgumentNullException()
    {
        var songId = new SongId(Guid.NewGuid());
        var grid = MakeGrid(songId, Tempo120, MakeBeat(0, 0, confidence: 0.1));
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var loop = new BeatGridAutoCorrectionLoop(_editor, _gate);

        Assert.Throws<ArgumentNullException>(() => loop.TryApply(null!, [], settings, 1));
        Assert.Throws<ArgumentNullException>(() => loop.TryApply(grid, null!, settings, 1));
        Assert.Throws<ArgumentNullException>(() => loop.TryApply(grid, [], null!, 1));
        Assert.Throws<ArgumentNullException>(() => new BeatGridAutoCorrectionLoop(null!, _gate));
        Assert.Throws<ArgumentNullException>(() => new BeatGridAutoCorrectionLoop(_editor, null!));
    }

    [Fact]
    public void ExportGate_BlocksBlindWholeSongExport_WhenLowConfidenceAndNotAcknowledged_NegativePath()
    {
        var songId = new SongId(Guid.NewGuid());
        var grid = MakeGrid(songId, Tempo120, MakeBeat(0, 0, confidence: 0.1));
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var report = _gate.Evaluate(grid, settings);
        Assert.True(report.IsLowConfidence);

        var fullSong = new TimeWindow(0, 10_000_000);
        var exportGate = new RhythmExportGate();

        var ex = Assert.Throws<RhythmExportBlockedException>(
            () => exportGate.EnsureExportAllowed(songId, report, fullSong, fullSong, lowConfidenceAcknowledged: false));
        Assert.NotEmpty(ex.Warnings);
    }

    [Fact]
    public void ExportGate_AllowsWholeSongExport_WhenAcknowledged()
    {
        var songId = new SongId(Guid.NewGuid());
        var grid = MakeGrid(songId, Tempo120, MakeBeat(0, 0, confidence: 0.1));
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var report = _gate.Evaluate(grid, settings);

        var fullSong = new TimeWindow(0, 10_000_000);
        var exportGate = new RhythmExportGate();

        exportGate.EnsureExportAllowed(songId, report, fullSong, fullSong, lowConfidenceAcknowledged: true);
    }

    [Fact]
    public void ExportGate_AllowsNarrowerPartialRangeExport_EvenWhenLowConfidence()
    {
        var songId = new SongId(Guid.NewGuid());
        var grid = MakeGrid(songId, Tempo120, MakeBeat(0, 0, confidence: 0.1));
        var settings = new BeatGridQualityGateSettings(LowConfidenceThreshold: 0.9, DeviationHistogramBucketTicks: 1_000);
        var report = _gate.Evaluate(grid, settings);

        var fullSong = new TimeWindow(0, 10_000_000);
        var partial = new TimeWindow(0, 1_000_000);
        var exportGate = new RhythmExportGate();

        exportGate.EnsureExportAllowed(songId, report, partial, fullSong, lowConfidenceAcknowledged: false);
    }

    [Fact]
    public void ExportGate_NullReport_ThrowsArgumentNullException()
    {
        var exportGate = new RhythmExportGate();
        var window = new TimeWindow(0, 1);
        Assert.Throws<ArgumentNullException>(
            () => exportGate.EnsureExportAllowed(new SongId(Guid.NewGuid()), null!, window, window, false));
    }
}
