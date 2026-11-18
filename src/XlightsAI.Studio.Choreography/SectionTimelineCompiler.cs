using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 28a §5: compiles a <see cref="SectionTimeline"/> against a base <see cref="Look"/> --
/// assembly, not new DSP. Starts from <see cref="LookCompiler.Compile"/>'s own unchanged output (the
/// fallback for any Props/time window no cue touches), then overlays one real <see cref="EffectInstance"/>
/// per real <see cref="SectionCue"/>, resolving every anchor against the exact same
/// <see cref="SectionTimingMarks.Resolve"/> enumeration <see cref="SectionTimelineValidator"/> already
/// checked every <see cref="SectionCue.AnchorId"/> against.
/// </summary>
public static class SectionTimelineCompiler
{
    /// <summary>
    /// Build-plan Phase 28a §5 item 4: the two real, disclosed mass/accent stagger offsets a
    /// <see cref="SectionCue.HandoffFromProps"/> pair resolves against via <see cref="PropDelayResolver.ResolveOffsets"/>
    /// -- the heavier of the two real props leads (no offset), the lighter trails, same "big things lead,
    /// small things trail" physical intuition <see cref="PropDelayResolver"/>'s own doc comment already
    /// states, applied here to exactly 2 props instead of a whole layer's own declared set.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> HandoffStaggerOffsets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [PropDelayResolver.TierMass] = "+0ms",
        [PropDelayResolver.TierMid] = "+40ms",
    };

    /// <exception cref="ChoreographyCompilationException"><paramref name="anchorId"/> does not match any real timing mark <see cref="SectionTimingMarks.Resolve"/> offers for this section -- never a best-effort guess.</exception>
    public static long ResolveAnchor(
        string anchorId, SectionMusicContext context,
        IReadOnlyList<PhraseCandidate>? phrases = null, IReadOnlyList<TransientHit>? transientHits = null,
        IReadOnlyList<NoteEvent>? vocalNotes = null, IReadOnlyList<LeadInstrumentWindow>? leadInstrumentWindows = null) =>
        ResolveAnchor(anchorId, SectionTimingMarks.Resolve(context, phrases, transientHits, vocalNotes, leadInstrumentWindows), context);

    private static long ResolveAnchor(string anchorId, IReadOnlyDictionary<string, long> marks, SectionMusicContext context) =>
        marks.TryGetValue(anchorId, out var ticks)
            ? ticks
            : throw new ChoreographyCompilationException(
                $"AnchorId '{anchorId}' is not one of the real timing marks offered for section window [{context.Window.StartTicks},{context.Window.EndTicks}).");

    /// <exception cref="ChoreographyCompilationException">A cue's own AnchorId/HoldUntilAnchorId does not resolve, its Props/Effect does not resolve against the real layout/recipe catalog, or any of <see cref="LookCompiler.Compile"/>'s own failure cases for the base layer.</exception>
    public static ChoreographyIR Compile(
        Look look, SectionTimeline timeline, SectionMusicContext context, DisplayGraph layout, LookCompilerSettings settings,
        IReadOnlyList<PhraseCandidate>? phrases = null, IReadOnlyList<TransientHit>? transientHits = null,
        IReadOnlyList<NoteEvent>? vocalNotes = null, IReadOnlyList<LeadInstrumentWindow>? leadInstrumentWindows = null)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(settings);

        // Base layer, unchanged -- the fallback for any Props/time window no cue touches.
        var baseIr = LookCompiler.Compile(look, context, layout, settings);

        var cues = timeline.CuesOrEmpty;
        if (cues.Count == 0)
        {
            return baseIr;
        }

        var marks = SectionTimingMarks.Resolve(context, phrases, transientHits, vocalNotes, leadInstrumentWindows);
        long Resolve(string id) => ResolveAnchor(id, marks, context);

        var sameProps = cues
            .Select((cue, index) => (cue, index))
            .GroupBy(x => x.cue.Props, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => Resolve(x.cue.AnchorId)).ToList(), StringComparer.Ordinal);

        var overlayEffects = new List<EffectInstance>();
        var baseLayerCount = look.Layers.Count;

        for (var cueIndex = 0; cueIndex < cues.Count; cueIndex++)
        {
            var cue = cues[cueIndex];
            var start = Resolve(cue.AnchorId);

            long end;
            if (cue.HoldUntilAnchorId is { Length: > 0 } holdId)
            {
                end = Resolve(holdId);
            }
            else
            {
                var group = sameProps[cue.Props];
                var position = group.FindIndex(x => x.index == cueIndex);
                end = position >= 0 && position + 1 < group.Count
                    ? Resolve(group[position + 1].cue.AnchorId)
                    : context.Window.EndTicks;
            }

            end = Math.Clamp(end, context.Window.StartTicks, context.Window.EndTicks);
            start = Math.Clamp(start, context.Window.StartTicks, context.Window.EndTicks);
            if (end <= start)
            {
                continue;
            }

            if (!settings.RecipeCatalog.TryGet(LookCompiler.RecipeIdFor(cue.Effect, settings.RecipeCatalog), out var recipe))
            {
                throw new ChoreographyCompilationException(
                    $"SectionTimeline for section '{timeline.SectionId}' cue on Props '{cue.Props}' declares effect '{cue.Effect}', which no registered recipe produces.");
            }

            var parameters = new Dictionary<string, string>(recipe.CreateDefaultParameters(), StringComparer.Ordinal);
            var duty = IntensityTierMapping.ResolveDuty(cue.Intensity);

            if (cue.HandoffFromProps is { Length: > 0 } fromProps && !string.Equals(fromProps, cue.Props, StringComparison.Ordinal))
            {
                EmitHandoffPair(cue, fromProps, start, end, duty, parameters, layout, settings, baseLayerCount + cueIndex, overlayEffects);
            }
            else
            {
                EmitSingle(cue, cue.Props, start, end, duty, parameters, layout, settings, baseLayerCount + cueIndex, overlayEffects);
            }
        }

        return baseIr with { Effects = [.. baseIr.Effects, .. overlayEffects] };
    }

    /// <summary>Build-plan Phase 28a §5 item 4: a real two-prop stagger between <paramref name="fromProps"/> (handing off) and <paramref name="cue"/>'s own Props (receiving), anchored to this cue's own resolved <paramref name="start"/> tick -- a genuine timed handoff, never two independent effects that happen to be adjacent.</summary>
    private static void EmitHandoffPair(
        SectionCue cue, string fromProps, long start, long end, double duty, IReadOnlyDictionary<string, string> parameters,
        DisplayGraph layout, LookCompilerSettings settings, int layerZ, List<EffectInstance> overlayEffects)
    {
        if (!TryResolveTarget(cue.Props, layout, out var toTarget) || !TryResolveTarget(fromProps, layout, out var fromTarget))
        {
            return; // An unresolvable Props/HandoffFromProps reference is skipped, never invented -- same discipline LookCompiler's own TryResolveModels already applies.
        }

        var pixelByModel = new Dictionary<ModelId, int>
        {
            [toTarget] = (int)Math.Max(0, ResolveWeight(cue.Props, layout)),
            [fromTarget] = (int)Math.Max(0, ResolveWeight(fromProps, layout)),
        };
        var offsets = PropDelayResolver.ResolveOffsets(pixelByModel, HandoffStaggerOffsets);

        EmitAt(cue, toTarget, start + offsets[toTarget], end + offsets[toTarget], duty, parameters, settings, layerZ, overlayEffects);
        EmitAt(cue, fromTarget, start + offsets[fromTarget], end + offsets[fromTarget], duty, parameters, settings, layerZ, overlayEffects);
    }

    private static void EmitSingle(
        SectionCue cue, string propsRef, long start, long end, double duty, IReadOnlyDictionary<string, string> parameters,
        DisplayGraph layout, LookCompilerSettings settings, int layerZ, List<EffectInstance> overlayEffects)
    {
        if (!TryResolveTarget(propsRef, layout, out var target))
        {
            return;
        }

        EmitAt(cue, target, start, end, duty, parameters, settings, layerZ, overlayEffects);
    }

    private static void EmitAt(
        SectionCue cue, ModelId target, long start, long end, double duty, IReadOnlyDictionary<string, string> parameters,
        LookCompilerSettings settings, int layerZ, List<EffectInstance> overlayEffects)
    {
        var span = end - start;
        if (span <= 0)
        {
            return;
        }

        var litSpan = Math.Max(FrameClock.FrameIntervalTicks, (long)Math.Round(span * duty));
        var litEnd = Math.Min(start + litSpan, end);
        if (litEnd - start < FrameClock.FrameIntervalTicks)
        {
            return;
        }

        var effect = settings.RecipeRegistry.CreateVerified(LookCompiler.RecipeIdFor(cue.Effect, settings.RecipeCatalog), target, start, litEnd)
            with
        { Layer = layerZ, Parameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal), PaletteIndex = 0, SourceAnnotation = cue.Annotation };
        overlayEffects.Add(effect);
    }

    /// <summary>Resolves <paramref name="propsRef"/> to the one real target an emitted <see cref="EffectInstance"/> should carry -- a real group's own <see cref="ModelId"/> when it names a group (same group-collapse LookCompiler's own base layer prefers), or the single resolved model otherwise.</summary>
    private static bool TryResolveTarget(string propsRef, DisplayGraph layout, out ModelId target)
    {
        if (PropGroupResolver.TryGetRealGroupName(propsRef, layout, out var groupName))
        {
            target = new ModelId(groupName!);
            return true;
        }

        var models = LookCompiler.TryResolveModels(propsRef, layout);
        if (models.Count > 0)
        {
            target = models[0];
            return true;
        }

        target = default!;
        return false;
    }

    /// <summary>Real pixel weight of <paramref name="propsRef"/> -- summed over every real, distinct, recursively-expanded member when it names a group, or that one model's own weight otherwise. 0 for a reference that resolves to neither.</summary>
    private static long ResolveWeight(string propsRef, DisplayGraph layout)
    {
        var modelsById = layout.Models.ToDictionary(m => m.Id);
        return LookCompiler.TryResolveModels(propsRef, layout)
            .Sum(id => modelsById.TryGetValue(id, out var model) ? Math.Max(0, model.NodeCount) : 0);
    }
}
