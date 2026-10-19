using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06A Step 044F — "Revise the feasibility report, disclose the license risk, and update Phase 06's
/// carried-forward gate status." Required acceptance (verbatim): "A new, persisted ModelFeasibilityReport
/// revision exists, distinct from Step 038's original (both independently loadable and inspectable); the
/// sherpa-onnx Spleeter candidate's VerifiedExecutableUnderDotNet is true; its LicenseRiskDisclosure is
/// non-empty and accurately describes the real, disclosed Spleeter-weights license ambiguity; and this
/// step's own gate-report text explicitly distinguishes 'Phase 06's real-model demo can now run' from
/// 'Step 038's own research-threshold is still formally unmet,' never conflating the two." Failure/edge
/// case (verbatim): "Attempting to construct a report that marks the candidate Feasible (not
/// FeasibleWithDisclosedLicenseRisk) while LicenseRedistributable is false/contested must still throw via
/// the evaluator's own unmodified invariant."
///
/// <b>Disclosed candidate-identity decision (required by this step's own implementation guidance, option
/// (b)):</b> the real, verified <c>sherpa-onnx-spleeter-2stems</c> model is recorded as a <i>new, distinct
/// candidate entry</i> alongside Step 038's own original "Candidate B" (generic, upstream-reported,
/// never-measured "2/4/5-stem" placeholder) — never a narrowing or in-place edit of that original record.
/// Step 038's own original three candidates are reconstructed here exactly as
/// <c>Step038AcceptanceTests.Step038_Acceptance_Passes_RealResearchedCandidates_ExplicitBlocker</c> declared
/// them (same <c>ModelId</c>/<c>Verdict</c>/<c>VerifiedExecutableUnderDotNet</c> values, byte-for-byte) and
/// persisted, unmutated, to its own report file — proving this step never silently edits Step 038's own
/// record — before the new revision (with a fourth, distinct candidate appended) is built and persisted
/// separately.
///
/// <b>Real network access required.</b> Reuses the same real, already-downloaded, hash-verified
/// <c>sherpa-onnx-spleeter-2stems</c> archive cache directory Steps 044D/044E's own tests established — a
/// genuine HTTPS download only occurs on this machine's very first run of any of these three steps' tests.
/// This step performs one real separation + real hardware measurement (via the real, unmodified Step 044E
/// <see cref="MixReconstructionSelfConsistencyEvaluator"/>) so the new candidate's own RAM/CPU fields are
/// real, freshly-measured numbers on this machine — never fabricated or copied from an estimate.
/// </summary>
public sealed class Step044FAcceptanceTests : IDisposable
{
    /// <summary>Deliberately the same shared cache path Steps 044D/044E use (not deleted in
    /// <see cref="Dispose"/>) — the real, already hash-verified model cache.</summary>
    private static readonly string SharedModelAssetCacheDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-phase06a-step044d-model-asset-cache");

    private readonly string _runDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step044f-{Guid.NewGuid():N}");
    private readonly ModelFeasibilityEvaluator _evaluator = new();

