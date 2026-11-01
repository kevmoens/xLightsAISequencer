using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 7: "A Look -&gt; ChoreographyIR compiler: given a hand-authored Look
/// (architecture §4) and a section's music data, expand it into the IR." Resolves every layer's own
/// prop group against the real layout, picks Mode A (bound) vs Mode B (discrete) emission per layer
/// (<see cref="EmissionStrategySelector"/>), validates every parameter override against the real
/// generated vocabulary, and applies <see cref="PropDelayResolver"/> stagger -- but never applies the
/// always-moving gap-splitting pass (that runs globally, across a whole target's entire song timeline,
/// in <see cref="ChoreographyEngine"/>, not per-section here).
/// </summary>
public static class LookCompiler
{
    /// <summary>
    /// Build-plan Phase 8 task 2 / architecture §10: "a vocalActive signal gating hold-vs-chase (steady
    /// single-colour full-house hold under vocals, frequency-reactive movement under instrumental
    /// breaks)." A <c>chase</c>-motion Look compiled against a vocal-active section is pushed toward
    /// hold-like behavior: its own declared duty cycle is floored at <see cref="VocalHoldMinDuty"/> (less
    /// flicker, closer to full coverage) and its own discrete-layer cadence is forced onto the steady
    /// <see cref="VocalHoldTimescale"/> track instead of whatever (possibly frequency-reactive, e.g.
    /// <c>Beats</c>/<c>Percussion</c>) track the Look itself declared -- never onto a curve/binding track
    /// the effect doesn't actually support, since <see cref="EmissionStrategySelector.SelectMode"/> and
    /// <see cref="ResolveEventWindows"/> both fall back safely when the requested track is unavailable.
    /// </summary>
    private const double VocalHoldMinDuty = 0.9;
    private const string VocalHoldTimescale = "Bars";

    /// <summary>Same 100ns tick unit as the rest of this project's contracts (<see cref="FrameClock"/> keeps its own copy private) -- used only to turn a mark window's own tick span into a real seconds-based rate for <see cref="ThinToTargetChangeRate"/>.</summary>
    private const long TicksPerSecond = 10_000_000L;

