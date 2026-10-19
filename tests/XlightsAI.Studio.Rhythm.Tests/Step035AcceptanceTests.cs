using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-05 / Step 035 — "Add beat-grid manual correction and dependency invalidation." Required
/// acceptance: "Move beat, dependent effects refresh only when authorized." Failure/edge case:
/// "Silence, invalid timing boundaries and missing media must be handled explicitly." Implementation
/// guidance: "Allow drag, anchored beat insertion/removal and downbeat reassignment; recalibrate
/// dependent gestures through source-provenance references."
/// </summary>
public sealed class Step035AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly OnsetDetectionSettings OnsetSettings = new(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: 256);
    private static readonly TempoDetectionSettings TempoSettings = new(MinBpm: 40, MaxBpm: 300, TimeResolutionSeconds: 0.01, MaxCandidates: 5);
    private static readonly long AlignmentToleranceTicks = 2L * WindowSettings.HopSamples * (10_000_000L / SampleRate);
    private static readonly BeatGridSettings GridSettings = new(BeatsPerBar: 4, AlignmentToleranceTicks: AlignmentToleranceTicks);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step035-{Guid.NewGuid():N}");
    private readonly BeatGridEditor _editor = new();

    public Step035AcceptanceTests()
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

    private async Task<SongAsset> ProbeAsync(string fileName, float[] samples)
    {
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, SampleRate);
        var path = Path.Combine(_tempDir, fileName);
        await File.WriteAllBytesAsync(path, wavBytes);
        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    private async Task<BeatGrid> BuildClickGridAsync(string fileName, int bpm, int clickCount, int leadInSamples = 4000)
    {
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var samples = new float[leadInSamples + clicks.Length];
        Array.Copy(clicks, 0, samples, leadInSamples, clicks.Length);

        var asset = await ProbeAsync(fileName, samples);
        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsets = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
        var tempoStore = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var tempo = await new TempoDetector(tempoStore).DetectAsync(onsets, TempoSettings, CancellationToken.None);

        var gridStore = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(gridStore);
        return await builder.BuildAsync(onsets, tempo, GridSettings, CancellationToken.None);
    }

    [Fact]
    public async Task Step035_Acceptance_Passes_MoveBeatRefreshesDependentsOnlyWhenAuthorized()
    {
        var grid = await BuildClickGridAsync("move.wav", bpm: 120, clickCount: 8);
        Assert.True(grid.Beats.Length >= 4);
        var target = grid.Beats[1];
        var originalOthers = grid.Beats.Where(b => b.Id != target.Id).ToArray();

        var newTimeTicks = target.TimeTicks + 1234;
        var moved = _editor.MoveBeat(grid, target.Id, newTimeTicks, grid.CorrectionVersion);

        // Required acceptance, verbatim: "Move beat..." — the targeted beat actually moved and is now
        // explicitly flagged as a human decision.
        var movedBeat = Assert.Single(moved.Beats, b => b.Id == target.Id);
        Assert.Equal(newTimeTicks, movedBeat.TimeTicks);
        Assert.True(movedBeat.UserCorrected);
        Assert.Equal(BeatGridEditor.UserCorrectedConfidence, movedBeat.Confidence);

        // "...dependent effects refresh only when authorized": the explicit, authorized edit bumps the
        // grid-level correction/version identity (the signal a future downstream consumer keys off of)...
        Assert.Equal(grid.CorrectionVersion + 1, moved.CorrectionVersion);
        Assert.NotEqual(grid.VersionId, moved.VersionId);

        // ...but does NOT retroactively alter any other, already-generated beat's own time or
        // confidence, and does NOT claim a re-analysis happened — provenance back to the untouched
        // source onset/tempo analysis is preserved exactly, since no re-analysis occurred.
        foreach (var other in originalOthers)
        {
            var stillThere = Assert.Single(moved.Beats, b => b.Id == other.Id);
            Assert.Equal(other.TimeTicks, stillThere.TimeTicks);
            Assert.Equal(other.Confidence, stillThere.Confidence);
            Assert.Equal(other.UserCorrected, stillThere.UserCorrected);
        }

        Assert.Equal(grid.SourceHash, moved.SourceHash);
        Assert.Equal(grid.SourceOnsetAnalysisVersionId, moved.SourceOnsetAnalysisVersionId);
        Assert.Equal(grid.SourceTempoAnalysisVersionId, moved.SourceTempoAnalysisVersionId);

        // Beats remain strictly time-ordered and contiguously renumbered after the move.
        for (var i = 0; i < moved.Beats.Length; i++)
        {
            Assert.Equal(i % GridSettings.BeatsPerBar, moved.Beats[i].BeatWithinBar);
            Assert.Equal(i / GridSettings.BeatsPerBar, moved.Beats[i].BarIndex);
            if (i > 0)
            {
                Assert.True(moved.Beats[i].TimeTicks > moved.Beats[i - 1].TimeTicks);
            }
        }
    }

    [Fact]
    public async Task MoveBeat_StaleExpectedCorrectionVersion_ThrowsWithoutMutating_UnauthorizedRefreshNegativePath()
    {
        var grid = await BuildClickGridAsync("conflict.wav", bpm: 120, clickCount: 6);
        var target = grid.Beats[0];

        var ex = Assert.Throws<BeatGridEditConflictException>(
            () => _editor.MoveBeat(grid, target.Id, target.TimeTicks + 1, expectedCorrectionVersion: grid.CorrectionVersion + 1));

        Assert.Equal(grid.CorrectionVersion, ex.ActualCorrectionVersion);
    }

    [Fact]
    public async Task MoveBeat_UnknownBeatId_ThrowsInvalid()
    {
        var grid = await BuildClickGridAsync("unknown.wav", bpm: 120, clickCount: 6);

        var ex = Assert.Throws<BeatGridEditInvalidException>(
            () => _editor.MoveBeat(grid, Guid.NewGuid(), 0, grid.CorrectionVersion));

        Assert.Contains("No beat with Id", ex.Reason);
    }

    [Fact]
    public async Task MoveBeat_NegativeTimeTicks_ThrowsInvalid_InvalidTimingBoundaryNegativePath()
    {
        var grid = await BuildClickGridAsync("negative-time.wav", bpm: 120, clickCount: 6);
        var target = grid.Beats[0];

        var ex = Assert.Throws<BeatGridEditInvalidException>(
            () => _editor.MoveBeat(grid, target.Id, -1, grid.CorrectionVersion));

        Assert.Contains("newTimeTicks", ex.Reason);
    }

    [Fact]
    public async Task SetDownbeat_ReassignsBarNumberingRelativeToChosenBeat()
    {
        var grid = await BuildClickGridAsync("downbeat.wav", bpm: 120, clickCount: 12);
        Assert.True(grid.Beats.Length >= 5, $"expected at least 5 beats, got {grid.Beats.Length}");

        // Pick the second beat (index 1 in time order) as the new downbeat.
        var anchor = grid.Beats.OrderBy(b => b.TimeTicks).ElementAt(1);
        var result = _editor.SetDownbeat(grid, anchor.Id, grid.CorrectionVersion);

        var ordered = result.Beats.OrderBy(b => b.TimeTicks).ToArray();
        var anchorIndex = Array.FindIndex(ordered, b => b.Id == anchor.Id);

        // The chosen beat is now a downbeat (BeatWithinBar == 0, BarIndex == 0) and is flagged as a
        // human decision; every other beat's bar numbering is consistent relative to it.
        Assert.Equal(0, ordered[anchorIndex].BeatWithinBar);
        Assert.Equal(0, ordered[anchorIndex].BarIndex);
        Assert.True(ordered[anchorIndex].UserCorrected);

        for (var i = 0; i < ordered.Length; i++)
        {
            var relative = i - anchorIndex;
            var beatsPerBar = GridSettings.BeatsPerBar;
            var expectedBeatWithinBar = ((relative % beatsPerBar) + beatsPerBar) % beatsPerBar;
            var expectedBarIndex = (relative - expectedBeatWithinBar) / beatsPerBar;
            Assert.Equal(expectedBeatWithinBar, ordered[i].BeatWithinBar);
            Assert.Equal(expectedBarIndex, ordered[i].BarIndex);
        }

        Assert.Equal(grid.CorrectionVersion + 1, result.CorrectionVersion);
    }

    [Fact]
    public async Task SetDownbeat_UnknownBeatId_ThrowsInvalid()
    {
        var grid = await BuildClickGridAsync("downbeat-unknown.wav", bpm: 120, clickCount: 6);

        Assert.Throws<BeatGridEditInvalidException>(
            () => _editor.SetDownbeat(grid, Guid.NewGuid(), grid.CorrectionVersion));
    }

    [Fact]
    public async Task InsertBeat_AnchoredInsertion_AddsFullyConfidentUserCorrectedBeatAndRenumbers()
    {
        var grid = await BuildClickGridAsync("insert.wav", bpm: 120, clickCount: 6);
        var first = grid.Beats.OrderBy(b => b.TimeTicks).First();
        var second = grid.Beats.OrderBy(b => b.TimeTicks).Skip(1).First();
        var midpoint = (first.TimeTicks + second.TimeTicks) / 2;

        var result = _editor.InsertBeat(grid, midpoint, grid.CorrectionVersion);

        Assert.Equal(grid.Beats.Length + 1, result.Beats.Length);
        var inserted = Assert.Single(result.Beats, b => b.TimeTicks == midpoint);
        Assert.True(inserted.UserCorrected);
        Assert.Equal(BeatGridEditor.UserCorrectedConfidence, inserted.Confidence);
        Assert.Equal(grid.CorrectionVersion + 1, result.CorrectionVersion);

        // Contiguous, position-based renumbering after insertion.
        var ordered = result.Beats.OrderBy(b => b.TimeTicks).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            Assert.Equal(i % GridSettings.BeatsPerBar, ordered[i].BeatWithinBar);
            Assert.Equal(i / GridSettings.BeatsPerBar, ordered[i].BarIndex);
        }
    }

    [Fact]
    public async Task InsertBeat_DuplicateTimeTicks_ThrowsInvalid()
    {
        var grid = await BuildClickGridAsync("insert-dup.wav", bpm: 120, clickCount: 6);
        var existing = grid.Beats[0];

        var ex = Assert.Throws<BeatGridEditInvalidException>(
            () => _editor.InsertBeat(grid, existing.TimeTicks, grid.CorrectionVersion));

        Assert.Contains("already exists", ex.Reason);
    }

    [Fact]
    public async Task InsertBeat_NegativeTimeTicks_ThrowsInvalid_InvalidTimingBoundaryNegativePath()
    {
        var grid = await BuildClickGridAsync("insert-negative.wav", bpm: 120, clickCount: 6);

        Assert.Throws<BeatGridEditInvalidException>(
            () => _editor.InsertBeat(grid, -5, grid.CorrectionVersion));
    }

    [Fact]
    public async Task RemoveBeat_AnchoredRemoval_DropsBeatAndRenumbersRemaining()
    {
        var grid = await BuildClickGridAsync("remove.wav", bpm: 120, clickCount: 8);
        var toRemove = grid.Beats.OrderBy(b => b.TimeTicks).ElementAt(1);

        var result = _editor.RemoveBeat(grid, toRemove.Id, grid.CorrectionVersion);

        Assert.Equal(grid.Beats.Length - 1, result.Beats.Length);
        Assert.DoesNotContain(result.Beats, b => b.Id == toRemove.Id);
        Assert.Equal(grid.CorrectionVersion + 1, result.CorrectionVersion);

        var ordered = result.Beats.OrderBy(b => b.TimeTicks).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            Assert.Equal(i % GridSettings.BeatsPerBar, ordered[i].BeatWithinBar);
            Assert.Equal(i / GridSettings.BeatsPerBar, ordered[i].BarIndex);
        }
    }

    [Fact]
    public async Task RemoveBeat_RemovingEveryBeat_LeavesEmptyGridWithoutThrowing_SilenceEdgeCase()
    {
        var grid = await BuildClickGridAsync("remove-all.wav", bpm: 120, clickCount: 4);
        var current = grid;

        while (current.Beats.Length > 0)
        {
            current = _editor.RemoveBeat(current, current.Beats[0].Id, current.CorrectionVersion);
        }

        Assert.Empty(current.Beats);
        Assert.Equal(grid.CorrectionVersion + grid.Beats.Length, current.CorrectionVersion);

        // An empty grid must still support further edits explicitly, not throw on re-entry.
        var inserted = _editor.InsertBeat(current, 1000, current.CorrectionVersion);
        Assert.Single(inserted.Beats);
    }

    [Fact]
    public async Task RemoveBeat_UnknownBeatId_ThrowsInvalid()
    {
        var grid = await BuildClickGridAsync("remove-unknown.wav", bpm: 120, clickCount: 6);

        Assert.Throws<BeatGridEditInvalidException>(
            () => _editor.RemoveBeat(grid, Guid.NewGuid(), grid.CorrectionVersion));
    }

    [Fact]
    public void AllEditors_NullGrid_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _editor.MoveBeat(null!, Guid.NewGuid(), 0, 0));
        Assert.Throws<ArgumentNullException>(() => _editor.SetDownbeat(null!, Guid.NewGuid(), 0));
        Assert.Throws<ArgumentNullException>(() => _editor.InsertBeat(null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => _editor.RemoveBeat(null!, Guid.NewGuid(), 0));
    }

    [Fact]
    public async Task BuildAsync_ReanalysisOverACorrectedGrid_ThrowsRequiresRebaseWithoutOverwriting_DependencyInvalidationNegativePath()
    {
        // Step 035's own "dependency invalidation": once a grid carries a real user correction, a
        // newer onset/tempo re-analysis (e.g. the user re-ran detection with different sensitivity)
        // must not silently discard the user's accepted work.
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var samples = new float[4000 + clicks.Length];
        Array.Copy(clicks, 0, samples, 4000, clicks.Length);
        var asset = await ProbeAsync("rebase.wav", samples);

        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsets1 = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
        var tempoStore = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var tempo1 = await new TempoDetector(tempoStore).DetectAsync(onsets1, TempoSettings, CancellationToken.None);

        var gridStore = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(gridStore);
        var grid1 = await builder.BuildAsync(onsets1, tempo1, GridSettings, CancellationToken.None);

        // Apply and persist a real user correction.
        var corrected = _editor.MoveBeat(grid1, grid1.Beats[0].Id, grid1.Beats[0].TimeTicks + 500, grid1.CorrectionVersion);
        Assert.True(corrected.CorrectionVersion > 0);
        await gridStore.SaveAsync(corrected, CancellationToken.None);

        // Independently rebuild onset/tempo analysis for the same song (simulating the user re-running
        // detection with different settings) — a new OnsetAnalysis/TempoAnalysis pair for the same SongId.
        var onsets2 = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings with { SensitivityMultiplier = 2.0f }, CancellationToken.None);
        var tempo2 = await new TempoDetector(tempoStore).DetectAsync(onsets2, TempoSettings, CancellationToken.None);
        Assert.NotEqual(onsets1.VersionId, onsets2.VersionId);

        var ex = await Assert.ThrowsAsync<BeatGridRequiresRebaseException>(
            () => builder.BuildAsync(onsets2, tempo2, GridSettings, CancellationToken.None));

        Assert.Equal(corrected.CorrectionVersion, ex.ExistingCorrectionVersion);

        // The persisted corrected grid is completely unmodified on disk — proof nothing was silently
        // overwritten.
        var reread = await gridStore.LoadAsync(gridStore.GetPath(onsets1.SongId), CancellationToken.None);
        Assert.Equal(corrected.VersionId, reread.VersionId);
        Assert.Equal(corrected.Beats.Length, reread.Beats.Length);
        Assert.Equal(corrected.CorrectionVersion, reread.CorrectionVersion);
    }

    [Fact]
    public async Task BuildAsync_ReanalysisOverAnUncorrectedGrid_StillRebuildsAsStep034Did_RegressionCheck()
    {
        // An uncorrected grid (CorrectionVersion == 0) must keep rebuilding exactly as Step 034
        // established — this step's own protection only applies once a real user correction exists.
        var (onsets1, tempo1, gridStore, builder) = await BuildWithTwoAnalysesSetupAsync("regression.wav");
        var grid1 = await builder.BuildAsync(onsets1, tempo1, GridSettings, CancellationToken.None);
        Assert.Equal(0, grid1.CorrectionVersion);

        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsets2 = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(await ReReadAssetAsync("regression.wav"), WindowSettings, OnsetSettings with { SensitivityMultiplier = 2.0f }, CancellationToken.None);
        var tempoStore = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var tempo2 = await new TempoDetector(tempoStore).DetectAsync(onsets2, TempoSettings, CancellationToken.None);

        var grid2 = await builder.BuildAsync(onsets2, tempo2, GridSettings, CancellationToken.None);
        Assert.NotEqual(grid1.VersionId, grid2.VersionId);
        Assert.Equal(onsets2.VersionId, grid2.SourceOnsetAnalysisVersionId);
    }

    private async Task<SongAsset> ReReadAssetAsync(string fileName) =>
        await new WavAudioDecoder().ProbeAsync(Path.Combine(_tempDir, fileName), CancellationToken.None);

    private async Task<(OnsetAnalysis Onsets, TempoAnalysis Tempo, JsonBeatGridStore Store, BeatGridBuilder Builder)> BuildWithTwoAnalysesSetupAsync(string fileName)
    {
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var samples = new float[4000 + clicks.Length];
        Array.Copy(clicks, 0, samples, 4000, clicks.Length);
        var asset = await ProbeAsync(fileName, samples);

        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsets = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
        var tempoStore = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var tempo = await new TempoDetector(tempoStore).DetectAsync(onsets, TempoSettings, CancellationToken.None);

        var gridStore = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(gridStore);
        return (onsets, tempo, gridStore, builder);
    }
}
