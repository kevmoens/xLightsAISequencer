namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 032's own "configurable sensitivity" (this step's own title) knobs, layered on top of the
/// shared <see cref="RhythmSettings"/> windowing already established by Step 031. Recorded alongside
/// every <see cref="OnsetAnalysis"/> it produces — part of the cache key (Section 4: "every cache key
/// includes algorithm/model/settings versions") exactly like <see cref="RhythmSettings"/> itself.
/// </summary>
/// <param name="SensitivityMultiplier">
/// Scales the adaptive peak-picking threshold (local mean + <c>SensitivityMultiplier</c> * local
/// standard deviation of normalized spectral flux, a standard adaptive-threshold onset-detection
/// shape). Must be positive. Lower values are more sensitive (more candidate onsets pass the
/// threshold, trading precision for recall); higher values are less sensitive (fewer, stronger-only
/// onsets, trading recall for precision) — this is the step's own "configurable sensitivity."
/// </param>
/// <param name="MinInterOnsetSamples">
/// Minimum sample-domain spacing enforced between two accepted onsets (a refractory period): when two
/// candidates fall closer together than this, only the stronger one is kept. Must be non-negative;
/// <c>0</c> disables the refractory period entirely. Guards against one physical transient producing
/// several adjacent frame-level candidates due to overlapping analysis windows.
/// </param>
public sealed record OnsetDetectionSettings(float SensitivityMultiplier, int MinInterOnsetSamples);
