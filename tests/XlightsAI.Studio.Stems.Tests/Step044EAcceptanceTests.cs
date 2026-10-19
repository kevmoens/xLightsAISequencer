using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06A Step 044E — "Re-run the quality/hardware-benchmark mechanism against the real model's real
/// output, as a disclosed proxy." Required acceptance (verbatim): "A MixReconstructionSelfConsistencyReport
/// is produced and persisted, recording a real, measured SeparationQualityMetric (reconstructed-mix-vs-
/// original) and a real, measured HardwareProfile for this specific mechanism on this specific machine."
/// Failure/edge case (verbatim): "Reject fabricating any claim of 'matches reference/professional
/// separation quality' anywhere in this report's own Decision text or field names ... A test must assert
/// the report's own Decision text does not claim quality parity, matching Step 044's own implementation
/// guidance verbatim ('reject parity claims unsupported by results')."
///
/// <b>Real network access required for the acceptance test.</b> Reuses the same real, already-downloaded,
/// hash-verified <c>sherpa-onnx-spleeter-2stems</c> archive cache directory Step 044D's own tests
/// established (<see cref="SharedModelAssetCacheDirectory"/>) -- genuine HTTPS download only on this
/// machine's very first run of either step's tests; every subsequent run reuses the already-verified cache.
/// No model weights, archive bytes, or ONNX inference result are faked, mocked, or pre-computed anywhere in
/// this file.
/// </summary>
public sealed class Step044EAcceptanceTests : IDisposable
{
    /// <summary>
    /// Deliberately the same shared cache path <see cref="Step044DAcceptanceTests"/> uses (not deleted in
    /// <see cref="Dispose"/>) -- the real, already hash-verified model cache, so a genuine network download
    /// only ever happens once across this addendum's own test suite, not once per step/file.
    /// </summary>
    private static readonly string SharedModelAssetCacheDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-phase06a-step044d-model-asset-cache");

    private readonly string _runDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step044e-{Guid.NewGuid():N}");