    /// <exception cref="ChoreographyCompilationException">A layer's own prop/effect/palette reference does not resolve, or a declared parameter override is not a verified-acceptable value for the resolved effect.</exception>
    public static ChoreographyIR Compile(Look look, SectionMusicContext section, DisplayGraph layout, LookCompilerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(settings);

        if (look.Layers.Count == 0)
        {
            throw new ChoreographyCompilationException($"Look '{look.Id}' has no layers -- nothing to compile.");
        }

        var effectiveCeiling = Math.Min(look.EnergyCeiling, section.EnergyCeiling);
        var effects = new List<EffectInstance>();
        var availableTrackNames = section.TimingTracksByName.Keys.ToHashSet(StringComparer.Ordinal);
        var vocalHoldBias = section.VocalActive && string.Equals(look.Motion, "chase", StringComparison.OrdinalIgnoreCase);

        foreach (var rawLayer in look.Layers.OrderBy(l => l.Z))
        {
            var layer = vocalHoldBias
                ? rawLayer with
                {
                    Duty = Math.Max(rawLayer.Duty, VocalHoldMinDuty),
                    Timescale = availableTrackNames.Contains(VocalHoldTimescale) ? VocalHoldTimescale : rawLayer.Timescale,
                }
                : rawLayer;

            // Normalize a free-form, AI-authored Timescale word ("beat"/"bar"/"phrase", ...) to the real
            // track name it was clearly meant to reference (see TimescaleResolver's own doc comment for
            // the real bug this fixes) -- every downstream exact-match lookup below (SelectMode, the
            // timing-track binding, ResolveEventWindows) then sees the canonical name and works unchanged.
            layer = layer with { Timescale = TimescaleResolver.Resolve(layer.Timescale, availableTrackNames) ?? layer.Timescale };

            var models = TryResolveModels(layer.Props, layout);
            if (models.Count == 0)
            {
                continue; // An empty-resolution group (e.g. a style's declared group not present on this house) is skipped, never invented.
            }

            if (!settings.RecipeCatalog.TryGet(RecipeIdFor(layer.Effect, settings.RecipeCatalog), out var recipe))
            {
                throw new ChoreographyCompilationException(
                    $"Look '{look.Id}' layer z={layer.Z} declares effect '{layer.Effect}', which no registered recipe produces.");
            }

            var defaultParameters = recipe.CreateDefaultParameters();
            var parameters = new Dictionary<string, string>(defaultParameters, StringComparer.Ordinal);
            foreach (var (key, value) in layer.ParamsOrEmpty)
            {
                if (!recipe.IsSupportedParameterValue(key, value))
                {
                    throw new ChoreographyCompilationException(
                        $"Look '{look.Id}' layer z={layer.Z} sets '{key}'='{value}' on effect '{layer.Effect}', which is not a verified-acceptable value.");
                }

                parameters[key] = value;
            }

            var mode = EmissionStrategySelector.SelectMode(layer.Effect, layer, availableTrackNames);
            if (mode == EmissionMode.Bound && EmissionStrategySelector.SupportsTimingTrackBinding(layer.Effect) &&
                layer.Timescale is { Length: > 0 } boundTrack && availableTrackNames.Contains(boundTrack))
            {
                ApplyTimingTrackBinding(layer.Effect, boundTrack, settings.Vocabulary, parameters);
            }

            // Confirmed against a real rendered show: a group reference ("group:Matrixes") was always
            // expanded into one identical EffectInstance per member model, never a true shared-group
            // effect -- real xLights represents both identically on export (XsqExporter/PropGroupResolver's
            // own doc comments), so the ONLY reason to expand per-member here is a genuine stagger
            // (PropDelays), which needs distinct per-model timing a single group target cannot carry. With
            // no stagger declared, target the group itself once -- both fixes what reads in xLights as one
            // real "Matrixes" effect (editable as a unit) instead of 3 duplicated per-model copies, and
            // lets a spatially-aware effect (Shockwave, Bars, Pinwheel, ...) render across the group's own
            // combined canvas instead of 3 independent small ones.
            if (models.Count > 1 && look.PropDelaysOrEmpty.Count == 0 &&
                PropGroupResolver.TryGetRealGroupName(layer.Props, layout, out var groupName))
            {
                var start = section.Window.StartTicks;
                var end = section.Window.EndTicks;
                if (end - start >= FrameClock.FrameIntervalTicks)
                {
                    var groupTarget = new ModelId(groupName!);
                    effects.AddRange(mode == EmissionMode.Bound
                        ? CompileBoundLayer(layer, groupTarget, start, end, parameters, settings)
                        : CompileDiscreteLayer(layer, groupTarget, start, end, parameters, section, settings));
                }

                continue;
            }

            var pixelByModel = models.ToDictionary(id => id, id => layout.Models.First(m => m.Id == id).NodeCount);
            var delayOffsets = PropDelayResolver.ResolveOffsets(pixelByModel, look.PropDelaysOrEmpty);

            foreach (var model in models)
            {
                var start = Math.Clamp(section.Window.StartTicks + delayOffsets[model], section.Window.StartTicks, section.Window.EndTicks);
                var end = section.Window.EndTicks;
                if (end - start < FrameClock.FrameIntervalTicks)
                {
                    continue;
                }

                effects.AddRange(mode == EmissionMode.Bound
                    ? CompileBoundLayer(layer, model, start, end, parameters, settings)
                    : CompileDiscreteLayer(layer, model, start, end, parameters, section, settings));
            }
        }

        if (!settings.PaletteLibrary.TryGetValue(look.PaletteRef, out var palette))
        {
            throw new ChoreographyCompilationException($"Look '{look.Id}' references palette '{look.PaletteRef}', which is not in the supplied palette library.");
        }

        _ = effectiveCeiling; // ceiling enforcement itself is ChoreographyLegalityValidator's own, independently re-checkable, concern -- captured here only to fail fast on an obviously-misconfigured Look/section pair in a future revision.

        return new ChoreographyIR([], effects, [palette]);
    }

