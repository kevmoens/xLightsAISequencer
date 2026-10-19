namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Section 3's own proposed signature, kept exactly as specified. Describes what a concrete
/// machine can actually do before any install/first-run action is attempted: its OS/architecture, whether
/// CPU inference is available (Section 2: "CPU-first inference; native/ONNX only after a demonstrated and
/// legally redistributable runtime/model fit" -- this phase never requires a GPU), and whether a real,
/// locally installed xLights executable was actually found (never assumed).
/// </summary>
/// <param name="OperatingSystem"><see cref="System.Runtime.InteropServices.RuntimeInformation.OSDescription"/> of the probed machine.</param>
/// <param name="Architecture"><see cref="System.Runtime.InteropServices.RuntimeInformation.OSArchitecture"/> of the probed machine, as its <see cref="System.Runtime.InteropServices.Architecture"/> string name.</param>
/// <param name="CpuInferenceAvailable">Whether CPU-only inference is usable on this machine (no GPU requirement in this phase).</param>
/// <param name="XlightsDetected">Whether a real, on-disk xLights executable was found at the probed path.</param>
/// <param name="XlightsVersion">The detected executable's own file version, or <see langword="null"/> when <see cref="XlightsDetected"/> is <see langword="false"/> or the version could not be read.</param>
public sealed record RuntimeCapability(
    string OperatingSystem,
    string Architecture,
    bool CpuInferenceAvailable,
    bool XlightsDetected,
    string? XlightsVersion);
