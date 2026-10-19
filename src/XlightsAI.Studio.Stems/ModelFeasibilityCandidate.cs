namespace XlightsAI.Studio.Stems;

/// <summary>
/// One inspected stem-separation model/backend candidate for Step 038's feasibility ADR. Every field is
/// a disclosed, inspectable claim — not a generic placeholder — per Section 2's "Verification" rule
/// ("An XML parse pass does not prove xLights renders an effect", generalized here to "a package
/// existing does not prove a model runs").
/// </summary>
/// <param name="ModelId">Stable identifier for this candidate (e.g. upstream repo/package name).</param>
/// <param name="Version">Pinned version/revision actually inspected.</param>
/// <param name="Backend">Concrete .NET-reachable inference backend (e.g. "ONNX Runtime
/// (Microsoft.ML.OnnxRuntime, CPU)"), never a generic "ONNX" claim.</param>
/// <param name="VerifiedExecutableUnderDotNet">True only when a real run (or a directly-cited,
/// independently reproducible upstream report of one) under a .NET host was observed — never inferred
/// merely from "ONNX Runtime has C# bindings".</param>
/// <param name="InputOutputTensorShape">Documented input/output tensor shape and sample rate/chunking
/// contract, inspected from the model source, not guessed.</param>
/// <param name="SampleRateHz">Sample rate the model's published weights were trained/exported for.</param>
/// <param name="EstimatedCpuRamMegabytes">Measured or directly-cited CPU working-set estimate for a
/// representative song length.</param>
/// <param name="EstimatedCpuSecondsPerSongMinute">Measured or directly-cited CPU wall-clock seconds of
/// inference per one minute of input audio.</param>
/// <param name="QualityVsReferenceNotes">Disclosed, non-fabricated quality comparison notes — never a
/// claim of "perfect" or "parity" the citations do not support.</param>
/// <param name="LicenseId">SPDX-style license identifier for the model weights and inference code.</param>
/// <param name="LicenseRedistributable">True only when the license permits bundling/redistributing the
/// weights with this application; false for "personal/research only" or unclear terms.</param>
/// <param name="Verdict">See <see cref="ModelFeasibilityVerdict"/>.</param>
/// <param name="Citations">Source URLs or upstream file paths backing the claims above.</param>
/// <param name="LicenseRiskDisclosure">New, additive, PHASE-06A Step 044F field (default empty string —
/// non-breaking for every prior candidate literal). Non-empty only when <see cref="Verdict"/> is
/// <see cref="ModelFeasibilityVerdict.FeasibleWithDisclosedLicenseRisk"/>: a disclosed, honest statement
/// of exactly which pretrained-weight redistribution question remains genuinely unresolved and why
/// (never a settled legal opinion) — see <c>implementation-evidence/PHASE-06A/STEP-044F.md</c>.</param>
public sealed record ModelFeasibilityCandidate(
    string ModelId,
    string Version,
    string Backend,
    bool VerifiedExecutableUnderDotNet,
    string InputOutputTensorShape,
    int SampleRateHz,
    long EstimatedCpuRamMegabytes,
    double EstimatedCpuSecondsPerSongMinute,
    string QualityVsReferenceNotes,
    string LicenseId,
    bool LicenseRedistributable,
    ModelFeasibilityVerdict Verdict,
    IReadOnlyList<string> Citations,
    string LicenseRiskDisclosure = "");
