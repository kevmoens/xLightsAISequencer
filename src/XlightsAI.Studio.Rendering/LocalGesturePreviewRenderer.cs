using System.Runtime.CompilerServices;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Default <see cref="ILocalPreviewRenderer"/>. PHASE-13 Step 085 ("Build fast lightweight internal preview for
/// supported primitive gestures"): a pure renderer for exactly three known simple gestures -- step, hold, pop --
/// on already-imported <see cref="SpatialDisplayGraph"/> geometry.
///
/// <para>
/// <b>Architecture decision -- gesture vocabulary source.</b> The only xLights recipe <see cref="EffectRecipeRegistry"/>
/// currently emits is <see cref="EffectRecipeRegistry.SimpleOnRecipeId"/> ("effect.on.v1"), which carries no
/// step/hold/pop distinction in its own <c>EffectFamily</c> parameter -- there is no existing compiled vocabulary
/// this step could read <see cref="EffectInstance.RecipeId"/> against to recover three distinguishable gestures.
/// Rather than inventing new real xLights effect recipes/parameters (explicitly forbidden: "do not invent xLights
/// effect parameters"), this preview-only renderer reads its own additive, non-xLights parameter key,
/// <see cref="PreviewGestureParameterKey"/>, directly off <see cref="EffectInstance.Parameters"/> -- a value this
/// fast approximate tier alone defines and consumes, never serialized into xLights XML and never a claim about
/// what RecipeId/parameters a real xLights effect requires. An <see cref="EffectInstance"/> with no recognized
/// <see cref="PreviewGestureParameterKey"/> value (including every currently-compiled "effect.on.v1" instance,
/// absent an explicit preview hint) is treated as <i>unsupported by this fast tier</i> -- correctly reported via
/// <see cref="PreviewFrame.UnsupportedEffectIds"/>, not silently guessed at.
/// </para>
///
/// <para>
/// <b>Gesture semantics</b> (fast, visually-approximate, white illumination only -- no color/brightness fidelity
/// claim is made; see PHASE-13 Section 2's "fast lightweight internal preview" scope):
/// <list type="bullet">
/// <item><description><b>Hold</b>: <see cref="EffectInstance.Target"/>'s own <see cref="DisplayNode"/> lit for the whole <c>[Start, End)</c> window.</description></item>
/// <item><description><b>Pop</b>: <see cref="EffectInstance.Target"/>'s own node lit only for the first <see cref="PopFlashFraction"/> of the window, dark afterward -- a brief flash, not a true xLights pop/flash recipe curve.</description></item>
/// <item><description><b>Step</b>: walks <see cref="SpatialPath.OrderedModels"/> (named by the effect's own <see cref="PathIdParameterKey"/> parameter, resolved against <paramref name="layout"/>) one node at a time, proportional to elapsed progress through the window.</description></item>
/// </list>
/// Any gesture whose referenced path/node cannot be resolved against <paramref name="layout"/> is <i>also</i>
/// reported as unsupported for that frame -- a missing/renamed target is never silently skipped as if it had no
/// effect at all.
/// </para>
/// </summary>
public sealed class LocalGesturePreviewRenderer : ILocalPreviewRenderer
{
    /// <summary>This renderer's own additive, preview-only parameter key -- never an xLights effect-settings key.</summary>
    public const string PreviewGestureParameterKey = "PreviewGesture";

    /// <summary>Required alongside <see cref="StepGestureValue"/>: the <see cref="SpatialPath.Id"/> (as a <see cref="Guid"/> string) to walk.</summary>
    public const string PathIdParameterKey = "PathId";

    public const string HoldGestureValue = "Hold";
    public const string PopGestureValue = "Pop";
    public const string StepGestureValue = "Step";

    /// <summary>50 ms per sampled frame (20 fps) -- a fast, lightweight preview cadence, not a claim about xLights' own render fps.</summary>
    public const long FrameIntervalTicks = 500_000;

    /// <summary>Fraction of a Pop effect's own window it stays lit before going dark.</summary>
    public const double PopFlashFraction = 0.25;

    public async IAsyncEnumerable<PreviewFrame> PreviewAsync(
        SequenceDocument sequence,
        SpatialDisplayGraph layout,
        TimeWindow window,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(layout);

        if (window.EndTicks <= window.StartTicks)
        {
            throw new RenderPreviewInvalidException(
                $"Preview window must be a non-empty half-open [start, end) range; received [{window.StartTicks}, {window.EndTicks}).");
        }

        var nodesById = layout.Nodes.ToDictionary(n => n.Id);
        var pathsById = layout.Paths.ToDictionary(p => p.Id);

        for (var t = window.StartTicks; t < window.EndTicks; t += FrameIntervalTicks)
        {
            ct.ThrowIfCancellationRequested();

            var illuminated = new List<PixelPoint>();
            var unsupported = new List<Guid>();

            foreach (var effect in sequence.Effects)
            {
                if (t < effect.StartTicks || t >= effect.EndTicks)
                {
                    continue;
                }

                if (!TryResolve(effect, nodesById, pathsById, t, out var pixel))
                {
                    unsupported.Add(effect.Id);
                    continue;
                }

                if (pixel is not null)
                {
                    illuminated.Add(pixel);
                }
            }

            yield return new PreviewFrame(t, illuminated, unsupported);
            await Task.Yield();
        }
    }

    private static bool TryResolve(
        EffectInstance effect,
        IReadOnlyDictionary<ModelId, DisplayNode> nodesById,
        IReadOnlyDictionary<Guid, SpatialPath> pathsById,
        long t,
        out PixelPoint? pixel)
    {
        pixel = null;

        if (!effect.Parameters.TryGetValue(PreviewGestureParameterKey, out var gesture))
        {
            return false;
        }

        switch (gesture)
        {
            case HoldGestureValue:
                if (!nodesById.TryGetValue(effect.Target, out var holdNode))
                {
                    return false;
                }

                pixel = Illuminate(holdNode);
                return true;

            case PopGestureValue:
                if (!nodesById.TryGetValue(effect.Target, out var popNode))
                {
                    return false;
                }

                var duration = effect.EndTicks - effect.StartTicks;
                var elapsed = t - effect.StartTicks;
                pixel = elapsed < (long)(duration * PopFlashFraction) ? Illuminate(popNode) : null;
                return true;

            case StepGestureValue:
                if (!effect.Parameters.TryGetValue(PathIdParameterKey, out var pathIdText)
                    || !Guid.TryParse(pathIdText, out var pathId)
                    || !pathsById.TryGetValue(pathId, out var path)
                    || path.OrderedModels.Count == 0)
                {
                    return false;
                }

                // Integer arithmetic only -- deliberately avoids double-precision progress*count truncation
                // error (e.g. 2/3 * 3 can round down to 1.999... in double, landing one node short).
                var stepDuration = effect.EndTicks - effect.StartTicks;
                var elapsedTicks = t - effect.StartTicks;
                var rawIndex = (int)(elapsedTicks * path.OrderedModels.Count / stepDuration);
                var index = Math.Clamp(rawIndex, 0, path.OrderedModels.Count - 1);

                if (!nodesById.TryGetValue(path.OrderedModels[index], out var stepNode))
                {
                    return false;
                }

                pixel = Illuminate(stepNode);
                return true;

            default:
                return false;
        }
    }

    private static PixelPoint Illuminate(DisplayNode node) => new(node.Id, node.X, node.Y, 255, 255, 255);
}
