using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// Default <see cref="IRenderEvaluationGate"/>. Compiles <see cref="CandidateAttempt.Gestures"/> into an
/// ephemeral, never-persisted <see cref="SequenceDocument"/> tagged with Step 085's own
/// <see cref="LocalGesturePreviewRenderer.PreviewGestureParameterKey"/> hint, renders it through the real
/// <see cref="ILocalPreviewRenderer"/>/<see cref="IDisplayMetricsCalculator"/> already accepted by Steps
/// 085/088, and reports the result as render-derived <see cref="MetricValue"/> evidence.
/// <para>
/// <b>Gesture-to-preview mapping (disclosed).</b> PHASE-10's own <see cref="TransitionGesture"/> (intensity
/// ramp over a window across one or more target props) carries no step/hold/pop distinction of its own --
/// <see cref="LocalGesturePreviewRenderer"/>'s own three-gesture vocabulary (Step 085) was designed against a
/// different, not-yet-compiled effect shape. Every non-<see cref="TransitionKind.Blackout"/> gesture is mapped
/// to a <see cref="LocalGesturePreviewRenderer.HoldGestureValue"/> preview effect, lit for its own
/// <see cref="TransitionGesture.Window"/>, on each of its own <see cref="TransitionGesture.TargetProps"/> --
/// the only one of the three vocabulary members that needs no further parameter (<see cref="LocalGesturePreviewRenderer.PathIdParameterKey"/>
/// a <c>Step</c> gesture would require) this record does not carry, and the most faithful of the three to "a
/// prop is lit for this gesture's own duration," which is true of every <see cref="TransitionKind"/> except
/// <see cref="TransitionKind.Blackout"/>. A <see cref="TransitionKind.Blackout"/> gesture is deliberately
/// compiled to <i>no</i> preview effect at all -- it is an intentional, legal darkening (PHASE-10's own
/// required acceptance, verbatim: "musical break can intentionally darken all permitted props"), not a gap
/// this renderer failed to resolve, so it must never be reported as an unsupported/unrenderable effect.
/// </para>
/// </summary>
public sealed class PreviewRenderEvaluationGate : IRenderEvaluationGate
{
    /// <summary>This gate's own metric name for its hard render-success gate.</summary>
    public const string RenderPreviewMetricName = "RenderPreview";

    private readonly ILocalPreviewRenderer _renderer;
    private readonly IDisplayMetricsCalculator _metricsCalculator;