    private static IReadOnlyList<ModelId> TryResolveModels(string reference, DisplayGraph layout)
    {
        try
        {
            return PropGroupResolver.Resolve(reference, layout);
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    /// <summary>
    /// Resolves <paramref name="xlightsEffectName"/> to a registered recipe id, preferring a *generated*
    /// recipe (<c>GeneratedEffectRecipe</c>, whose default parameters are drawn from the real vocabulary)
    /// over a legacy hand-written one with the same <see cref="IEffectRecipe.XlightsEffectName"/> --
    /// <c>EffectRecipeCatalog.CreateDefault</c> still registers <c>OnEffectRecipe</c> (Phase 1b's own
    /// pre-vocabulary placeholder, whose own default <c>EffectFamily</c> parameter is not a real
    /// vocabulary property) alongside a generated <c>effect.generated.on.v1</c> once
    /// <c>GeneratedEffectRecipeFactory.RegisterAll</c> has run; picking the legacy one would fail real
    /// vocabulary validation the moment a caller actually supplies one.
    ///
    /// Build-plan Phase 15 §3: public (not private) so <c>XlightsAI.Studio.Director.ChoreographyGenerationPipeline</c>
    /// can reuse this exact resolution rule for name-based filler-effect-to-recipe-id resolution, instead
    /// of re-deriving the same LINQ a fourth time (it was previously duplicated across
    /// <c>AiChoreographyWholeSongGenerator.BuildFillerPool</c> and three test fixtures).
    /// </summary>
    public static string RecipeIdFor(string xlightsEffectName, EffectRecipeCatalog catalog)
    {
        var generated = catalog.Recipes.FirstOrDefault(r =>
            r.RecipeId.StartsWith("effect.generated.", StringComparison.Ordinal) &&
            string.Equals(r.XlightsEffectName, xlightsEffectName, StringComparison.Ordinal));
        if (generated is not null)
        {
            return generated.RecipeId;
        }

        var anyMatch = catalog.Recipes.FirstOrDefault(r => string.Equals(r.XlightsEffectName, xlightsEffectName, StringComparison.Ordinal));
        return anyMatch?.RecipeId ?? $"effect.generated.{xlightsEffectName.ToLowerInvariant()}.v1";
    }

    /// <summary>Sets the real <c>SettingKey</c> for <paramref name="binding"/>'s own timing-track property to <paramref name="trackName"/> -- architecture §6's "one Shockwave with Shockwave_TimingTrack=Beats."</summary>
    private static void ApplyTimingTrackBinding(string xlightsEffectName, string trackName, EffectVocabularyCatalog vocabulary, Dictionary<string, string> parameters)
    {
        if (!EmissionStrategySelector.TimingTrackBoundEffects.TryGetValue(xlightsEffectName, out var binding))
        {
            return;
        }

        if (!vocabulary.Effects.TryGetValue(xlightsEffectName, out var effectVocabulary))
        {
            throw new ChoreographyCompilationException($"'{xlightsEffectName}' is not in the supplied vocabulary.");
        }

        var trackProperty = effectVocabulary.FindByPropertyId(binding.TimingTrackPropertyId)
            ?? throw new ChoreographyCompilationException($"'{xlightsEffectName}' has no known property for its own declared timing-track binding '{binding.TimingTrackPropertyId}'.");
        parameters[trackProperty.SettingKey] = trackName;

        if (binding.EnableCheckboxPropertyId is { } enableId)
        {
            var enableProperty = effectVocabulary.FindByPropertyId(enableId);
            if (enableProperty is not null)
            {
                parameters[enableProperty.SettingKey] = "1";
            }
        }
    }

    /// <summary>Mode A: one effect spanning <c>[start, end)</c>, optionally carrying value curves (architecture §6's "Timing Track Fade"/"Music" families) for every curve the layer declares.</summary>
    private static IReadOnlyList<EffectInstance> CompileBoundLayer(
        LookLayer layer, ModelId model, long start, long end, Dictionary<string, string> parameters, LookCompilerSettings settings)
    {
        // IEffectRecipeRegistry.CreateVerified always hardcodes Layer: 0 (it has no layer parameter) --
        // overridden here to the Look's own declared z-order (architecture §4: "layer 1 is the first
        // EffectLayer in document order and is the topmost... maps directly onto z"), or every layer of
        // a multi-layer Look would silently collide onto the same exported EffectLayer.
        var effect = settings.RecipeRegistry.CreateVerified(RecipeIdFor(layer.Effect, settings.RecipeCatalog), model, start, end)
            with
        { Layer = layer.Z, Parameters = parameters, PaletteIndex = 0 };

        if (layer.CurvesOrEmpty.Count == 0)
        {
            return [effect];
        }

        var curves = new List<Xlights.EffectValueCurve>();
        foreach (var (paramKey, binding) in layer.CurvesOrEmpty)
        {
            double min = 0, max = 100;
            if (settings.Vocabulary.TryGetProperty(layer.Effect, paramKey, out var property))
            {
                min = property.Min ?? min;
                max = property.Max ?? max;
            }

            var isAudioBinding = binding.Type.Contains("Music", StringComparison.OrdinalIgnoreCase);
            curves.Add(new Xlights.EffectValueCurve(
                paramKey, binding.Type, min, max,
                TimingTrack: isAudioBinding ? null : binding.Track,
                AudioTrack: isAudioBinding ? binding.Track : null));
        }

        return [effect with { ValueCurves = curves }];
    }

    /// <summary>Mode B: one short effect per mark of the layer's own declared timescale track (falling back to this song's own measured bars when the declared track is unavailable), each filling <see cref="LookLayer.Duty"/> of its own inter-mark span.</summary>
    private static IReadOnlyList<EffectInstance> CompileDiscreteLayer(
        LookLayer layer, ModelId model, long start, long end, Dictionary<string, string> parameters, SectionMusicContext section, LookCompilerSettings settings)
    {
        var markWindows = ResolveEventWindows(layer, start, end, section);
        var duty = Math.Clamp(layer.Duty <= 0 ? 1.0 : layer.Duty, 0.05, 1.0);

        var result = new List<EffectInstance>(markWindows.Count);
        foreach (var window in markWindows)
        {
            var span = window.EndTicks - window.StartTicks;
            var litSpan = (long)Math.Round(span * duty);
            // Never emit an effect shorter than one exported frame (XsqExporter cannot represent it --
            // "rounds to a non-positive duration"): floor at one frame interval, capped at the mark's
            // own window; a mark whose own full window is itself sub-frame is skipped entirely.
            litSpan = Math.Max(FrameClock.FrameIntervalTicks, litSpan);
            var effectEnd = Math.Min(window.StartTicks + litSpan, window.EndTicks);
            if (effectEnd - window.StartTicks < FrameClock.FrameIntervalTicks)
            {
                continue;
            }

            result.Add(settings.RecipeRegistry.CreateVerified(RecipeIdFor(layer.Effect, settings.RecipeCatalog), model, window.StartTicks, effectEnd)
                with
            { Layer = layer.Z, Parameters = parameters, PaletteIndex = 0 });
        }

        return result;
    }

    private static List<TimeWindow> ResolveEventWindows(LookLayer layer, long start, long end, SectionMusicContext section)
    {
        List<TimeWindow> windows;
        if (layer.Timescale is { Length: > 0 } trackName && section.TimingTracksByName.TryGetValue(trackName, out var track))
        {
            windows = track.Marks
                .Where(m => m.StartTicks < end && m.EndTicks > start)
                .Select(m => new TimeWindow(Math.Max(m.StartTicks, start), Math.Min(m.EndTicks, end)))
                .Where(w => w.EndTicks > w.StartTicks)
                .ToList();
        }
        else
        {
            windows = section.BarGrid.Bars
                .Where(b => b.Window.StartTicks < end && b.Window.EndTicks > start)
                .Select(b => new TimeWindow(Math.Max(b.Window.StartTicks, start), Math.Min(b.Window.EndTicks, end)))
                .Where(w => w.EndTicks > w.StartTicks)
                .ToList();
        }

        return ThinToTargetChangeRate(windows, section.TargetChangeRatePerSecond);
    }

    /// <summary>
    /// Build-plan Phase 12 §3a: a section-level ceiling/floor on Mode B's own inter-mark cadence, applied
    /// on top of whatever track the layer's own declared <see cref="LookLayer.Timescale"/> resolved to --
    /// never a new compiled-IR concept, just merging consecutive marks until this layer's own implied
    /// change-rate (one effect start per window) no longer exceeds <paramref name="targetChangeRatePerSecond"/>.
    /// Only ever throttles (merges marks together), never speeds up (splits one mark into several) --
    /// a layer already under its section's own target compiles completely unchanged. A heavily-throttled
    /// quiet section's own subject layer naturally ends up with a handful of long merged windows instead of
    /// many short ones, which directly helps close the "no anchor/base layer anywhere" gap (§3b) as a side
    /// effect of throttling cadence, not a separate mechanism.
    /// </summary>
    private static List<TimeWindow> ThinToTargetChangeRate(List<TimeWindow> windows, double targetChangeRatePerSecond)
    {
        if (windows.Count < 2 || double.IsPositiveInfinity(targetChangeRatePerSecond) || targetChangeRatePerSecond <= 0)
        {
            return windows;
        }

        var totalSeconds = (windows[^1].EndTicks - windows[0].StartTicks) / (double)TicksPerSecond;
        if (totalSeconds <= 0)
        {
            return windows;
        }

        var naturalRate = windows.Count / totalSeconds;
        if (naturalRate <= targetChangeRatePerSecond)
        {
            return windows;
        }

        var keepEvery = (int)Math.Ceiling(naturalRate / targetChangeRatePerSecond);
        if (keepEvery <= 1)
        {
            return windows;
        }

        var thinned = new List<TimeWindow>();
        for (var i = 0; i < windows.Count; i += keepEvery)
        {
            var lastIndex = Math.Min(i + keepEvery, windows.Count) - 1;
            thinned.Add(new TimeWindow(windows[i].StartTicks, windows[lastIndex].EndTicks));
        }

        return thinned;
    }
}
