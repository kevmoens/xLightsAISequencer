using System.Security.Cryptography;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06 Step 044 — "Compare reference outputs, audio metrics and listening tests; establish
/// supported hardware." Required acceptance (verbatim): "Measured separation quality/performance is
/// documented and acceptance threshold met." Failure/edge case (verbatim): "Silence, invalid timing
/// boundaries and missing media must be handled explicitly."
///
/// Reuses the exact same honestly-synthetic, analytically-invertible "stem = input * known_gain" ONNX
/// fixture Steps 040/041 already committed (<c>fixtures/onnx/synthetic-stem-separator-v1.onnx</c>) as
/// the ground-truth-known mechanism this step measures -- the committed fixture's exactly-known gains
/// let a reference clip be constructed analytically (reference = source * gain) without needing a real
/// production separation model, which Step 038's own feasibility verdict still has BLOCKED. See
/// <c>implementation-evidence/PHASE-06/STEP-044.md</c> §3.
/// </summary>
public sealed class Step044AcceptanceTests : IDisposable
{
    private const string GoodFixtureBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjrzAQorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwogCgppbnB1dF91bnNxCgVnYWlucxIGb3V0cHV0IgNNdWwSGFN5bnRoZXRpY1N0ZW1TZXBhcmF0b3JWMSojCAEIBAgBCAEQAUIFZ2FpbnNKEM3MzD0AAIA+AAAAPwAAQD9aLgoFaW5wdXQSJQojCAESHwoCCAEKChIIY2hhbm5lbHMKDRILbnVtX3NhbXBsZXNiMwoGb3V0cHV0EikKJwgBEiMKAggBCgIIBAoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private static readonly IReadOnlyList<StemKind> FixtureStemOrder =
        new[] { StemKind.Drums, StemKind.Bass, StemKind.Vocals, StemKind.Other };

    private static readonly IReadOnlyDictionary<StemKind, float> FixtureGains = new Dictionary<StemKind, float>
    {
        [StemKind.Drums] = 0.10f,
        [StemKind.Bass] = 0.25f,
        [StemKind.Vocals] = 0.50f,
        [StemKind.Other] = 0.75f,
    };

    private readonly string _rootDirectory;
    private readonly string _modelCacheDirectory;
    private readonly string _audioDirectory;
    private readonly string _referenceDirectory;
    private readonly string _stemOutputDirectory;
    private readonly string _reportDirectory;

    public Step044AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step044-{Guid.NewGuid():N}");
        _modelCacheDirectory = Path.Combine(_rootDirectory, "models");
        _audioDirectory = Path.Combine(_rootDirectory, "audio");
        _referenceDirectory = Path.Combine(_rootDirectory, "reference");
        _stemOutputDirectory = Path.Combine(_rootDirectory, "stems");
        _reportDirectory = Path.Combine(_rootDirectory, "reports");
        Directory.CreateDirectory(_modelCacheDirectory);
        Directory.CreateDirectory(_audioDirectory);
        Directory.CreateDirectory(_referenceDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static float[] DeterministicSineWave(int sampleCount, int sampleRate, double frequencyHz, float amplitude)
    {
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
        }

        return samples;
    }

    private static StemSeparationRecipe FixtureRecipe() => new(
        RecipeVersion: "step044-fixture-recipe/1.0.0",
        ExpectedSampleRate: 44100,
        ExpectedChannels: 1,
        StemOrder: FixtureStemOrder,
        InputTensorName: "input",
        OutputTensorName: "output");

    private static ModelAsset FixtureModelAsset(byte[] modelBytes, string id = "synthetic-stem-separator") => new(
        Id: id,
        Version: "1.0.0-fixture",
        Source: new Uri("https://example.invalid/models/synthetic-stem-separator.onnx"),
        Sha256: Convert.ToHexString(SHA256.HashData(modelBytes)).ToLowerInvariant(),
        LicenseId: "fixture-only, no third-party rights",
        InferenceBackend: "Microsoft.ML.OnnxRuntime (CPU)");