    public Step044EAcceptanceTests() => Directory.CreateDirectory(_runDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_runDirectory))
        {
            Directory.Delete(_runDirectory, recursive: true);
        }
    }

    /// <summary>A real multi-second stereo WAV sample at the real model's own required 44100 Hz -- two
    /// distinct synthetic tones per channel, never a copyrighted recording. Identical fixture shape to
    /// Step 044D's own established pattern.</summary>
    private static async Task<SongAsset> WriteRealStereoFixtureAsync(string path)
    {
        const int sampleRate = 44100;
        var left = DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase([220.0, 330.0], noteDurationSeconds: 1.5, sampleRate);
        var right = DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase([440.0, 550.0], noteDurationSeconds: 1.5, sampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeStereoWav(left, right, sampleRate);
        await File.WriteAllBytesAsync(path, wavBytes);

        var decoder = new WavAudioDecoder();
        return await decoder.ProbeAsync(path, CancellationToken.None);
    }

    private static async Task<ModelAssetManager> EnsureRealModelDownloadedAsync()
    {
        using var downloader = new HttpModelAssetDownloader();
        var manager = new ModelAssetManager(SharedModelAssetCacheDirectory, downloader);
        await manager.EnsureVerifiedAsync(SherpaOnnxSpleeterModelAsset.Archive, userConsent: true, CancellationToken.None);
        return manager;
    }

    [Fact]
    public async Task Step044E_Acceptance_Passes_RealReconstructionSelfConsistencyReportIsProducedAndPersisted()
    {
        // 1. Real downloaded/verified model (reused across the addendum's own test suite) and a real
        // multi-second stereo fixture -- identical real-model wiring to Step 044D's own acceptance test.
        var modelAssetManager = await EnsureRealModelDownloadedAsync();
        var expander = new TarBzip2ModelArchiveExpander();
        var wavPath = Path.Combine(_runDirectory, "real-stereo-sample.wav");
        var source = await WriteRealStereoFixtureAsync(wavPath);

        var separator = new SpectrogramOnnxStemSeparator(
            modelAssetManager,
            expander,
            new WavStereoAudioDecoder(),
            outputDirectory: Path.Combine(_runDirectory, "separation-output"));

        var evaluator = new MixReconstructionSelfConsistencyEvaluator(new WavAudioDecoder());
        var thresholds = new SeparationQualityThresholds(
            MinSignalToDistortionRatioDb: -100.0,
            MinCorrelationCoefficient: -1.0);
        var reportId = Guid.NewGuid();
        var createdAtTicks = 638_000_000_000_000_000L;

        // 2. Required acceptance, verbatim: real separation + real reconstructed-mix self-consistency
        // measurement + real measured hardware profile, all against the real downloaded model's real output.
        var report = await evaluator.EvaluateAsync(
            reportId, createdAtTicks, separator, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe,
            source, thresholds, CancellationToken.None);

        Assert.Equal(reportId, report.Id);
        Assert.Equal(createdAtTicks, report.CreatedAtTicks);
        Assert.Equal(SherpaOnnxSpleeterModelAsset.Archive.Id, report.ModelId);
        Assert.Equal(SherpaOnnxSpleeterModelAsset.Recipe.RecipeVersion, report.RecipeVersion);

        // 3. "A real, measured SeparationQualityMetric (reconstructed-mix-vs-original)" -- verbatim.
        Assert.Equal(MixReconstructionSelfConsistencyEvaluator.ReconstructionClipId, report.ReconstructionMetric.ClipId);
        Assert.False(double.IsNaN(report.ReconstructionMetric.SignalToDistortionRatioDb));
        Assert.False(double.IsInfinity(report.ReconstructionMetric.SignalToDistortionRatioDb));
        Assert.False(double.IsNaN(report.ReconstructionMetric.CorrelationCoefficient));
        Assert.InRange(report.ReconstructionMetric.CorrelationCoefficient, -1.0, 1.0);

        // 4. "A real, measured HardwareProfile for this specific mechanism on this specific machine" --
        // verbatim. Never estimated/upstream-reported figures (Step 038's own superseded pattern).
        Assert.False(string.IsNullOrWhiteSpace(report.Hardware.OsDescription));
        Assert.True(report.Hardware.ProcessorCount > 0);
        Assert.True(report.Hardware.TotalAvailableMemoryBytes > 0);
        Assert.True(report.Hardware.PeakWorkingSetBytes > 0);
        Assert.True(report.Hardware.MeasuredWallClockSecondsPerSongMinute > 0.0);

        // 5. Required negative-path assertion, verbatim: "A test must assert the report's own Decision text
        // does not claim quality parity." Never present, regardless of whether the proxy metric passed.
        AssertDecisionNeverClaimsQualityParity(report.Decision);

        // 6. "Saved audio/analysis artifact with timestamp provenance" (this step's own user-visible
        // outcome, verbatim) -- real JSON persistence, round-tripped, never merely asserted in memory.
        var store = new JsonMixReconstructionSelfConsistencyReportStore(Path.Combine(_runDirectory, "reports"));
        var savedPath = await store.SaveAsync(report, CancellationToken.None);
        Assert.True(File.Exists(savedPath));

        var reloaded = await store.LoadAsync(savedPath, CancellationToken.None);
        Assert.Equal(report.Id, reloaded.Id);
        Assert.Equal(report.ModelId, reloaded.ModelId);
        Assert.Equal(report.ReconstructionMetric.SignalToDistortionRatioDb, reloaded.ReconstructionMetric.SignalToDistortionRatioDb);
        Assert.Equal(report.Decision, reloaded.Decision);
        AssertDecisionNeverClaimsQualityParity(reloaded.Decision);
    }

    /// <summary>
    /// The step's own named failure/edge case, verbatim: reject fabricating any claim of "matches
    /// reference/professional separation quality" anywhere in the report's own Decision text. Checks every
    /// phrase a parity claim could plausibly use, not merely one literal string.
    /// </summary>
    private static void AssertDecisionNeverClaimsQualityParity(string decision)
    {
        Assert.False(string.IsNullOrWhiteSpace(decision));

        string[] forbiddenParityPhrases =
        [
            "matches reference separation quality",
            "matches professional separation quality",
            "reference-quality separation",
            "professional-quality separation",
            "proven separation quality",
            "separation quality is proven",
            "quality parity",
        ];

        foreach (var phrase in forbiddenParityPhrases)
        {
            Assert.DoesNotContain(phrase, decision, StringComparison.OrdinalIgnoreCase);
        }

        // Positive assertion: the Decision text must itself disclose that this is a self-consistency
        // proxy, not a quality measurement -- never silently omitted.
        Assert.Contains("self-consistency", decision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does NOT measure separation quality", decision, StringComparison.Ordinal);
    }

    /// <summary>
    /// Negative path: a real separator that genuinely produces zero stems (a fake, in-process
    /// <see cref="ISpectrogramStemSeparator"/> standing in for an otherwise-valid real separator returning
    /// an empty manifest -- never a claim this is itself a real model run) must be rejected before any mask
    /// arithmetic or metric computation is attempted, rather than silently reporting a vacuous "perfect"
    /// self-consistency score.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_SeparatorProducesZeroStems_ThrowsSeparationQualityInvalidInputException_NegativePath()
    {
        var wavPath = Path.Combine(_runDirectory, "stereo-fixture.wav");
        var source = await WriteRealStereoFixtureAsync(wavPath);

        var evaluator = new MixReconstructionSelfConsistencyEvaluator(new WavAudioDecoder());
        var thresholds = new SeparationQualityThresholds(
            MinSignalToDistortionRatioDb: -100.0,
            MinCorrelationCoefficient: -1.0);

        var ex = await Assert.ThrowsAsync<SeparationQualityInvalidInputException>(() => evaluator.EvaluateAsync(
            Guid.NewGuid(), 0L, new ZeroStemSeparator(), SherpaOnnxSpleeterModelAsset.Bundle,
            SherpaOnnxSpleeterModelAsset.Recipe, source, thresholds, CancellationToken.None));

        Assert.Contains("zero stems", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stand-in <see cref="ISpectrogramStemSeparator"/> that always returns an empty manifest --
    /// proves the evaluator's own explicit zero-stem guard, independent of any real model weights.</summary>
    private sealed class ZeroStemSeparator : ISpectrogramStemSeparator
    {
        public Task<StemManifest> SeparateAsync(
            SongAsset source, SpectrogramModelBundle modelBundle, SpectrogramSeparationRecipe recipe,
            IProgress<SeparationProgress>? progress, CancellationToken ct) =>
            Task.FromResult(new StemManifest(Guid.NewGuid(), source.Sha256, modelBundle.Archive.Id, Array.Empty<StemAsset>(), Array.Empty<string>()));
    }
}
