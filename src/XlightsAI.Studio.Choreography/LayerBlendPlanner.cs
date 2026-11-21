using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 30 step 30.1: "a <c>LayerBlendPlanner</c> that, given a Look's planned layer stack,
/// selects a blend mode per adjacent layer pair from the confirmed set only, informed by the mined
/// compatibility signal, instead of leaving the pipeline's current implicit default." Pure and read-only
/// over its inputs -- it never mutates <see cref="MultiLayerTemplateCatalog"/>/<see cref="EffectVisualCharacterCatalog"/>,
/// only returns a new <see cref="Look"/> with updated <see cref="LookLayer.Blend"/> values.
///
/// Deliberately opt-in, not wired into <see cref="LookCompiler"/> automatically: matching Phase 29's own
/// guardrail against blanket requirements (and this phase's own explicit "keep all new behavior opt-in
/// per Look/section" dependency note), nothing calls this planner unless a caller (a Director-side
/// generation pass, or a test/tool) deliberately chooses to run a specific Look through it. A Look this
/// planner has not touched keeps whatever <see cref="LookLayer.Blend"/> it already declared (almost
/// always the pipeline's own existing <c>"Normal"</c> default).
/// </summary>
public static class LayerBlendPlanner
{
    /// <summary>
    /// For every adjacent layer pair in <paramref name="look"/>'s own declared z-order (lower Z = base,
    /// higher Z = stacked on top, matching <see cref="LookCompiler"/>'s own <c>OrderBy(l =&gt; l.Z)</c>
    /// convention), looks up <see cref="BlendModeCompatibilityIndex.SelectBlendMode"/> for that exact
    /// (base effect, top effect) pair and, when the mined catalog has real evidence for it, sets the top
    /// layer's own <see cref="LookLayer.Blend"/> to that confirmed mode's real XML value. A pair with no
    /// mined evidence is left exactly as declared (almost always <c>"Normal"</c>) -- never guessed. Every
    /// value this method ever writes into <see cref="LookLayer.Blend"/> passes
    /// <see cref="ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue"/> by construction, since it only
    /// ever comes from <see cref="ConfirmedLayerBlendModeCatalog.Find"/> resolving a
    /// <see cref="BlendModeCompatibilityIndex"/> candidate name.
    /// </summary>
    public static Look Plan(Look look, MultiLayerTemplateCatalog templates, EffectVisualCharacterCatalog brightnessCatalog)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(brightnessCatalog);

        if (look.Layers.Count < 2)
        {
            return look; // Nothing to blend a single layer against.
        }

        var ordered = look.Layers.OrderBy(l => l.Z).ToList();
        var blendByZ = new Dictionary<int, string>();

        for (var i = 1; i < ordered.Count; i++)
        {
            var baseLayer = ordered[i - 1];
            var topLayer = ordered[i];

            var candidateName = BlendModeCompatibilityIndex.SelectBlendMode(templates, brightnessCatalog, baseLayer.Effect, topLayer.Effect);
            if (candidateName is null)
            {
                continue;
            }

            var mode = ConfirmedLayerBlendModeCatalog.Find(candidateName);
            if (mode is null || !ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue(mode.XmlValue))
            {
                continue; // Defensive -- the compatibility index only ever reports a candidate name this catalog itself produced, but a confirmed XML value is still re-checked here rather than trusted blind.
            }

            blendByZ[topLayer.Z] = mode.XmlValue;
        }

        if (blendByZ.Count == 0)
        {
            return look;
        }

        var newLayers = look.Layers.Select(l => blendByZ.TryGetValue(l.Z, out var xmlValue) ? l with { Blend = xmlValue } : l).ToList();
        return look with { Layers = newLayers };
    }
}