    public PreviewRenderEvaluationGate(ILocalPreviewRenderer renderer, IDisplayMetricsCalculator metricsCalculator)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _metricsCalculator = metricsCalculator ?? throw new ArgumentNullException(nameof(metricsCalculator));
    }

    public async Task<IReadOnlyList<MetricValue>> EvaluateAsync(LoopRun run, CandidateAttempt candidate, SpatialDisplayGraph layout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(layout);
        ct.ThrowIfCancellationRequested();

        var gestures = candidate.Gestures ?? [];

        // Candidate.Violations / an upstream malformed generation is already reported by the "legal
        // effects" hard gate (GestureQualityEvaluator) -- this gate's own concern is narrower: can what
        // *was* generated actually be shown on the real display geometry. Nothing to render is not itself
        // a render failure (there is nothing broken to report), so it passes trivially; the upstream gate
        // still blocks the candidate on its own already-disclosed grounds.
        if (gestures.Count == 0)
        {
            return
            [
                new MetricValue(RenderPreviewMetricName, 1.0, Weight: 1.0, HardGate: true, "No gestures to render."),
            ];
        }

        var effects = CompilePreviewEffects(gestures);
        var document = new SequenceDocument(candidate.Id, Revision: 0, effects);

        List<PreviewFrame> frames;
        try
        {
            frames = await CollectFramesAsync(_renderer, document, layout, run.Scope.Time, ct).ConfigureAwait(false);
        }
        catch (RenderPreviewInvalidException ex)
        {
            // A malformed render window is itself evidence this candidate cannot currently be rendered --
            // this step's own required acceptance, verbatim: "Broken/unrenderable proposal cannot
            // auto-apply; clear diagnostics." Reported as a failed gate, not rethrown, so the caller never
            // crashes evaluating a candidate; diagnosing *why* a run's own scope is malformed is a
            // different, already-covered concern (LoopRun.Validate).
            return
            [
                new MetricValue(RenderPreviewMetricName, 0.0, Weight: 1.0, HardGate: true, $"Preview render could not run: {ex.Message}"),
            ];
        }

        var unresolvedIds = frames.SelectMany(f => f.UnsupportedEffectIds).Distinct().ToArray();
        var renderable = unresolvedIds.Length == 0;

        var metrics = new List<MetricValue>
        {
            new(
                RenderPreviewMetricName,
                renderable ? 1.0 : 0.0,
                Weight: 1.0,
                HardGate: true,
                renderable
                    ? $"{effects.Count} compiled preview effect(s) across {frames.Count} sampled frame(s) all resolved against the display layout."
                    : $"{unresolvedIds.Length} of {effects.Count} compiled preview effect(s) could not be resolved against the display layout (target prop missing from layout, or window outside the layout's own geometry)."),
        };

        metrics.AddRange(ComputeSoftMetrics(frames, layout));

        return metrics;
    }

    private static async Task<List<PreviewFrame>> CollectFramesAsync(
        ILocalPreviewRenderer renderer, SequenceDocument document, SpatialDisplayGraph layout, TimeWindow window, CancellationToken ct)
    {
        var frames = new List<PreviewFrame>();
        await foreach (var frame in renderer.PreviewAsync(document, layout, window, ct).ConfigureAwait(false))
        {
            frames.Add(frame);
        }

        return frames;
    }

    private IReadOnlyList<MetricValue> ComputeSoftMetrics(IReadOnlyList<PreviewFrame> frames, SpatialDisplayGraph layout)
    {
        if (frames.Count == 0 || layout.Nodes.Count == 0)
        {
            return
            [
                NoData("RenderCoverage", "no sampled preview frames or no known display points to compare against."),
                NoData("RenderBrightness", "no sampled preview frames or no known display points to compare against."),
                NoData("RenderMotion", "no sampled preview frames or no known display points to compare against."),
                NoData("RenderDensity", "no sampled preview frames or no known display points to compare against."),
            ];
        }

        DisplayMetrics metrics;
        try
        {
            metrics = _metricsCalculator.Compute(frames, layout.Nodes.Count);
        }
        catch (DisplayMetricsInvalidException ex)
        {
            var reason = $"display metrics could not be computed: {ex.Message}";
            return
            [
                NoData("RenderCoverage", reason),
                NoData("RenderBrightness", reason),
                NoData("RenderMotion", reason),
                NoData("RenderDensity", reason),
            ];
        }

        return
        [
            new MetricValue("RenderCoverage", Clamp01(metrics.CoverageRatio), Weight: 1.0, HardGate: false,
                $"{metrics.CoverageRatio:P0} of {layout.Nodes.Count} known display point(s) illuminated at least once."),
            new MetricValue("RenderBrightness", Clamp01(metrics.MeanBrightness), Weight: 1.0, HardGate: false,
                $"Mean normalized luma across every illuminated point: {metrics.MeanBrightness:F2}."),
            new MetricValue("RenderMotion", Clamp01(metrics.SpatialMotion), Weight: 1.0, HardGate: false,
                $"Mean illuminated-centroid movement between sampled frames: {metrics.SpatialMotion:F2} (clamped to [0,1]; raw value may exceed 1 in normalized preview-coordinate units)."),
            new MetricValue("RenderDensity", Clamp01(metrics.DensityRatio), Weight: 1.0, HardGate: false,
                $"Mean fraction of known display points illuminated per sampled frame: {metrics.DensityRatio:P0}."),
        ];
    }

    private static double Clamp01(double value) => double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.0;

    private static MetricValue NoData(string name, string reason) => new(name, 0.5, Weight: 0.0, HardGate: false, reason);

    private static IReadOnlyList<EffectInstance> CompilePreviewEffects(IReadOnlyList<TransitionGesture> gestures)
    {
        var compiled = new List<EffectInstance>();
        foreach (var gesture in gestures)
        {
            if (gesture.Kind == TransitionKind.Blackout)
            {
                continue;
            }

            foreach (var target in gesture.TargetProps)
            {
                compiled.Add(new EffectInstance(
                    Guid.NewGuid(),
                    target,
                    Layer: 0,
                    gesture.Window.StartTicks,
                    gesture.Window.EndTicks,
                    EffectRecipeRegistry.SimpleOnRecipeId,
                    EffectRecipeRegistry.SimpleOnRecipeVersion,
                    new Dictionary<string, string> { [LocalGesturePreviewRenderer.PreviewGestureParameterKey] = LocalGesturePreviewRenderer.HoldGestureValue }));
            }
        }

        return compiled;
    }
}
