using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Knowledge;
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
        IReadOnlyList<NoteEvent>? vocalNotes = null, IReadOnlyList<LeadInstrumentWindow>? leadInstrumentWindows = null,
        EffectVisualCharacterCatalog? brightnessCatalog = null, SectionTimingCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(settings);

        var cues = timeline.CuesOrEmpty;
        if (cues.Count == 0)
        {
            return LookCompiler.Compile(look, context, layout, settings);
        }

        var marks = (catalog ?? SectionTimingCatalog.Build(context, phrases, transientHits, vocalNotes, leadInstrumentWindows)).Marks;
        foreach (var cue in cues)
        {
            _ = ResolveAnchor(cue.AnchorId, marks, context);
            if (cue.HoldUntilAnchorId is { Length: > 0 } id)
            {
                _ = ResolveAnchor(id, marks, context);
            }
        }
        var resolved = SectionCueWindow.Resolve(timeline, marks, context.Window, layout);
        return CompileResolved(LookCompiler.Compile(look, context, layout, settings), context, layout,
            settings, resolved, resolved, brightnessCatalog);
    }

    internal static ChoreographyIR CompileResolved(
        ChoreographyIR baseIr, SectionMusicContext context, DisplayGraph layout, LookCompilerSettings settings,
        IReadOnlyList<SectionCueWindow> resolved, IReadOnlyList<SectionCueWindow> program,
        EffectVisualCharacterCatalog? brightnessCatalog)
    {
        var holds = program.Where(c => c.IsHeld).ToArray();
        var cueLayerCount = program.Select(c => c.Index + 1).DefaultIfEmpty().Max();
        // Smaller layer numbers render above the base. Reserve a separate band to prevent XSQ export from shifting a cue.
        var firstBaseLayer = baseIr.Effects.Select(e => e.Layer).DefaultIfEmpty(1).Min();
        var baseLayerOffset = cueLayerCount == 0 ? 0 : cueLayerCount + Math.Max(0, 1 - firstBaseLayer);
        var baseEffects = MaskProtectedBase(baseIr.Effects, layout, context.Window, program)
            .Select(effect => effect with { Layer = effect.Layer + baseLayerOffset }).ToArray();
        var palettes = baseIr.Palettes.ToList();
        var overlayEffects = new List<EffectInstance>();

        foreach (var placement in resolved)
        {
            var cue = placement.Cue;
            var firstPlacement = overlayEffects.Count;
            var start = placement.Window.StartTicks;
            var end = placement.Window.EndTicks;
            if (end <= start)
            {
                continue;
            }

            if (cue.Kind == CueKind.Accent && holds.Any(hold => placement.OverlapsHeld(hold, program)))
            {
                continue;
            }
            if (cue.Intensity == IntensityTier.Off)
            {
                continue;
            }

            if (!settings.RecipeCatalog.TryGet(LookCompiler.RecipeIdFor(cue.Effect, settings.RecipeCatalog), out var recipe))
            {
                throw new ChoreographyCompilationException(
                    $"Cue '{cue.AnchorId}' on Props '{cue.Props}' declares effect '{cue.Effect}', which no registered recipe produces.");
            }

            var parameters = new Dictionary<string, string>(recipe.CreateDefaultParameters(), StringComparer.Ordinal);
            // Mined gestures already describe the lit interval of a real placement.
            var duty = cue.Kind == CueKind.Accent && !cue.IsMeasuredLitSpan
                ? IntensityTierMapping.ResolveDuty(cue.Intensity) : 1;

            if (cue.Targets is not { Count: > 0 } && cue.Kind == CueKind.Accent && cue.HandoffFromProps is { Length: > 0 } fromProps &&
                !string.Equals(fromProps, cue.Props, StringComparison.Ordinal))
            {
                EmitHandoffPair(cue, fromProps, start, end, duty, parameters, layout, settings, placement.Index + 1, overlayEffects);
            }
            else
            {
                var placedMembers = new HashSet<ModelId>();
                foreach (var target in cue.TargetReferences)
                {
                    var members = LookCompiler.TryResolveModels(target, layout);
                    var ends = members.Select(m => placement.IsHeld ? placement.HeldEnd(program, m) : end).Distinct().ToArray();
                    if (ends.Length > 1 || members.Any(placedMembers.Contains))
                    {
                        foreach (var member in members.Where(placedMembers.Add))
                        {
                            var memberEnd = placement.IsHeld ? placement.HeldEnd(program, member) : end;
                            EmitAt(cue, member, start, memberEnd, duty, parameters, settings, placement.Index + 1, overlayEffects);
                        }
                    }
                    else
                    {
                        placedMembers.UnionWith(members);
                        EmitSingle(cue, target, start, ends.FirstOrDefault(end), duty, parameters, layout, settings, placement.Index + 1, overlayEffects);
                    }
                }
            }
            if (cue.PaletteSlot is { } slot && settings.PaletteLibrary.TryGetValue(slot, out var palette))
            {
                var index = palettes.IndexOf(palette);
                if (index < 0)
                {
                    index = palettes.Count;
                    palettes.Add(palette);
                }
                for (var i = firstPlacement; i < overlayEffects.Count; i++)
                {
                    overlayEffects[i] = overlayEffects[i] with { PaletteIndex = index };
                }
            }
        }

        var compiled = baseIr with { Effects = [.. baseEffects, .. overlayEffects], Palettes = palettes };
        return brightnessCatalog is null ? compiled : EmphasisContrastPlanner.ApplyTimelineContrast(
            compiled, resolved, context, layout, settings, brightnessCatalog, program);
    }

    internal static List<EffectInstance> MaskProtectedBase(
        IReadOnlyList<EffectInstance> baseEffects, DisplayGraph layout, TimeWindow section, IReadOnlyList<SectionCueWindow> program)
    {
        var result = baseEffects.ToList();
        foreach (var (window, models) in ProtectedWindows(program))
        {
            if (window.StartTicks < section.EndTicks && window.EndTicks > section.StartTicks)
            {
                result = TimelineEffectMask.Remove(result, layout, window, models);
            }
        }
        return result;
    }

    internal static IEnumerable<(TimeWindow Window, IReadOnlySet<ModelId> Models)> ProtectedWindows(
        IReadOnlyList<SectionCueWindow> program) =>
        program.Where(c => c.IsHeld || c.Cue.Intensity == IntensityTier.Off)
            .SelectMany(c => c.Models.GroupBy(m => c.IsHeld ? c.HeldEnd(program, m) : c.Window.EndTicks)
                .Select(g => (new TimeWindow(c.Window.StartTicks, g.Key), (IReadOnlySet<ModelId>)g.ToHashSet())));

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
        { Layer = layerZ, Parameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal), PaletteIndex = 0,
            SourceAnnotation = cue.Annotation, CueTraceId = cue.TraceId };
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
