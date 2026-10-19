namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 044's own title, verbatim: "...establish supported hardware." A single real,
/// measured (never estimated) hardware/performance observation for one machine, taken while the real
/// <see cref="OnnxStemSeparator"/> mechanism actually ran. Replaces Step 038's own
/// <see cref="ModelFeasibilityCandidate.EstimatedCpuRamMegabytes"/>/
/// <see cref="ModelFeasibilityCandidate.EstimatedCpuSecondsPerSongMinute"/> "estimated/upstream-reported"
/// figures with real measurements for the one real mechanism actually integrated so far (the synthetic
/// ONNX fixture's inference mechanism -- Step 038's own BLOCKED production-model verdict is unchanged;
/// see <c>implementation-evidence/PHASE-06/STEP-044.md</c> §3). Only one machine's profile is recorded
/// per report -- Section 2's "no undocumented claim that all ... behave identically" rule applies
/// directly: "supported" means "measured here," never "guaranteed everywhere."
/// </summary>
/// <param name="OsDescription">The real <see cref="System.Runtime.InteropServices.RuntimeInformation.OSDescription"/> of the machine this profile was measured on.</param>
/// <param name="ProcessArchitecture">The real <see cref="System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture"/> value.</param>
/// <param name="ProcessorCount">Real <see cref="Environment.ProcessorCount"/> (logical CPU count) at measurement time.</param>
/// <param name="TotalAvailableMemoryBytes">Real <see cref="GC.GetGCMemoryInfo()"/> total available memory to the process's GC, in bytes -- the closest cross-platform, dependency-free proxy for machine RAM available in .NET.</param>
/// <param name="PeakWorkingSetBytes">Real <see cref="System.Diagnostics.Process.PeakWorkingSet64"/> for this process, sampled immediately after the benchmarked run(s) completed.</param>
/// <param name="MeasuredWallClockSecondsPerSongMinute">Real measured wall-clock seconds of
/// <see cref="IStemSeparator.SeparateAsync"/> execution per one minute of input audio, averaged across
/// every clip the evaluator ran (CPU-only, this machine, this mechanism -- not a production-model
/// quality/performance claim; see Step 038's own disclosed BLOCKED feasibility verdict, unchanged by
/// this step).</param>
public sealed record HardwareProfile(
    string OsDescription,
    string ProcessArchitecture,
    int ProcessorCount,
    long TotalAvailableMemoryBytes,
    long PeakWorkingSetBytes,
    double MeasuredWallClockSecondsPerSongMinute);
