using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-05 / Step 033 — "Implement BPM candidate detection including half/double-time alternatives."
/// Required acceptance: "120 BPM and 60/120 ambiguity tests pass." Failure/edge case: "Silence, invalid
/// timing boundaries and missing media must be handled explicitly." Implementation guidance: "Rank
/// multiple tempo candidates rather than one BPM; evaluate autocorrelation/periodicity and known
/// half/double-time signals" (window = 256 samples, hop = 128 samples at the fixture's own 8 kHz rate
/// for the onset analysis stage, matching Steps 031/032; 10 ms autocorrelation bins throughout this
/// file).
/// </summary>
public sealed class Step033AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly OnsetDetectionSettings OnsetSettings = new(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: 256);
    private static readonly TempoDetectionSettings TempoSettings = new(MinBpm: 40, MaxBpm: 300, TimeResolutionSeconds: 0.01, MaxCandidates: 5);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step033-{Guid.NewGuid():N}");

    public Step033AcceptanceTests()
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

    private async Task<OnsetAnalysis> BuildOnsetsAsync(string fileName, int bpm, int clickCount, int leadInSamples = 4000)
    {
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        var samples = new float[leadInSamples + clicks.Length];
        Array.Copy(clicks, 0, samples, leadInSamples, clicks.Length);

        var asset = await ProbeAsync(fileName, samples);
        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsetDetector = new OnsetDetector(new WavAudioDecoder(), onsetStore);
        return await onsetDetector.DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
    }

    private static bool HasCandidateNear(TempoAnalysis analysis, double bpm, double tolerance = 2.0) =>
        analysis.Candidates.Any(c => Math.Abs(c.Bpm - bpm) <= tolerance);

    [Fact]
    public async Task Step033_Acceptance_Passes_120BpmAndHalfDoubleAmbiguity()
    {
        var onsets = await BuildOnsetsAsync("click-120.wav", bpm: 120, clickCount: 12);
        Assert.NotEmpty(onsets.Onsets); // sanity: Step 032's own detector actually found the clicks.

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        var analysis = await detector.DetectAsync(onsets, TempoSettings, CancellationToken.None);

        Assert.True(File.Exists(store.GetPath(onsets.SongId)));
        Assert.NotEmpty(analysis.Candidates);

        // "Rank multiple tempo candidates rather than one BPM": more than a single collapsed answer.
        Assert.True(analysis.Candidates.Length > 1, "expected more than one ranked tempo candidate.");

        // Required acceptance, verbatim: "120 BPM ... tests pass" — the true tempo is the top-ranked
        // (highest-confidence) candidate.
        var top = analysis.Candidates[0];
        Assert.Equal(120.0, top.Bpm, tolerance: 2.0);
        Assert.Equal(1.0, top.Confidence, precision: 6);

        // Required acceptance, verbatim: "...and 60/120 ambiguity tests pass" — an isochronous 120 BPM
        // source is, by construction, also perfectly periodic at 60 BPM (every other beat), so a real
        // periodicity-based detector must surface 60 BPM as a genuine alternate hypothesis, not hide it.
        Assert.True(HasCandidateNear(analysis, 60.0), $"expected a 60 BPM half-time alternate hypothesis among: {string.Join(", ", analysis.Candidates.Select(c => c.Bpm))}");

        // Confidence is a real, bounded, ranked signal.
        Assert.All(analysis.Candidates, c => Assert.InRange(c.Confidence, 0.0, 1.0));
        for (var i = 1; i < analysis.Candidates.Length; i++)
        {
            Assert.True(analysis.Candidates[i - 1].Confidence >= analysis.Candidates[i].Confidence,
                "candidates must be ranked by confidence descending.");
        }

        Assert.All(analysis.Candidates, c => Assert.False(string.IsNullOrWhiteSpace(c.MeterGuess)));

        // Provenance recorded (Section 4's "every cache key includes algorithm/model/settings versions").
        Assert.Equal(onsets.SourceHash, analysis.SourceHash);
        Assert.Equal(onsets.VersionId, analysis.SourceOnsetAnalysisVersionId);
        Assert.Equal(TempoDetector.Version, analysis.AnalyzerVersion);
        Assert.Equal(TempoSettings, analysis.Settings);

        // Cache reopens without recompute (no decoder/cache-count instrumentation needed here — the
        // detector never touches audio or the onset store again; re-run equality is the real proof).
        var analysis2 = await detector.DetectAsync(onsets, TempoSettings, CancellationToken.None);
        Assert.Equal(analysis.VersionId, analysis2.VersionId);
    }

    [Fact]
    public async Task DetectAsync_SilentAudio_ReturnsEmptyCandidatesWithoutThrowing_HandlesSilenceExplicitly()
    {
        // "Silence... must be handled explicitly": a real song that decodes to literally zero onsets
        // (true digital silence) must not crash the tempo scan — it has no measurable periodicity at
        // all, and that must be reported plainly as an empty candidate list.
        var silentSamples = new float[SampleRate * 2];
        var asset = await ProbeAsync("silence.wav", silentSamples);
        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var onsets = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, OnsetSettings, CancellationToken.None);
        Assert.Empty(onsets.Onsets); // sanity: truly no transients detected in digital silence.

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        var analysis = await detector.DetectAsync(onsets, TempoSettings, CancellationToken.None);

        Assert.Empty(analysis.Candidates);
        Assert.True(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task ProbeAsync_ZeroLengthMedia_IsRejectedBeforeAnyOnsetOrTempoScan_HandlesMissingMediaExplicitly()
    {
        // "...missing media must be handled explicitly": a zero-length/empty audio file is already
        // rejected explicitly, with a clear reason, at the Audio layer's own probe boundary — one layer
        // before Step 033's own OnsetAnalysis-consuming scope even begins. This proves "missing media"
        // can never silently reach (and confuse) the tempo scan as a degenerate zero-duration input.
        var ex = await Assert.ThrowsAsync<AudioProbeInvalidException>(() => ProbeAsync("empty.wav", []));
        Assert.Contains("zero-length", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DetectAsync_OnsetAnalysisWithNoDetectableOnsets_ReturnsEmptyCandidatesWithoutThrowing_HandlesMissingMediaExplicitly()
    {
        // The in-scope "missing media" shape for this step: a real, successfully probed/decoded
        // OnsetAnalysis (Step 033's own sole input contract — see ITempoDetector's own remarks) that
        // simply carries no onsets at all, e.g. because the underlying media was too short or otherwise
        // degenerate for Step 032's own detector to find any transient. Must resolve to an explicit
        // empty candidate list, never an unhandled exception deep inside the autocorrelation scan.
        var onsets = await BuildOnsetsAsync("too-short.wav", bpm: 120, clickCount: 0, leadInSamples: 10);
        Assert.Empty(onsets.Onsets);

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        var analysis = await detector.DetectAsync(onsets, TempoSettings, CancellationToken.None);

        Assert.Empty(analysis.Candidates);
        Assert.True(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task DetectAsync_MaxBpmNotGreaterThanMinBpm_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath()
    {
        var onsets = await BuildOnsetsAsync("boundary-1.wav", bpm: 120, clickCount: 4);
        var invalid = TempoSettings with { MaxBpm = TempoSettings.MinBpm };

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        var ex = await Assert.ThrowsAsync<TempoAnalysisInvalidException>(
            () => detector.DetectAsync(onsets, invalid, CancellationToken.None));

        Assert.Contains("MaxBpm", ex.Reason);
        Assert.False(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task DetectAsync_NonPositiveMinBpm_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath()
    {
        var onsets = await BuildOnsetsAsync("boundary-2.wav", bpm: 120, clickCount: 4);
        var invalid = TempoSettings with { MinBpm = 0 };

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        var ex = await Assert.ThrowsAsync<TempoAnalysisInvalidException>(
            () => detector.DetectAsync(onsets, invalid, CancellationToken.None));

        Assert.Contains("MinBpm", ex.Reason);
        Assert.False(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task DetectAsync_NonPositiveTimeResolution_ThrowsWithoutWriting_InvalidTimingBoundaryNegativePath()
    {
        var onsets = await BuildOnsetsAsync("boundary-3.wav", bpm: 120, clickCount: 4);
        var invalid = TempoSettings with { TimeResolutionSeconds = 0 };

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        var ex = await Assert.ThrowsAsync<TempoAnalysisInvalidException>(
            () => detector.DetectAsync(onsets, invalid, CancellationToken.None));

        Assert.Contains("TimeResolutionSeconds", ex.Reason);
        Assert.False(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task DetectAsync_NullOnsets_ThrowsArgumentNullException()
    {
        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => detector.DetectAsync(null!, TempoSettings, CancellationToken.None));
    }

    [Fact]
    public async Task DetectAsync_CancelledBeforeComputeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath()
    {
        var onsets = await BuildOnsetsAsync("cancel.wav", bpm: 120, clickCount: 12);

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => detector.DetectAsync(onsets, TempoSettings, cts.Token));

        Assert.False(File.Exists(store.GetPath(onsets.SongId)));
    }

    [Fact]
    public async Task DetectAsync_SourceOnsetAnalysisVersionChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath()
    {
        var onsets1 = await BuildOnsetsAsync("stale-1.wav", bpm: 120, clickCount: 6);

        var store = new JsonTempoAnalysisStore(Path.Combine(_tempDir, "tempo-cache"));
        var detector = new TempoDetector(store);

        var analysis1 = await detector.DetectAsync(onsets1, TempoSettings, CancellationToken.None);

        // A re-run of onset detection with different OnsetDetectionSettings produces a new
        // OnsetAnalysis.VersionId for the exact same SongId/content — the tempo cache must not silently
        // keep serving the result built from the now-superseded onset run.
        var asset = await new WavAudioDecoder().ProbeAsync(Path.Combine(_tempDir, "stale-1.wav"), CancellationToken.None);
        var onsetStore = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var stricterOnsetSettings = OnsetSettings with { SensitivityMultiplier = 10.0f };
        var onsets2 = await new OnsetDetector(new WavAudioDecoder(), onsetStore)
            .DetectAsync(asset, WindowSettings, stricterOnsetSettings, CancellationToken.None);
        Assert.NotEqual(onsets1.VersionId, onsets2.VersionId);

        var analysis2 = await detector.DetectAsync(onsets2, TempoSettings, CancellationToken.None);

        Assert.NotEqual(analysis1.VersionId, analysis2.VersionId);
        Assert.Equal(onsets2.VersionId, analysis2.SourceOnsetAnalysisVersionId);
    }
}