    private static async Task SeedVerifiedModelAsync(ModelAssetManager manager, ModelAsset asset, byte[] modelBytes)
    {
        var verifiedPath = manager.GetVerifiedPath(asset);
        await File.WriteAllBytesAsync(verifiedPath, modelBytes);
    }

    private async Task<SongAsset> WriteSourceClipAsync(string fileName, float[] samples, int sampleRate)
    {
        var path = Path.Combine(_audioDirectory, fileName);
        await StemWavWriter.WriteMonoFloatAsync(path, samples, sampleRate, CancellationToken.None);
        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    /// <summary>
    /// Writes the analytically-known-correct reference stem for every fixture gain
    /// (reference = source * gain, exactly what the committed fixture's own ONNX graph computes) and
    /// returns the resulting <see cref="QualityClip"/>.
    /// </summary>
    private async Task<QualityClip> BuildQualityClipAsync(string clipId, float[] sourceSamples, int sampleRate)
    {
        var source = await WriteSourceClipAsync($"{clipId}.wav", sourceSamples, sampleRate);

        var referencePaths = new Dictionary<StemKind, string>();
        foreach (var (kind, gain) in FixtureGains)
        {
            var referenceSamples = new float[sourceSamples.Length];
            for (var i = 0; i < sourceSamples.Length; i++)
            {
                referenceSamples[i] = sourceSamples[i] * gain;
            }

            var referencePath = Path.Combine(_referenceDirectory, $"{clipId}_{kind}_reference.wav");
            await StemWavWriter.WriteMonoFloatAsync(referencePath, referenceSamples, sampleRate, CancellationToken.None);
            referencePaths[kind] = referencePath;
        }

        return new QualityClip(clipId, source, referencePaths);
    }

    private sealed class NeverCalledSeparator : IStemSeparator
    {
        public Task<StemManifest> SeparateAsync(SongAsset source, ModelAsset model, IProgress<SeparationProgress>? progress, CancellationToken ct) =>
            throw new InvalidOperationException("Separator must not be invoked when a reference media file is missing.");
    }

    [Fact]
    public async Task Step044_Acceptance_Passes_MeasuredQualityAndPerformanceDocumentedAndThresholdMet()
    {
        const int sampleRate = 44100;
        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new ThrowingDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var recipe = FixtureRecipe();
        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory);

        // Full-phase test matrix item 7, verbatim: "At least two listening reference clips meet declared
        // quality bar" -- two distinct, deterministic clips, both real.
        var clip1 = await BuildQualityClipAsync(
            "clip-a", DeterministicSineWave(44100, sampleRate, 220.0, 0.6f), sampleRate);
        var clip2 = await BuildQualityClipAsync(
            "clip-b", DeterministicSineWave(44100, sampleRate, 440.0, 0.4f), sampleRate);

        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 60.0, MinCorrelationCoefficient: 0.999);
        var evaluator = new SeparationQualityEvaluator(new WavAudioDecoder());

        var reportId = Guid.NewGuid();
        const long createdAtTicks = 638_000_000_000_000_000L;
        var report = await evaluator.EvaluateAsync(
            reportId, createdAtTicks, separator, asset, recipe.RecipeVersion,
            new[] { clip1, clip2 }, thresholds, CancellationToken.None);

        Assert.True(report.OverallPass, report.Decision);
        Assert.Equal(8, report.ClipMetrics.Count); // 2 clips * 4 stems
        Assert.All(report.ClipMetrics, m => Assert.True(m.PassesThreshold, $"{m.ClipId}: SDR={m.SignalToDistortionRatioDb} corr={m.CorrelationCoefficient}"));
        Assert.All(report.ClipMetrics, m => Assert.False(double.IsNaN(m.SignalToDistortionRatioDb)));
        Assert.All(report.ClipMetrics, m => Assert.True(double.IsFinite(m.SignalToDistortionRatioDb)));

