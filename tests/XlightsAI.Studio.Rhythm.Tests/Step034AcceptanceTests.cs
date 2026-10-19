using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-05 / Step 034 — "Generate beats, likely downbeats, bars and confidence intervals." Required
/// acceptance: "Tempo fixture aligns events within configured threshold." Failure/edge case: "Silence,
/// invalid timing boundaries and missing media must be handled explicitly." Implementation guidance:
/// "Represent beats with absolute ticks and bar indices; meter must be user correctable; show
/// confidence and avoid asserting uncertainty away" (window = 256 samples, hop = 128 samples at the
/// fixture's own 8 kHz rate for onset detection, 10 ms autocorrelation bins for tempo detection,
/// matching Steps 031-033).
/// </summary>
public sealed class Step034AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly OnsetDetectionSettings OnsetSettings = new(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: 256);
    private static readonly TempoDetectionSettings TempoSettings = new(MinBpm: 40, MaxBpm: 300, TimeResolutionSeconds: 0.01, MaxCandidates: 5);

    // A beat's predicted position is compared against an onset timestamp that is itself only accurate
    // to within one hop (Step 032's own established tolerance convention — see Step032AcceptanceTests's
    // "correct algorithmic group delay" assertions). Two independently hop-quantized timestamps can
    // therefore disagree by up to two hops even for a perfectly isochronous source.
    private static readonly long AlignmentToleranceTicks = 2L * WindowSettings.HopSamples * (10_000_000L / SampleRate);
    private static readonly BeatGridSettings GridSettings = new(BeatsPerBar: 4, AlignmentToleranceTicks: AlignmentToleranceTicks);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step034-{Guid.NewGuid():N}");

    public Step034AcceptanceTests()
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

    private async Task<OnsetAnalysis> BuildOnsetsAsync(SongAsset asset)
    {
        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsetDetector = new OnsetDetector(new WavAudioDecoder(), onsetStore);
        return await onsetDetector.DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
    }

    private static async Task<TempoAnalysis> BuildTempoAsync(OnsetAnalysis onsets, string tempoCacheDir)
    {
        var tempoStore = new JsonTempoAnalysisStore(tempoCacheDir);
        var tempoDetector = new TempoDetector(tempoStore);
        return await tempoDetector.DetectAsync(onsets, TempoSettings, CancellationToken.None);
    }

    private async Task<(OnsetAnalysis Onsets, TempoAnalysis Tempo)> BuildClickFixtureAsync(
        string fileName, int bpm, int clickCount, int leadInSamples = 4000)
    {
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var samples = new float[leadInSamples + clicks.Length];
        Array.Copy(clicks, 0, samples, leadInSamples, clicks.Length);

        var asset = await ProbeAsync(fileName, samples);
        var onsets = await BuildOnsetsAsync(asset);
        var tempo = await BuildTempoAsync(onsets, Path.Combine(_tempDir, "tempo-cache"));
        return (onsets, tempo);
    }

    [Fact]
    public async Task Step034_Acceptance_Passes_TempoFixtureAlignsEventsWithinConfiguredThreshold()
    {
        var (onsets, tempo) = await BuildClickFixtureAsync("click-120.wav", bpm: 120, clickCount: 12);
        Assert.NotEmpty(onsets.Onsets); // sanity: real onsets exist to align against.
        Assert.NotEmpty(tempo.Candidates); // sanity: a real tempo hypothesis exists to generate from.

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        var grid = await builder.BuildAsync(onsets, tempo, GridSettings, CancellationToken.None);

        Assert.True(File.Exists(store.GetPath(onsets.SongId)));
        Assert.NotEmpty(grid.Beats);
        Assert.NotNull(grid.SelectedTempo);
        Assert.Equal(tempo.Candidates[0], grid.SelectedTempo);

        // Required acceptance, verbatim: "Tempo fixture aligns events within configured threshold" —
        // every generated beat over a perfectly isochronous click fixture must land within
        // AlignmentToleranceTicks of a real detected onset.
        foreach (var beat in grid.Beats)
        {
            var nearestDistance = onsets.Onsets.Min(o => Math.Abs(o.TimeTicks - beat.TimeTicks));
            Assert.True(nearestDistance <= GridSettings.AlignmentToleranceTicks,
                $"beat at {beat.TimeTicks} ticks is {nearestDistance} ticks from the nearest onset, exceeding the {GridSettings.AlignmentToleranceTicks}-tick threshold.");
        }

        // "Represent beats with absolute ticks and bar indices": bars/beat-within-bar increment
        // correctly and every beat has an absolute (never bar-relative) TimeTicks.
        for (var i = 0; i < grid.Beats.Length; i++)
        {
            Assert.Equal(i % GridSettings.BeatsPerBar, grid.Beats[i].BeatWithinBar);
            Assert.Equal(i / GridSettings.BeatsPerBar, grid.Beats[i].BarIndex);
        }

        // "likely downbeats": every BeatsPerBar-th beat (BeatWithinBar == 0) is a likely downbeat,
        // and the very first beat always is one.
        Assert.Equal(0, grid.Beats[0].BeatWithinBar);

        // Beats are strictly ordered by absolute time.
        for (var i = 1; i < grid.Beats.Length; i++)
        {
            Assert.True(grid.Beats[i].TimeTicks > grid.Beats[i - 1].TimeTicks);
        }

        // "show confidence... ": every beat carries a real, bounded confidence value; an aligned beat
        // over this isochronous fixture should be confident, not an arbitrary placeholder.
        Assert.All(grid.Beats, b => Assert.InRange(b.Confidence, 0.0, 1.0));
        Assert.All(grid.Beats, b => Assert.True(b.Confidence > 0.0, "an aligned beat on a real click fixture must not report zero confidence."));

        // No beat is pre-marked as user-corrected by this step.
        Assert.All(grid.Beats, b => Assert.False(b.UserCorrected));

        // Provenance recorded (Section 4's "every cache key includes algorithm/model/settings versions").
        Assert.Equal(onsets.SourceHash, grid.SourceHash);
        Assert.Equal(onsets.VersionId, grid.SourceOnsetAnalysisVersionId);
        Assert.Equal(tempo.VersionId, grid.SourceTempoAnalysisVersionId);
        Assert.Equal(BeatGridBuilder.Version, grid.AnalyzerVersion);
        Assert.Equal(GridSettings, grid.Settings);
        Assert.Equal(tempo.Candidates, grid.Candidates);
        Assert.Equal(0, grid.CorrectionVersion);

        // Cache reopens without recompute.
        var grid2 = await builder.BuildAsync(onsets, tempo, GridSettings, CancellationToken.None);
        Assert.Equal(grid.VersionId, grid2.VersionId);
    }

    [Fact]
    public async Task BuildAsync_MissingClicksLeaveUnalignedBeats_LowerConfidenceWithoutAssertingCertaintyAway()
    {
        // A click track with a large silent gap in the middle: the grid still generates beats across
        // that gap (interpolated from the tempo period alone), but those unaligned beats must report
        // a visibly lower confidence than the aligned ones — "avoid asserting uncertainty away."
        var leadIn = 4000;
        var firstBurst = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 4, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var gapSamples = SampleRate * 2; // 2s silent gap, several missed beats at 120 BPM (0.5s/beat)
        var secondBurst = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 4, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);

        var samples = new float[leadIn + firstBurst.Length + gapSamples + secondBurst.Length];
        Array.Copy(firstBurst, 0, samples, leadIn, firstBurst.Length);
        Array.Copy(secondBurst, 0, samples, leadIn + firstBurst.Length + gapSamples, secondBurst.Length);

        var asset = await ProbeAsync("gapped.wav", samples);
        var onsets = await BuildOnsetsAsync(asset);
        var tempo = await BuildTempoAsync(onsets, Path.Combine(_tempDir, "tempo-cache"));
        Assert.NotEmpty(tempo.Candidates);

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);
        var grid = await builder.BuildAsync(onsets, tempo, GridSettings, CancellationToken.None);

        var aligned = grid.Beats.Where(b => onsets.Onsets.Min(o => Math.Abs(o.TimeTicks - b.TimeTicks)) <= GridSettings.AlignmentToleranceTicks).ToArray();
        var unaligned = grid.Beats.Where(b => onsets.Onsets.Min(o => Math.Abs(o.TimeTicks - b.TimeTicks)) > GridSettings.AlignmentToleranceTicks).ToArray();

        Assert.NotEmpty(aligned);
        Assert.NotEmpty(unaligned); // sanity: the gap really did produce at least one interpolated-only beat.
        Assert.True(unaligned.Max(b => b.Confidence) < aligned.Min(b => b.Confidence),
            "an interpolated beat with no corroborating onset must report lower confidence than every aligned beat.");
    }

    [Fact]
    public async Task BuildAsync_SilentAudio_ReturnsEmptyBeatsWithNullSelectedTempoWithoutThrowing_HandlesSilenceExplicitly()
    {
        var silentSamples = new float[SampleRate * 2];
        var asset = await ProbeAsync("silence.wav", silentSamples);
        var onsets = await BuildOnsetsAsync(asset);
        Assert.Empty(onsets.Onsets);

        var tempo = await BuildTempoAsync(onsets, Path.Combine(_tempDir, "tempo-cache"));
        Assert.Empty(tempo.Candidates);

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        var grid = await builder.BuildAsync(onsets, tempo, GridSettings, CancellationToken.None);

        Assert.Null(grid.SelectedTempo);
        Assert.Empty(grid.Beats);
        Assert.Empty(grid.Candidates);
        Assert.True(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task ProbeAsync_ZeroLengthMedia_IsRejectedBeforeAnyRhythmAnalysis_HandlesMissingMediaExplicitly()
    {
        var ex = await Assert.ThrowsAsync<AudioProbeInvalidException>(() => ProbeAsync("empty.wav", []));
        Assert.Contains("zero-length", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_OnsetAnalysisWithNoDetectableOnsets_ReturnsEmptyBeatsWithoutThrowing_HandlesMissingMediaExplicitly()
    {
        var asset = await ProbeAsync("too-short.wav", new float[10]);
        var onsets = await BuildOnsetsAsync(asset);
        Assert.Empty(onsets.Onsets);

        var tempo = await BuildTempoAsync(onsets, Path.Combine(_tempDir, "tempo-cache"));

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        var grid = await builder.BuildAsync(onsets, tempo, GridSettings, CancellationToken.None);

        Assert.Empty(grid.Beats);
        Assert.True(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task BuildAsync_NonPositiveBeatsPerBar_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath()
    {
        var (onsets, tempo) = await BuildClickFixtureAsync("boundary-1.wav", bpm: 120, clickCount: 4);
        var invalid = GridSettings with { BeatsPerBar = 0 };

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        var ex = await Assert.ThrowsAsync<BeatGridInvalidException>(
            () => builder.BuildAsync(onsets, tempo, invalid, CancellationToken.None));

        Assert.Contains("BeatsPerBar", ex.Reason);
        Assert.False(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task BuildAsync_NegativeAlignmentTolerance_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath()
    {
        var (onsets, tempo) = await BuildClickFixtureAsync("boundary-2.wav", bpm: 120, clickCount: 4);
        var invalid = GridSettings with { AlignmentToleranceTicks = -1 };

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        var ex = await Assert.ThrowsAsync<BeatGridInvalidException>(
            () => builder.BuildAsync(onsets, tempo, invalid, CancellationToken.None));

        Assert.Contains("AlignmentToleranceTicks", ex.Reason);
        Assert.False(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task BuildAsync_TempoAnalysisFromADifferentOnsetAnalysisVersion_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath()
    {
        // Same SongId (one probed asset), but two independently rebuilt OnsetAnalysis runs (different
        // OnsetDetectionSettings) — tempo2 is derived from onsets2, so passing onsets1 alongside it
        // must be rejected as a stale/mismatched pair, distinct from the "different song" check.
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm: 120, clickCount: 6, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var samples = new float[4000 + clicks.Length];
        Array.Copy(clicks, 0, samples, 4000, clicks.Length);
        var asset = await ProbeAsync("mismatch.wav", samples);

        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsets1 = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
        var onsets2 = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings with { SensitivityMultiplier = 10.0f }, CancellationToken.None);
        Assert.Equal(onsets1.SongId, onsets2.SongId);
        Assert.NotEqual(onsets1.VersionId, onsets2.VersionId);

        var tempo2 = await BuildTempoAsync(onsets2, Path.Combine(_tempDir, "tempo-cache"));

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        var ex = await Assert.ThrowsAsync<BeatGridInvalidException>(
            () => builder.BuildAsync(onsets1, tempo2, GridSettings, CancellationToken.None));

        Assert.Contains("SourceOnsetAnalysisVersionId", ex.Reason);
        Assert.False(File.Exists(store.GetPath(onsets1.SongId)));
    }

    [Fact]
    public async Task BuildAsync_NullArguments_ThrowArgumentNullException()
    {
        var (onsets, tempo) = await BuildClickFixtureAsync("nulls.wav", bpm: 120, clickCount: 4);
        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(null!, tempo, GridSettings, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(onsets, null!, GridSettings, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(onsets, tempo, null!, CancellationToken.None));
    }

    [Fact]
    public async Task BuildAsync_CancelledBeforeComputeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath()
    {
        var (onsets, tempo) = await BuildClickFixtureAsync("cancel.wav", bpm: 120, clickCount: 12);

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => builder.BuildAsync(onsets, tempo, GridSettings, cts.Token));

        Assert.False(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task BuildAsync_SourceTempoAnalysisVersionChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath()
    {
        var (onsets, tempo1) = await BuildClickFixtureAsync("stale.wav", bpm: 120, clickCount: 6);

        var store = new JsonBeatGridStore(Path.Combine(_tempDir, "beatgrid-cache"));
        var builder = new BeatGridBuilder(store);

        var grid1 = await builder.BuildAsync(onsets, tempo1, GridSettings, CancellationToken.None);

        // Rebuild tempo analysis with different TempoDetectionSettings for the exact same onsets —
        // produces a new TempoAnalysis.VersionId, still built from the same OnsetAnalysis.
        var differentTempoSettings = TempoSettings with { MaxCandidates = 1 };
        var tempoStore2 = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache-2"));
        var tempo2 = await new TempoDetector(tempoStore2).DetectAsync(onsets, differentTempoSettings, CancellationToken.None);
        Assert.NotEqual(tempo1.VersionId, tempo2.VersionId);

        var grid2 = await builder.BuildAsync(onsets, tempo2, GridSettings, CancellationToken.None);

        Assert.NotEqual(grid1.VersionId, grid2.VersionId);
        Assert.Equal(tempo2.VersionId, grid2.SourceTempoAnalysisVersionId);
    }
}
