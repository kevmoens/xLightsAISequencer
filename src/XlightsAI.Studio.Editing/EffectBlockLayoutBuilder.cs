using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 018: builds the pixel-space <see cref="EffectBlockLayout"/>s for a single model's
/// timeline row, and hit-tests a pixel back to the effect it belongs to. Effects are grouped into
/// per-<see cref="EffectInstance.Layer"/> tracks — this step's own "support overlapping layers
/// explicitly" guidance: two effects on the same model whose time ranges overlap are never merged or
/// silently hidden behind one another; they render on distinct layer tracks. <see cref="HitTest"/>
/// on an overlapping pixel returns the highest <see cref="EffectInstance.Layer"/> value (the
/// topmost, most-recently-composited track), an explicit, documented choice rather than an
/// arbitrary one.
/// </summary>
public static class EffectBlockLayoutBuilder
{
    /// <summary>
    /// Builds one pixel-space <see cref="EffectBlockLayout"/> per effect on <paramref name="modelId"/>,
    /// grouped into layer tracks (<see cref="EffectLayerTrack"/>), ordered by
    /// <see cref="EffectInstance.Layer"/> ascending, then by start time within a layer. Empty for a
    /// model with no effects — the "silence" edge case this step shares with Step 016's own ruler —
    /// never a crash.
    /// </summary>
    public static IReadOnlyList<EffectLayerTrack> BuildLayerTracks(
        IReadOnlyList<EffectInstance> effects, ModelId modelId, ITimelineViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(viewport);

        var tracks = new List<EffectLayerTrack>();
        foreach (var layerGroup in effects
            .Where(e => e.Target == modelId)
            .GroupBy(e => e.Layer)
            .OrderBy(g => g.Key))
        {
            var blocks = layerGroup
                .OrderBy(e => e.StartTicks)
                .Select(e => ToBlock(e, viewport))
                .ToList();
            tracks.Add(new EffectLayerTrack(layerGroup.Key, blocks));
        }

        return tracks;
    }

    /// <summary>
    /// Returns the id of the topmost (highest <see cref="EffectInstance.Layer"/>) effect on
    /// <paramref name="modelId"/> whose half-open <c>[Start, End)</c> pixel span contains
    /// <paramref name="pixelX"/>, or <see langword="null"/> when nothing is under that pixel — the
    /// real hit test behind "hit test click and keyboard navigation" (Step 018 implementation
    /// guidance).
    /// </summary>
    public static Guid? HitTest(
        IReadOnlyList<EffectInstance> effects, ModelId modelId, ITimelineViewport viewport, double pixelX)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(viewport);

        var ticks = viewport.TimeFromX(pixelX);
        EffectInstance? best = null;
        foreach (var effect in effects)
        {
            if (effect.Target != modelId)
            {
                continue;
            }

            if (ticks < effect.StartTicks || ticks >= effect.EndTicks)
            {
                continue;
            }

            if (best is null || effect.Layer > best.Layer)
            {
                best = effect;
            }
        }

        return best?.Id;
    }

    private static EffectBlockLayout ToBlock(EffectInstance effect, ITimelineViewport viewport)
    {
        var x = viewport.XFromTime(effect.StartTicks);
        var width = viewport.XFromTime(effect.EndTicks) - x;
        return new EffectBlockLayout(
            effect.Id, effect.Target, effect.Layer, effect.StartTicks, effect.EndTicks,
            effect.RecipeId, effect.Locked, x, Math.Max(0d, width));
    }
}