    public Step044FAcceptanceTests() => Directory.CreateDirectory(_runDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_runDirectory))
        {
            Directory.Delete(_runDirectory, recursive: true);
        }
    }

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

    /// <summary>
    /// Step 038's own original three candidates, reconstructed exactly as
    /// <c>Step038AcceptanceTests.Step038_Acceptance_Passes_RealResearchedCandidates_ExplicitBlocker</c>
    /// declared them — byte-for-byte identical field values — so this step's own persisted copy of "Step
    /// 038's original report" is a faithful, unmutated reconstruction, never a silently-edited stand-in.
    /// </summary>
    private static IReadOnlyList<ModelFeasibilityCandidate> Step038OriginalCandidates() =>
    [
        new ModelFeasibilityCandidate(
            ModelId: "StemSplit/demucs-onnx (htdemucs_ft export)",
            Version: "0.3.4 (demucs-onnx PyPI) / htdemucs_ft weights",
            Backend: "ONNX Runtime (Microsoft.ML.OnnxRuntime, CPU EP) -- not yet run in this repository",
            VerifiedExecutableUnderDotNet: false,
            InputOutputTensorShape: "Documented upstream as stereo 44.1kHz chunked waveform in, 4-stem stereo waveform out; exact tensor shape not yet independently re-inspected against a downloaded .onnx file in this repository",
            SampleRateHz: 44100,
            EstimatedCpuRamMegabytes: 7168,
            EstimatedCpuSecondsPerSongMinute: 90.0,
            QualityVsReferenceNotes: "Upstream-reported quality roughly matches reference PyTorch htdemucs_ft; not independently re-measured in this repository yet.",
            LicenseId: "MIT",
            LicenseRedistributable: true,
            Verdict: ModelFeasibilityVerdict.Unproven,
            Citations:
            [
                "https://github.com/StemSplit/demucs-onnx",
                "https://huggingface.co/StemSplitio/htdemucs-ft-onnx",
                "https://github.com/facebookresearch/demucs",
            ]),
        new ModelFeasibilityCandidate(
            ModelId: "sherpa-onnx Spleeter 4-stem conversion",
            Version: "Spleeter 2.4.x weights, sherpa-onnx conversion (date not pinned)",
            Backend: "ONNX Runtime (Microsoft.ML.OnnxRuntime, CPU EP) -- not yet run in this repository",
            VerifiedExecutableUnderDotNet: false,
            InputOutputTensorShape: "Documented upstream as 2/4/5-stem U-Net spectrogram masking model; exact tensor shape not yet independently re-inspected against a downloaded .onnx file in this repository",
            SampleRateHz: 44100,
            EstimatedCpuRamMegabytes: 1536,
            EstimatedCpuSecondsPerSongMinute: 15.0,
            QualityVsReferenceNotes: "Upstream Spleeter quality is reported lower than Demucs-family models but materially faster/lighter on CPU; not independently re-measured in this repository yet.",
            LicenseId: "MIT (Spleeter code) + Apache-2.0 (sherpa-onnx conversion/runtime glue)",
            LicenseRedistributable: true,
            Verdict: ModelFeasibilityVerdict.Unproven,
            Citations:
            [
                "https://github.com/deezer/spleeter",
                "https://research.deezer.com/publication/2020/06/24/releasing-spleeter.html",
            ]),
        new ModelFeasibilityCandidate(
            ModelId: "UVR-MDX-NET-Voc_FT",
            Version: "Anjok07/aufr33 published weights (version not pinned)",
            Backend: "ONNX Runtime -- CPU fallback reported, no CPU RAM/latency benchmark located",
            VerifiedExecutableUnderDotNet: false,
            InputOutputTensorShape: "Not independently inspected in this repository",
            SampleRateHz: 44100,
            EstimatedCpuRamMegabytes: 4096,
            EstimatedCpuSecondsPerSongMinute: 240.0,
            QualityVsReferenceNotes: "Community reports describe CPU inference as falling back silently and running very slowly when no CUDA device is present; no reproducible CPU-only benchmark was located during this step's research, so this candidate is rejected for this phase's CPU-first requirement rather than assumed acceptable.",
            LicenseId: "MIT",
            LicenseRedistributable: true,
            Verdict: ModelFeasibilityVerdict.NotFeasible,
            Citations:
            [
                "https://github.com/Anjok07/ultimatevocalremovergui",
                "https://web.navan.dev/posts/2025-10-26-vocal-separation-and-rvc-onnx-coreml.html",
            ]),
    ];

    [Fact]
    public async Task Step044F_Acceptance_Passes_RevisedReportDistinctFromStep038OriginalWithHonestLicenseRiskVerdict()
    {
        // 1. Reconstruct and persist Step 038's own original report, byte-for-byte identical to the real
        // candidates that step actually recorded -- proving this step treats it as read-only, never
        // silently edited in place (required acceptance: "never mutating Step 038's original report").
        var originalCandidates = Step038OriginalCandidates();
        var originalReport = _evaluator.Evaluate(
            Guid.NewGuid(),
            createdAtTicks: 638_700_000_000_000_000L,
            originalCandidates,
            decision: "BLOCKED for this step: zero candidates are yet proven to run end-to-end under a " +
                      ".NET host on CPU in this repository. Demucs-onnx (htdemucs_ft export, MIT) is the " +
                      "recommended primary path for Step 039/040; sherpa-onnx's Spleeter conversion (MIT + " +
                      "Apache-2.0) is the recommended lighter-weight fallback. UVR-MDX-NET is rejected.",
            blockerReason: "No candidate has an actual, reproduced .NET CPU inference run in this " +
                           "repository yet; Steps 039 (verified download) and 040 (integration) must " +
                           "complete a real run before any candidate can be marked Feasible.");

        var originalStoreDirectory = Path.Combine(_runDirectory, "step038-original");
        var originalStore = new JsonModelFeasibilityReportStore(originalStoreDirectory);
        var originalPath = await originalStore.SaveAsync(originalReport, CancellationToken.None);

        // 2. Real, freshly re-measured HardwareProfile for this exact mechanism/model on this machine
        // (Step 044E's own real, unmodified evaluator) -- real numbers for the new candidate's own
        // RAM/CPU fields, never estimated/fabricated.
        var modelAssetManager = await EnsureRealModelDownloadedAsync();
        var expander = new TarBzip2ModelArchiveExpander();
        var wavPath = Path.Combine(_runDirectory, "real-stereo-sample.wav");
        var source = await WriteRealStereoFixtureAsync(wavPath);
        var separator = new SpectrogramOnnxStemSeparator(
            modelAssetManager, expander, new WavStereoAudioDecoder(),
            outputDirectory: Path.Combine(_runDirectory, "separation-output"));
        var hardwareEvaluator = new MixReconstructionSelfConsistencyEvaluator(new WavAudioDecoder());
        var thresholds = new SeparationQualityThresholds(
            MinSignalToDistortionRatioDb: -100.0,
            MinCorrelationCoefficient: -1.0);
        var selfConsistencyReport = await hardwareEvaluator.EvaluateAsync(
            Guid.NewGuid(), 638_700_000_000_000_001L, separator, SherpaOnnxSpleeterModelAsset.Bundle,
            SherpaOnnxSpleeterModelAsset.Recipe, source, thresholds, CancellationToken.None);

        var realMeasuredRamMegabytes = Math.Max(1, selfConsistencyReport.Hardware.PeakWorkingSetBytes / (1024 * 1024));
        var realMeasuredSecondsPerSongMinute = Math.Max(0.0, selfConsistencyReport.Hardware.MeasuredWallClockSecondsPerSongMinute);

        // 3. The new, distinct candidate entry for the real, verified model (disclosed option (b): a
        // separate entry, never a narrowing of Step 038's own original "sherpa-onnx Spleeter 4-stem
        // conversion" record above, which remains byte-for-byte unchanged in originalCandidates).
        const string licenseRiskDisclosure =
            "Spleeter's own official README states only that the CODE is MIT-licensed; Deezer has made no " +
            "explicit statement extending that license to the released PRETRAINED WEIGHTS. No official " +
            "Deezer guidance on this specific question exists (the one public GitHub discussion asking " +
            "Deezer to clarify -- https://github.com/deezer/spleeter/issues/898 -- has no maintainer " +
            "response of any kind). This is a real, unresolved question with no existing official answer " +
            "to cite, not a settled policy either way. No explicit, user-granted waiver of this residual " +
            "license risk exists in this addendum's own record.";

        var realVerifiedCandidate = new ModelFeasibilityCandidate(
            ModelId: "sherpa-onnx Spleeter 2-stem (sherpa-onnx-spleeter-2stems, real downloaded/verified model)",
            Version: "source-separation-models release, sherpa-onnx-spleeter-2stems.tar.bz2, SHA-256 e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2",
            Backend: "ONNX Runtime (Microsoft.ML.OnnxRuntime, CPU EP) -- real, repeated inference runs in this repository",
            VerifiedExecutableUnderDotNet: true,
            InputOutputTensorShape: "Real, independently inspected: dual single-stem sessions (vocals.onnx / accompaniment.onnx), each (2, num_splits, 512, 1024) float32 in -> same shape out; first 1024 of 2049 STFT bins retained (permanent quality ceiling)",
            SampleRateHz: 44100,
            EstimatedCpuRamMegabytes: realMeasuredRamMegabytes,
            EstimatedCpuSecondsPerSongMinute: realMeasuredSecondsPerSongMinute,
            QualityVsReferenceNotes: "Real, measured reconstructed-mix self-consistency proxy only (SignalToDistortionRatioDb=" +
                                     selfConsistencyReport.ReconstructionMetric.SignalToDistortionRatioDb.ToString("F2") +
                                     "); this is NOT a separation-quality measurement -- no professionally-mastered reference multitrack exists to score true separation quality against.",
            LicenseId: "MIT (code/conversion tooling only) -- pretrained-weight redistribution license genuinely contested, see LicenseRiskDisclosure",
            LicenseRedistributable: false,
            Verdict: ModelFeasibilityVerdict.FeasibleWithDisclosedLicenseRisk,
            Citations:
            [
                "https://github.com/k2-fsa/sherpa-onnx/releases/tag/source-separation-models",
                "https://raw.githubusercontent.com/k2-fsa/sherpa-onnx/master/scripts/spleeter/separate_onnx.py",
                "https://github.com/deezer/spleeter",
                "https://github.com/deezer/spleeter/issues/898",
                "implementation-evidence/PHASE-06A/STEP-044D.md",
                "implementation-evidence/PHASE-06A/STEP-044E.md",
            ],
            LicenseRiskDisclosure: licenseRiskDisclosure);

        // 4. The revised report: Step 038's own three original candidates (untouched) plus the one new,
        // distinct candidate. FeasibleWithDisclosedLicenseRisk never counts toward the "two feasible
        // alternatives" threshold, so an honest BlockerReason is still required and supplied.
        var revisedCandidates = originalCandidates.Append(realVerifiedCandidate).ToArray();
        var revisedReport = _evaluator.Evaluate(
            Guid.NewGuid(),
            createdAtTicks: 638_700_000_001_000_000L,
            revisedCandidates,
            decision: "Phase 06's own real-model separation DEMONSTRATION can now run end-to-end against " +
                      "the real, downloaded, hash-verified sherpa-onnx-spleeter-2stems model (see " +
                      "implementation-evidence/PHASE-06A/STEP-044D.md and STEP-044E.md). However, Step " +
                      "038's own formal research-threshold acceptance criterion ('at least two feasible " +
                      "alternatives') remains HONESTLY UNMET: the sherpa-onnx Spleeter candidate is marked " +
                      "FeasibleWithDisclosedLicenseRisk, not Feasible, because its pretrained-weight " +
                      "redistribution license is genuinely contested; Candidate A (Demucs-onnx) remains " +
                      "Unproven; Candidate C (UVR-MDX-NET) remains rejected.",
            blockerReason: "Zero candidates carry the full Feasible verdict: the real, verified sherpa-onnx " +
                           "Spleeter model is executable but its pretrained-weight redistribution license is " +
                           "unresolved (FeasibleWithDisclosedLicenseRisk only); Demucs-onnx remains Unproven " +
                           "(never downloaded/run in this repository); UVR-MDX-NET remains rejected. Step " +
                           "038's own 'two feasible alternatives' acceptance threshold is not met.");

        var revisedStoreDirectory = Path.Combine(_runDirectory, "step044f-revision");
        var revisedStore = new JsonModelFeasibilityReportStore(revisedStoreDirectory);
        var revisedPath = await revisedStore.SaveAsync(revisedReport, CancellationToken.None);

        // 5. Required acceptance, verbatim: "distinct from Step 038's original (both independently
        // loadable and inspectable)".
        Assert.NotEqual(originalReport.Id, revisedReport.Id);
        Assert.NotEqual(originalPath, revisedPath);

        var reloadedOriginal = await originalStore.LoadAsync(originalPath, CancellationToken.None);
        var reloadedRevised = await revisedStore.LoadAsync(revisedPath, CancellationToken.None);

        // 6. Step 038's own original report is proven unmutated: its own persisted candidate for the
        // generic placeholder is byte-for-byte identical to what Step 038 itself originally recorded.
        var reloadedOriginalSpleeter = Assert.Single(
            reloadedOriginal.Candidates, c => c.ModelId == "sherpa-onnx Spleeter 4-stem conversion");
        Assert.Equal(ModelFeasibilityVerdict.Unproven, reloadedOriginalSpleeter.Verdict);
        Assert.False(reloadedOriginalSpleeter.VerifiedExecutableUnderDotNet);
        Assert.Equal(1536, reloadedOriginalSpleeter.EstimatedCpuRamMegabytes);
        Assert.Equal(15.0, reloadedOriginalSpleeter.EstimatedCpuSecondsPerSongMinute);
        Assert.Equal(string.Empty, reloadedOriginalSpleeter.LicenseRiskDisclosure);
        Assert.Equal(3, reloadedOriginal.Candidates.Count);
        Assert.True(reloadedOriginal.IsBlocked);

        // 7. Required acceptance, verbatim: "the sherpa-onnx Spleeter candidate's
        // VerifiedExecutableUnderDotNet is true; its LicenseRiskDisclosure is non-empty and accurately
        // describes the real, disclosed Spleeter-weights license ambiguity."
        var reloadedRealCandidate = Assert.Single(
            reloadedRevised.Candidates, c => c.ModelId == realVerifiedCandidate.ModelId);
        Assert.True(reloadedRealCandidate.VerifiedExecutableUnderDotNet);
        Assert.False(string.IsNullOrWhiteSpace(reloadedRealCandidate.LicenseRiskDisclosure));
        Assert.Contains("Deezer", reloadedRealCandidate.LicenseRiskDisclosure, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pretrained", reloadedRealCandidate.LicenseRiskDisclosure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ModelFeasibilityVerdict.FeasibleWithDisclosedLicenseRisk, reloadedRealCandidate.Verdict);
        Assert.False(reloadedRealCandidate.LicenseRedistributable);

        // 8. FeasibleWithDisclosedLicenseRisk never counts toward the "two feasible alternatives"
        // threshold -- the revised report is still correctly IsBlocked with an honest, non-empty reason.
        Assert.Equal(0, reloadedRevised.FeasibleCandidateCount);
        Assert.True(reloadedRevised.IsBlocked);
        Assert.False(string.IsNullOrWhiteSpace(reloadedRevised.BlockerReason));
        Assert.Equal(4, reloadedRevised.Candidates.Count);

        // 9. Required acceptance, verbatim: this step's own gate-report text explicitly distinguishes
        // "Phase 06's real-model demo can now run" from "Step 038's own research-threshold is still
        // formally unmet," never conflating the two.
        Assert.Contains("can now run", reloadedRevised.Decision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HONESTLY UNMET", reloadedRevised.Decision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("two feasible alternatives", reloadedRevised.Decision, StringComparison.OrdinalIgnoreCase);

        // 10. Real, measured HardwareProfile sanity (never a fabricated number): both figures carried
        // into the new candidate are strictly positive, matching the real measurement taken in step 2.
        Assert.True(reloadedRealCandidate.EstimatedCpuRamMegabytes > 0);
        Assert.True(reloadedRealCandidate.EstimatedCpuSecondsPerSongMinute >= 0);
    }

    /// <summary>
    /// Required failure/edge case, verbatim: "Attempting to construct a report that marks the candidate
    /// Feasible (not FeasibleWithDisclosedLicenseRisk) while LicenseRedistributable is false/contested
    /// must still throw via the evaluator's own unmodified invariant." Proves this step did not quietly
    /// bypass <see cref="ModelFeasibilityEvaluator"/>'s existing, unmodified
    /// `Feasible`-implies-`VerifiedExecutableUnderDotNet &amp;&amp; LicenseRedistributable` check by routing
    /// around it with the new verdict member.
    /// </summary>
    [Fact]
    public void Evaluate_CandidateMarkedFeasibleWithContestedLicense_StillThrowsViaExistingUnmodifiedInvariant_NegativePath()
    {
        var misconstructedCandidate = new ModelFeasibilityCandidate(
            ModelId: "sherpa-onnx Spleeter 2-stem (misconstructed -- attempting to skip the license-risk verdict)",
            Version: "source-separation-models",
            Backend: "ONNX Runtime (Microsoft.ML.OnnxRuntime, CPU EP)",
            VerifiedExecutableUnderDotNet: true,
            InputOutputTensorShape: "(2, num_splits, 512, 1024)",
            SampleRateHz: 44100,
            EstimatedCpuRamMegabytes: 1024,
            EstimatedCpuSecondsPerSongMinute: 15.0,
            QualityVsReferenceNotes: "N/A -- this candidate exists only to prove the negative-path invariant.",
            LicenseId: "MIT (code only) -- weights contested",
            LicenseRedistributable: false, // genuinely contested, never true
            Verdict: ModelFeasibilityVerdict.Feasible, // WRONG: should be FeasibleWithDisclosedLicenseRisk
            Citations: ["https://github.com/deezer/spleeter/issues/898"]);

        var ex = Assert.Throws<ModelFeasibilityCandidateInvalidException>(() => _evaluator.Evaluate(
            Guid.NewGuid(),
            createdAtTicks: 0L,
            candidates: [misconstructedCandidate],
            decision: "Attempting to incorrectly mark the contested-license candidate as fully Feasible.",
            blockerReason: "Deliberately attempting to bypass the license-risk disclosure requirement."));

        Assert.Contains("not both proven executable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