        // "...establish supported hardware" -- real, measured, non-fabricated hardware figures.
        Assert.True(report.Hardware.ProcessorCount > 0);
        Assert.False(string.IsNullOrWhiteSpace(report.Hardware.OsDescription));
        Assert.True(report.Hardware.PeakWorkingSetBytes > 0);
        Assert.True(report.Hardware.MeasuredWallClockSecondsPerSongMinute >= 0.0);

        // Persisted, versioned, inspectable output: round-trip through a real file on disk.
        var store = new JsonSeparationQualityReportStore(_reportDirectory);
        var path = await store.SaveAsync(report, CancellationToken.None);
        Assert.True(File.Exists(path));

        var reloaded = await store.LoadAsync(path, CancellationToken.None);
        Assert.Equal(report.Id, reloaded.Id);
        Assert.Equal(report.CreatedAtTicks, reloaded.CreatedAtTicks);
        Assert.Equal(report.OverallPass, reloaded.OverallPass);
        Assert.Equal(report.ClipMetrics.Count, reloaded.ClipMetrics.Count);
        Assert.Equal(report.Hardware.ProcessorCount, reloaded.Hardware.ProcessorCount);
    }

    [Fact]
    public void SilentReferenceReproducedBySilentCandidate_HandledExplicitly_NoNaNOrInfinity()
    {
        var reference = new float[256];
        var candidate = new float[256];
        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 0.99);

        var metric = SeparationQualityMetricCalculator.Compute("silent-clip", StemKind.Drums, candidate, reference, thresholds);

        Assert.True(metric.ReferenceIsSilent);
        Assert.Equal(SeparationQualityMetricCalculator.PerfectSilenceSentinelDb, metric.SignalToDistortionRatioDb);
        Assert.Equal(1.0, metric.CorrelationCoefficient);
        Assert.True(metric.PassesThreshold);
        Assert.False(double.IsNaN(metric.SignalToDistortionRatioDb));
        Assert.False(double.IsInfinity(metric.SignalToDistortionRatioDb));
        Assert.False(double.IsNaN(metric.CorrelationCoefficient));
    }

    [Fact]
    public void SilentReferenceButLoudCandidate_HandledExplicitly_FailsWithoutNaN_NegativePath()
    {
        var reference = new float[256];
        var candidate = Enumerable.Repeat(0.5f, 256).ToArray();
        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 0.99);

        var metric = SeparationQualityMetricCalculator.Compute("silent-mismatch", StemKind.Bass, candidate, reference, thresholds);

        Assert.True(metric.ReferenceIsSilent);
        Assert.Equal(SeparationQualityMetricCalculator.FailedSilenceSentinelDb, metric.SignalToDistortionRatioDb);
        Assert.False(metric.PassesThreshold);
        Assert.False(double.IsNaN(metric.SignalToDistortionRatioDb));
        Assert.False(double.IsNaN(metric.CorrelationCoefficient));
    }

    [Fact]
    public void MismatchedClipLengths_ThrowsSeparationQualityInvalidInputException_NegativePath()
    {
        var reference = new float[256];
        var candidate = new float[200];
        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 0.99);

        Assert.Throws<SeparationQualityInvalidInputException>(
            () => SeparationQualityMetricCalculator.Compute("bad-boundary-clip", StemKind.Vocals, candidate, reference, thresholds));
    }

    [Fact]
    public void ZeroLengthClip_ThrowsSeparationQualityInvalidInputException_NegativePath()
    {
        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 0.99);

        Assert.Throws<SeparationQualityInvalidInputException>(
            () => SeparationQualityMetricCalculator.Compute("empty-clip", StemKind.Vocals, Array.Empty<float>(), Array.Empty<float>(), thresholds));
    }

    [Fact]
    public void InvalidThresholds_ThrowsSeparationQualityInvalidInputException_NegativePath()
    {
        var reference = new float[] { 0.1f, 0.2f };
        var candidate = new float[] { 0.1f, 0.2f };
        var invalidThresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 2.0);

        Assert.Throws<SeparationQualityInvalidInputException>(
            () => SeparationQualityMetricCalculator.Compute("bad-thresholds", StemKind.Vocals, candidate, reference, invalidThresholds));
    }

    [Fact]
    public async Task MissingReferenceMediaFile_ThrowsSeparationQualityInvalidInputException_SeparatorNeverInvoked_NegativePath()
    {
        const int sampleRate = 44100;
        var source = await WriteSourceClipAsync("missing-ref-source.wav", DeterministicSineWave(1000, sampleRate, 220.0, 0.5f), sampleRate);

        var missingPath = Path.Combine(_referenceDirectory, "does-not-exist.wav");
        var clip = new QualityClip("missing-ref-clip", source, new Dictionary<StemKind, string> { [StemKind.Drums] = missingPath });

        var asset = FixtureModelAsset(Convert.FromBase64String(GoodFixtureBase64));
        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 0.99);
        var evaluator = new SeparationQualityEvaluator(new WavAudioDecoder());

        var ex = await Assert.ThrowsAsync<SeparationQualityInvalidInputException>(() => evaluator.EvaluateAsync(
            Guid.NewGuid(), 0L, new NeverCalledSeparator(), asset, "recipe/1.0.0",
            new[] { clip }, thresholds, CancellationToken.None));

        Assert.Contains("does not exist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BelowThresholdMeasurement_FlipsOverallPassToFalse_NegativePath()
    {
        const int sampleRate = 44100;
        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new ThrowingDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var recipe = FixtureRecipe();
        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory);

        var clip = await BuildQualityClipAsync("clip-strict", DeterministicSineWave(4410, sampleRate, 220.0, 0.6f), sampleRate);

        // Deliberately unattainable threshold (the real recovery error is float32 rounding noise, on the
        // order of 1e-7 relative, i.e. well under ~300 dB SDR) -- proves the gate actually gates rather
        // than rubber-stamping every run as passing.
        var impossibleThresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 300.0, MinCorrelationCoefficient: 0.5);
        var evaluator = new SeparationQualityEvaluator(new WavAudioDecoder());

        var report = await evaluator.EvaluateAsync(
            Guid.NewGuid(), 0L, separator, asset, recipe.RecipeVersion, new[] { clip }, impossibleThresholds, CancellationToken.None);

        Assert.False(report.OverallPass);
        Assert.Contains(report.ClipMetrics, m => !m.PassesThreshold);
    }

    [Fact]
    public async Task EvaluateAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath()
    {
        const int sampleRate = 44100;
        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new ThrowingDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var recipe = FixtureRecipe();
        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), recipe, _stemOutputDirectory);
        var clip = await BuildQualityClipAsync("clip-cancel", DeterministicSineWave(1000, sampleRate, 220.0, 0.5f), sampleRate);

        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 0.99);
        var evaluator = new SeparationQualityEvaluator(new WavAudioDecoder());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => evaluator.EvaluateAsync(
            Guid.NewGuid(), 0L, separator, asset, recipe.RecipeVersion, new[] { clip }, thresholds, cts.Token));
    }

    [Fact]
    public async Task NoQualityClips_ThrowsSeparationQualityInvalidInputException_NegativePath()
    {
        var asset = FixtureModelAsset(Convert.FromBase64String(GoodFixtureBase64));
        var thresholds = new SeparationQualityThresholds(MinSignalToDistortionRatioDb: 40.0, MinCorrelationCoefficient: 0.99);
        var evaluator = new SeparationQualityEvaluator(new WavAudioDecoder());

        await Assert.ThrowsAsync<SeparationQualityInvalidInputException>(() => evaluator.EvaluateAsync(
            Guid.NewGuid(), 0L, new NeverCalledSeparator(), asset, "recipe/1.0.0",
            Array.Empty<QualityClip>(), thresholds, CancellationToken.None));
    }

    private sealed class ThrowingDownloader : IModelAssetDownloader
    {
        public Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct) =>
            throw new InvalidOperationException("Downloader must not be invoked; Step 044 tests always pre-provision the verified model file.");
    }
}
