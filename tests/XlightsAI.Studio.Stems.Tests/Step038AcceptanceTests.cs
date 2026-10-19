using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06 Step 038 — "Evaluate separation model licensing, .NET inference options, CPU baseline and
/// memory profile." Required acceptance (verbatim): "Decision document includes at least two feasible
/// alternatives or explicit blocker." Failure/edge case (verbatim): "Missing or incompatible model,
/// cancellation and offline mode must yield a recoverable state."
/// </summary>
public sealed class Step038AcceptanceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ModelFeasibilityEvaluator _evaluator = new();

    public Step038AcceptanceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step038-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static ModelFeasibilityCandidate FeasibleFixtureCandidate(string id) => new(
        ModelId: id,
        Version: "1.0.0-fixture",
        Backend: "Fixture ONNX Runtime (CPU)",
        VerifiedExecutableUnderDotNet: true,
        InputOutputTensorShape: "[1,2,44100] -> [4,2,44100]",
        SampleRateHz: 44100,
        EstimatedCpuRamMegabytes: 2048,
        EstimatedCpuSecondsPerSongMinute: 30.0,
        QualityVsReferenceNotes: "Fixture candidate for deterministic acceptance testing only.",
        LicenseId: "MIT",
        LicenseRedistributable: true,
        Verdict: ModelFeasibilityVerdict.Feasible,
        Citations: new[] { "https://example.invalid/fixture-candidate" });

    [Fact]
    public async Task Step038_Acceptance_Passes_TwoFeasibleCandidates_NoBlockerNeeded()
    {
        // Required acceptance, verbatim: "at least two feasible alternatives" -- proven with two
        // independently-marked-Feasible candidates and a third, rejected candidate kept for audit.
        var rejected = FeasibleFixtureCandidate("candidate-c-rejected") with
        {
            VerifiedExecutableUnderDotNet = false,
            Verdict = ModelFeasibilityVerdict.NotFeasible,
            QualityVsReferenceNotes = "Rejected: never actually executed under any .NET host.",
        };

        var candidates = new[]
        {
            FeasibleFixtureCandidate("candidate-a-feasible"),
            FeasibleFixtureCandidate("candidate-b-feasible"),
            rejected,
        };

        var reportId = Guid.NewGuid();
        var report = _evaluator.Evaluate(
            reportId,
            createdAtTicks: 638_700_000_000_000_000L,
            candidates,
            decision: "Candidates A and B are both proven feasible; proceed to Step 039 with A primary, B fallback.",
            blockerReason: null);

        Assert.False(report.IsBlocked);
        Assert.Null(report.BlockerReason);
        Assert.Equal(2, report.FeasibleCandidateCount);
        Assert.Equal(3, report.Candidates.Count);

        var store = new JsonModelFeasibilityReportStore(_tempDirectory);
        var path = await store.SaveAsync(report, CancellationToken.None);

        Assert.True(File.Exists(path));

        var reloaded = await store.LoadAsync(path, CancellationToken.None);

        Assert.Equal(report.Id, reloaded.Id);
        Assert.Equal(report.CreatedAtTicks, reloaded.CreatedAtTicks);
        Assert.Equal(report.Decision, reloaded.Decision);
        Assert.False(reloaded.IsBlocked);
        Assert.Equal(2, reloaded.FeasibleCandidateCount);
        Assert.Equal(
            candidates.Select(c => c.ModelId).OrderBy(x => x),
            reloaded.Candidates.Select(c => c.ModelId).OrderBy(x => x));
    }

    /// <summary>
    /// The real feasibility inventory this step actually researched (see
    /// implementation-evidence/PHASE-06/STEP-038.md for full citations/benchmarks). No candidate is
    /// marked Feasible here because no candidate has actually been downloaded and run end-to-end under
    /// a .NET host in this repository yet (that is Steps 039/040's own job) -- per Section 3, "do not
    /// assert that a given model runs under ONNX merely because ONNX Runtime has C# bindings." The
    /// required acceptance's "...or explicit blocker" branch is therefore the honest outcome for this
    /// step, not a fabricated "two feasible" claim.
    /// </summary>
    [Fact]
    public async Task Step038_Acceptance_Passes_RealResearchedCandidates_ExplicitBlocker()
    {
        var demucsOnnx = new ModelFeasibilityCandidate(
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
            Citations: new[]
            {
                "https://github.com/StemSplit/demucs-onnx",
                "https://huggingface.co/StemSplitio/htdemucs-ft-onnx",
                "https://github.com/facebookresearch/demucs",
            });

        var spleeterOnnx = new ModelFeasibilityCandidate(
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
            Citations: new[]
            {
                "https://github.com/deezer/spleeter",
                "https://research.deezer.com/publication/2020/06/24/releasing-spleeter.html",
            });

        var uvrMdxNet = new ModelFeasibilityCandidate(
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
            Citations: new[]
            {
                "https://github.com/Anjok07/ultimatevocalremovergui",
                "https://web.navan.dev/posts/2025-10-26-vocal-separation-and-rvc-onnx-coreml.html",
            });

        var candidates = new[] { demucsOnnx, spleeterOnnx, uvrMdxNet };

        var report = _evaluator.Evaluate(
            Guid.NewGuid(),
            createdAtTicks: 638_700_000_000_000_000L,
            candidates,
            decision: "BLOCKED for this step: zero candidates are yet proven to run end-to-end under a " +
                      ".NET host on CPU in this repository. Demucs-onnx (htdemucs_ft export, MIT) is the " +
                      "recommended primary path for Step 039/040 (best documented quality, redistributable " +
                      "license, concrete C# ONNX Runtime path); sherpa-onnx's Spleeter conversion (MIT + " +
                      "Apache-2.0) is the recommended lighter-weight fallback. UVR-MDX-NET is rejected for " +
                      "this phase: no reproducible CPU-only benchmark was found and community reports " +
                      "describe very slow CPU fallback behavior.",
            blockerReason: "No candidate has an actual, reproduced .NET CPU inference run in this " +
                           "repository yet; Steps 039 (verified download) and 040 (integration) must " +
                           "complete a real run before any candidate can be marked Feasible.");

        Assert.True(report.IsBlocked);
        Assert.Equal(0, report.FeasibleCandidateCount);
        Assert.NotNull(report.BlockerReason);
        Assert.Contains("CPU inference run", report.BlockerReason);

        var store = new JsonModelFeasibilityReportStore(_tempDirectory);
        var path = await store.SaveAsync(report, CancellationToken.None);
        var reloaded = await store.LoadAsync(path, CancellationToken.None);

        Assert.True(reloaded.IsBlocked);
        Assert.Equal(report.BlockerReason, reloaded.BlockerReason);
        Assert.Equal(3, reloaded.Candidates.Count);
        Assert.Contains(reloaded.Candidates, c => c.ModelId == demucsOnnx.ModelId);
        Assert.Contains(reloaded.Candidates, c => c.ModelId == uvrMdxNet.ModelId && c.Verdict == ModelFeasibilityVerdict.NotFeasible);
    }

    [Fact]
    public void Evaluate_FewerThanTwoFeasible_NoBlockerReason_Throws_NegativePath()
    {
        var candidates = new[]
        {
            FeasibleFixtureCandidate("only-one-feasible"),
            FeasibleFixtureCandidate("second-candidate") with
            {
                VerifiedExecutableUnderDotNet = false,
                Verdict = ModelFeasibilityVerdict.Unproven,
            },
        };

        var ex = Assert.Throws<ModelFeasibilityReportInvalidException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, candidates, "Attempting to skip the blocker.", blockerReason: null));

        Assert.Contains("at least two feasible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_FewerThanTwoFeasible_WhitespaceBlockerReason_Throws_NegativePath()
    {
        var candidates = new[] { FeasibleFixtureCandidate("only-one") };

        Assert.Throws<ModelFeasibilityReportInvalidException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, candidates, "decision", blockerReason: "   "));
    }

    [Fact]
    public void Evaluate_CandidateMarkedFeasibleButNotVerifiedExecutable_Throws_NegativePath()
    {
        var contradictory = FeasibleFixtureCandidate("contradictory") with
        {
            VerifiedExecutableUnderDotNet = false,
        };

        var ex = Assert.Throws<ModelFeasibilityCandidateInvalidException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, new[] { contradictory }, "decision", "blocker"));

        Assert.Contains("contradictory", ex.Message);
    }

    [Fact]
    public void Evaluate_CandidateMarkedFeasibleButNotRedistributable_Throws_NegativePath()
    {
        var contradictory = FeasibleFixtureCandidate("non-redistributable") with
        {
            LicenseRedistributable = false,
        };

        Assert.Throws<ModelFeasibilityCandidateInvalidException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, new[] { contradictory }, "decision", "blocker"));
    }

    [Fact]
    public void Evaluate_CandidateMissingCitations_Throws_NegativePath()
    {
        var uncited = FeasibleFixtureCandidate("uncited") with { Citations = Array.Empty<string>() };

        Assert.Throws<ModelFeasibilityCandidateInvalidException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, new[] { uncited }, "decision", "blocker"));
    }

    [Fact]
    public void Evaluate_EmptyCandidateList_Throws_NegativePath()
    {
        Assert.Throws<ModelFeasibilityReportInvalidException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, Array.Empty<ModelFeasibilityCandidate>(), "decision", "blocker"));
    }

    [Fact]
    public void Evaluate_NullCandidates_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, null!, "decision", "blocker"));
    }

    [Fact]
    public void Evaluate_NullOrWhitespaceDecision_ThrowsArgumentException()
    {
        var candidates = new[] { FeasibleFixtureCandidate("a"), FeasibleFixtureCandidate("b") };

        Assert.Throws<ArgumentException>(() =>
            _evaluator.Evaluate(Guid.NewGuid(), 0L, candidates, decision: "  ", blockerReason: null));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath()
    {
        var store = new JsonModelFeasibilityReportStore(_tempDirectory);
        var missingPath = Path.Combine(_tempDirectory, "model-feasibility-does-not-exist.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.LoadAsync(missingPath, CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ThrowsModelFeasibilityReportCorruptException_NegativePath()
    {
        Directory.CreateDirectory(_tempDirectory);
        var corruptPath = Path.Combine(_tempDirectory, "model-feasibility-corrupt.json");
        await File.WriteAllTextAsync(corruptPath, "{ this is not valid json");

        var store = new JsonModelFeasibilityReportStore(_tempDirectory);

        await Assert.ThrowsAsync<ModelFeasibilityReportCorruptException>(
            () => store.LoadAsync(corruptPath, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_NullReport_ThrowsArgumentNullException()
    {
        var store = new JsonModelFeasibilityReportStore(_tempDirectory);

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath()
    {
        var report = _evaluator.Evaluate(
            Guid.NewGuid(), 0L, new[] { FeasibleFixtureCandidate("a"), FeasibleFixtureCandidate("b") },
            "decision", blockerReason: null);
        var store = new JsonModelFeasibilityReportStore(_tempDirectory);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(report, cts.Token));
        Assert.False(File.Exists(store.GetPath(report.Id)));
    }
}
