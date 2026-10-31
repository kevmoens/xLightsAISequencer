using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement.Harness;

/// <summary>
/// Build-plan Phase 7b: every input <see cref="SequenceMetricBattery"/> may use to compute architecture
/// §9's numeric battery for one rendered (or, when <see cref="Frames"/> is empty, predicted-fallback)
/// sequence. <see cref="Frames"/>/<see cref="TotalDisplayPointCount"/> are the only required inputs
/// (real <see cref="PreviewFrame"/>s from <c>XlightsAI.Studio.Rendering.RenderPipeline</c>); every other
/// field is optional, mirroring <see cref="ChoreographyQualityContext"/>'s own "every analysis input is
/// independently optional, report Weight=0 with an explicit reason rather than fabricate a score" rule.
/// </summary>
/// <param name="Frames">Real decoded frames, ordered by <see cref="PreviewFrame.TimeTicks"/>, covering the whole sequence (or the window under evaluation).</param>
/// <param name="TotalDisplayPointCount">The show's own total addressable point count -- the same denominator <see cref="DisplayMetricsCalculator"/> already uses.</param>
/// <param name="CategoryByModel">Each model's <see cref="ModelCategory"/>, for the pixel-heavy-prop duty-cycle metric. <see langword="null"/> when layout category data was not supplied.</param>
/// <param name="BeatGrid">The song's analyzed beat grid, for the effect-boundary-deviation and beat-hit-rate metrics.</param>
/// <param name="BarDurationTicks">One bar's duration in ticks, for the "longest no-model-change stretch" metric's ≤1-bar target. <see langword="null"/> when no bar grid is available.</param>
/// <param name="Sections">Per-section declared ceiling/palette-band data, for the per-section-energy and active-palette-slots metrics.</param>
/// <param name="DeclaredBlackoutWindows">Time windows where a fully-black frame is a deliberate, declared choice (e.g. a <2s outro punctuation) rather than a defect -- never flagged by the undeclared-black-frames hard gate.</param>
/// <param name="EffectBoundaryTicks">Every compiled effect's own start tick, for the effect-boundary-deviation-from-beat and beat-hit-rate metrics.</param>
/// <param name="PeakTimestampTicks">Timestamps of designated musical peak moments, for the pairwise-peak-similarity metric.</param>
/// <param name="RmsEnvelope">Smoothed RMS energy samples (time tick, normalized RMS in <c>[0,1]</c>), for the lit-pixel-vs-RMS correlation metric.</param>
public sealed record HarnessSequenceContext(
    IReadOnlyList<PreviewFrame> Frames,
    int TotalDisplayPointCount,
    IReadOnlyDictionary<ModelId, ModelCategory>? CategoryByModel = null,
    BeatGrid? BeatGrid = null,
    long? BarDurationTicks = null,
    IReadOnlyList<HarnessSection>? Sections = null,
    IReadOnlyList<TimeWindow>? DeclaredBlackoutWindows = null,
    IReadOnlyList<long>? EffectBoundaryTicks = null,
    IReadOnlyList<long>? PeakTimestampTicks = null,
    IReadOnlyList<(long TimeTicks, double NormalizedRms)>? RmsEnvelope = null);
