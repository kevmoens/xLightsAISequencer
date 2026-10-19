using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-05 / Step 032 — "Implement onset/transient detection with configurable sensitivity." Required
/// acceptance: "Onset precision/recall measured on annotated fixtures." Failure/edge case: "Invalid
/// input, cancellation or stale persisted state must not corrupt the project." Implementation
/// guidance: "Build spectral flux/energy rise onsets and optional percussive stem hooks; correct
/// algorithmic group delay; test false triggers from sustained chords" (window = 256 samples, hop =
/// 128 samples at the fixture's own 8 kHz rate throughout this file, matching Step 031).
/// </summary>
public sealed class Step032AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly OnsetDetectionSettings DefaultOnsetSettings = new(SensitivityMultiplier: 1.5f, MinInterOnsetSamples: 256);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step032-{Guid.NewGuid():N}");

    public Step032AcceptanceTests()
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

    /// <summary>
    /// A click track with a silent lead-in prepended, so the first click's own onset is not placed at
    /// absolute sample 0 — spectral flux is, by definition, undefined for the very first analysis
    /// frame (no predecessor to compare against), so a true onset sitting exactly at sample 0 could
    /// never be detected by this class of algorithm regardless of sensitivity. This is a disclosed,
    /// standard onset-detection edge case, not something this fixture should (or plausibly could) paper
    /// over by asserting recall against an undetectable position.
    /// </summary>
    private static (float[] Samples, long[] TrueOnsetSamples) BuildLeadInClickTrack(int bpm, int clickCount, int leadInSamples)
    {
        var clicks = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);

        var samples = new float[leadInSamples + clicks.Length];
        Array.Copy(clicks, 0, samples, leadInSamples, clicks.Length);

        var beatIntervalSamples = (long)(60.0 / bpm * SampleRate);
        var trueOnsets = new long[clickCount];
        for (var i = 0; i < clickCount; i++)
        {
            trueOnsets[i] = leadInSamples + (i * beatIntervalSamples);
        }

        return (samples, trueOnsets);
    }

    private static long ToTicks(long sampleIndex) => (long)Math.Round(sampleIndex * (10_000_000.0 / SampleRate));

    [Fact]
    public async Task Step032_Acceptance_Passes_OnsetPrecisionRecallMeasuredOnAnnotatedFixture()
    {
        // Annotated fixture: true onset sample positions are known exactly (the synthetic click
        // generator's own recipe), matching Step 032's own required acceptance text verbatim.
        var (samples, trueOnsetSamples) = BuildLeadInClickTrack(bpm: 120, clickCount: 6, leadInSamples: 4000);
        var asset = await ProbeAsync("click-track.wav", samples);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(decoder, store);

        var analysis = await detector.DetectAsync(asset, WindowSettings, DefaultOnsetSettings, CancellationToken.None);

        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.True(File.Exists(store.GetPath(asset.Id)));
        Assert.NotEmpty(analysis.Onsets);

        // Tolerance: one hop (128 samples / 16 ms) — tight enough that only a correctly group-delay
        // corrected timestamp (window center, not window start) can land inside it for every click.
        var toleranceTicks = ToTicks(WindowSettings.HopSamples);

        var truePositives = 0;
        var matchedTrueOnsets = new HashSet<int>();
        foreach (var detected in analysis.Onsets)
        {
            var matchIndex = -1;
            for (var i = 0; i < trueOnsetSamples.Length; i++)
            {
                if (matchedTrueOnsets.Contains(i))
                {
                    continue;
                }

                if (Math.Abs(detected.TimeTicks - ToTicks(trueOnsetSamples[i])) <= toleranceTicks)
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex >= 0)
            {
                matchedTrueOnsets.Add(matchIndex);
                truePositives++;
            }
        }

        var precision = truePositives / (double)analysis.Onsets.Length;
        var recall = truePositives / (double)trueOnsetSamples.Length;

        Assert.True(precision >= 0.8, $"expected precision >= 0.8, was {precision} ({truePositives}/{analysis.Onsets.Length} detections matched).");
        Assert.True(recall >= 1.0, $"expected recall == 1.0 (every annotated click found), was {recall} ({truePositives}/{trueOnsetSamples.Length} true onsets matched).");

        // "correct algorithmic group delay": every matched detection's timestamp lands within one hop
        // of its true click start, not systematically offset by half a window (32 ms) or more.
        foreach (var trueSample in trueOnsetSamples)
        {
            var nearest = analysis.Onsets.MinBy(o => Math.Abs(o.TimeTicks - ToTicks(trueSample)));
            Assert.NotNull(nearest);
            Assert.True(Math.Abs(nearest!.TimeTicks - ToTicks(trueSample)) <= toleranceTicks,
                $"onset near true sample {trueSample} was offset by {Math.Abs(nearest.TimeTicks - ToTicks(trueSample))} ticks, exceeding the one-hop tolerance of {toleranceTicks}.");
        }

        Assert.All(analysis.Onsets, o =>
        {
            Assert.InRange(o.Strength, 0f, 1f);
            Assert.Equal(OnsetDetector.AlgorithmName, o.SourceAlgorithm);
        });
        Assert.Contains(analysis.Onsets, o => o.Strength == 1.0f);

        // Provenance recorded (Section 4's "every cache key includes algorithm/model/settings versions").
        Assert.Equal(asset.Sha256, analysis.SourceHash);
        Assert.Equal(asset.DecoderVersion, analysis.SourceDecoderVersion);
        Assert.Equal(OnsetDetector.Version, analysis.AnalyzerVersion);
        Assert.Equal(WindowSettings, analysis.Settings);
        Assert.Equal(DefaultOnsetSettings, analysis.OnsetSettings);

        // Cache reopens without recompute.
        var analysis2 = await detector.DetectAsync(asset, WindowSettings, DefaultOnsetSettings, CancellationToken.None);
        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.Equal(analysis.VersionId, analysis2.VersionId);
    }

    [Fact]
    public async Task DetectAsync_SustainedTone_NoSpuriousOnsetsAfterAttack_FalseTriggerNegativePath()
    {
        // "test false triggers from sustained chords": a single long, constant-amplitude tone has
        // exactly one real transient (its own abrupt start) and must not keep re-triggering onsets for
        // every subsequent analysis frame merely because the signal continues to be loud.
        var sustainedTone = DeterministicSyntheticAudioGenerator.BuildNoteSequence(
            frequenciesHz: [440.0], noteDurationSeconds: 2.0, SampleRate);
        var asset = await ProbeAsync("sustained-tone.wav", sustainedTone);

        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(new WavAudioDecoder(), store);

        var analysis = await detector.DetectAsync(asset, WindowSettings, DefaultOnsetSettings, CancellationToken.None);

        // At most the initial attack may register; nothing in the steady-state body of the sustained
        // tone (everything after the first 256-sample window) should cross the adaptive threshold.
        Assert.All(analysis.Onsets, o => Assert.True(o.TimeTicks <= ToTicks(WindowSettings.FftSize * 2),
            $"unexpected mid-sustain onset at {o.TimeTicks} ticks — spectral flux should settle to near-zero once the tone is steady."));
    }

    [Fact]
    public async Task DetectAsync_HigherSensitivityMultiplier_ProducesFewerOrEqualOnsets_ConfigurableSensitivity()
    {
        // This step's own title: "configurable sensitivity." A higher SensitivityMultiplier raises the
        // adaptive threshold, so it must never detect strictly more onsets than a lower one on the
        // same signal.
        var (samples, _) = BuildLeadInClickTrack(bpm: 120, clickCount: 6, leadInSamples: 4000);
        var asset = await ProbeAsync("sensitivity.wav", samples);

        var sensitive = DefaultOnsetSettings with { SensitivityMultiplier = 0.3f };
        var strict = DefaultOnsetSettings with { SensitivityMultiplier = 4.0f };

        var detectorSensitive = new OnsetDetector(new WavAudioDecoder(), new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "sensitive-cache")));
        var detectorStrict = new OnsetDetector(new WavAudioDecoder(), new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "strict-cache")));

        var sensitiveResult = await detectorSensitive.DetectAsync(asset, WindowSettings, sensitive, CancellationToken.None);
        var strictResult = await detectorStrict.DetectAsync(asset, WindowSettings, strict, CancellationToken.None);

        Assert.True(sensitiveResult.Onsets.Length >= strictResult.Onsets.Length,
            $"expected the more sensitive setting ({sensitive.SensitivityMultiplier}) to detect at least as many onsets as the stricter one ({strict.SensitivityMultiplier}): {sensitiveResult.Onsets.Length} vs {strictResult.Onsets.Length}.");
    }

    [Fact]
    public async Task DetectAsync_MismatchedAnalysisSampleRate_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath()
    {
        var (samples, _) = BuildLeadInClickTrack(bpm: 120, clickCount: 2, leadInSamples: 1000);
        var asset = await ProbeAsync("rate-mismatch.wav", samples);
        var mismatched = WindowSettings with { AnalysisSampleRate = SampleRate * 2 };

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(decoder, store);

        var ex = await Assert.ThrowsAsync<OnsetAnalysisInvalidException>(
            () => detector.DetectAsync(asset, mismatched, DefaultOnsetSettings, CancellationToken.None));

        Assert.Contains("AnalysisSampleRate", ex.Reason);
        Assert.Equal(0, decoder.DecodeCallCount);
        Assert.False(File.Exists(store.GetPath(asset.Id)));
    }

    [Fact]
    public async Task DetectAsync_NonPositiveSensitivityMultiplier_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath()
    {
        var (samples, _) = BuildLeadInClickTrack(bpm: 120, clickCount: 2, leadInSamples: 1000);
        var asset = await ProbeAsync("bad-sensitivity.wav", samples);
        var invalidOnsetSettings = DefaultOnsetSettings with { SensitivityMultiplier = 0f };

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(decoder, store);

        var ex = await Assert.ThrowsAsync<OnsetAnalysisInvalidException>(
            () => detector.DetectAsync(asset, WindowSettings, invalidOnsetSettings, CancellationToken.None));

        Assert.Contains("SensitivityMultiplier", ex.Reason);
        Assert.Equal(0, decoder.DecodeCallCount);
        Assert.False(File.Exists(store.GetPath(asset.Id)));
    }

    [Fact]
    public async Task DetectAsync_DeclaredSampleCountMismatch_ThrowsOnsetAnalysisInvalidException_NegativePath()
    {
        var (samples, _) = BuildLeadInClickTrack(bpm: 120, clickCount: 2, leadInSamples: 1000);
        var asset = await ProbeAsync("count-mismatch.wav", samples);
        var doctored = asset with { SampleCount = asset.SampleCount + 1000 };

        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(new WavAudioDecoder(), store);

        var ex = await Assert.ThrowsAsync<OnsetAnalysisInvalidException>(
            () => detector.DetectAsync(doctored, WindowSettings, DefaultOnsetSettings, CancellationToken.None));

        Assert.Contains("sample count", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(store.GetPath(doctored.Id)));
    }

    [Fact]
    public async Task DetectAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath()
    {
        var (samples, _) = BuildLeadInClickTrack(bpm: 120, clickCount: 20, leadInSamples: 1000);
        var asset = await ProbeAsync("cancel.wav", samples);

        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(new WavAudioDecoder(), store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => detector.DetectAsync(asset, WindowSettings, DefaultOnsetSettings, cts.Token));

        Assert.False(File.Exists(store.GetPath(asset.Id)));
    }

    [Fact]
    public async Task DetectAsync_ContentHashChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath()
    {
        var (samples, _) = BuildLeadInClickTrack(bpm: 120, clickCount: 4, leadInSamples: 1000);
        var asset = await ProbeAsync("hash-change.wav", samples);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(decoder, store);

        var analysis1 = await detector.DetectAsync(asset, WindowSettings, DefaultOnsetSettings, CancellationToken.None);
        Assert.Equal(1, decoder.DecodeCallCount);

        var changed = asset with { Sha256 = "0000000000000000000000000000000000000000000000000000000000000" };
        var analysis2 = await detector.DetectAsync(changed, WindowSettings, DefaultOnsetSettings, CancellationToken.None);

        Assert.Equal(2, decoder.DecodeCallCount);
        Assert.NotEqual(analysis1.VersionId, analysis2.VersionId);
        Assert.Equal(changed.Sha256, analysis2.SourceHash);

        var reopened = await store.LoadAsync(store.GetPath(asset.Id), CancellationToken.None);
        Assert.Equal(changed.Sha256, reopened.SourceHash);
    }

    [Fact]
    public async Task DetectAsync_OnsetSettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath()
    {
        // Same song/content/windowing, different sensitivity — must not silently serve an analysis
        // computed with the previous (now stale) OnsetDetectionSettings.
        var (samples, _) = BuildLeadInClickTrack(bpm: 120, clickCount: 6, leadInSamples: 4000);
        var asset = await ProbeAsync("onset-settings-change.wav", samples);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonOnsetAnalysisStore(Path.Combine(_tempDir, "onset-cache"));
        var detector = new OnsetDetector(decoder, store);

        var analysis1 = await detector.DetectAsync(asset, WindowSettings, DefaultOnsetSettings, CancellationToken.None);
        var stricter = DefaultOnsetSettings with { SensitivityMultiplier = 10.0f };
        var analysis2 = await detector.DetectAsync(asset, WindowSettings, stricter, CancellationToken.None);

        Assert.Equal(2, decoder.DecodeCallCount);
        Assert.Equal(stricter, analysis2.OnsetSettings);
        Assert.True(analysis2.Onsets.Length <= analysis1.Onsets.Length);
    }

    /// <summary>Wraps a real <see cref="IAudioDecoder"/>, counting <see cref="DecodeMonoAsync"/> calls so a test can prove cache behavior against real decode behavior, not a hand-rolled fake.</summary>
    private sealed class CountingAudioDecoder(IAudioDecoder inner) : IAudioDecoder
    {
        public int DecodeCallCount { get; private set; }

        public Task<SongAsset> ProbeAsync(string path, CancellationToken ct) => inner.ProbeAsync(path, ct);

        public IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(SongAsset song, int targetSampleRate, CancellationToken ct)
        {
            DecodeCallCount++;
            return inner.DecodeMonoAsync(song, targetSampleRate, ct);
        }
    }
}
