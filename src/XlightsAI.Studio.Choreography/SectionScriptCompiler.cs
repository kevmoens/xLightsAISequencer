using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Choreography;

/// <summary>Build-plan Phase 10 §3e: one phrase-scoped sub-window's own Look assignment -- <see cref="ChoreographyEngine.Input.PhraseLookAssignmentsBySectionId"/>'s own element shape, produced by joining a <see cref="SectionScript.PhraseVariations"/> declaration against this song's own real <see cref="PhraseSegmentationAnalysis"/> boundaries.</summary>
/// <param name="StartBar">The phrase's own first bar (inclusive).</param>
/// <param name="EndBarExclusive">One past the phrase's own last bar (half-open).</param>
/// <param name="LookId">The effective Look (in the same <see cref="LookLibrary"/> <see cref="SectionScriptCompiler.Compile(LookLibrary,IReadOnlyDictionary{Guid,SectionScript},IReadOnlyDictionary{Guid,IReadOnlyList{PhraseCandidate}}?)"/> returns) this phrase's own window compiles against.</param>
public sealed record SectionPhraseLookAssignment(BarIndex StartBar, BarIndex EndBarExclusive, string LookId);

/// <summary>
/// Build-plan Phase 15 §3: one section's own name-based filler plan (real xLights effect names, e.g.
/// <c>"On"</c>, not recipe ids) -- <see cref="SectionScriptCompiler"/>'s own copy of
/// <see cref="SectionScript.FillerEffects"/>/<see cref="SectionScript.FillerEffectsByRole"/>, kept
/// name-based (not resolved to recipe ids, and not typed against the Director-only <c>BandRole</c> enum)
/// so this compiler stays a pure, I/O-free, Director-independent transformation -- the Director-layer
/// caller (<c>ChoreographyGenerationPipeline</c>) is the one place with both a real <c>EffectRecipeCatalog</c>
/// to resolve names against and the real <c>BandEnergyGroupRoleMapper</c> classification to key role-specific
/// lists by.
/// </summary>
public sealed record SectionFillerPlan(IReadOnlyList<string> FlatEffectNames, IReadOnlyDictionary<string, IReadOnlyList<string>>? ByRole);

/// <summary>
/// Build-plan Phase 6 task 3: "Wire Phase 4's Look -&gt; ChoreographyIR compiler to consume SectionScript +
/// Look instead of a hand-authored Look directly." Rather than widening <see cref="ChoreographyEngine.Input"/>'s
/// own contract to understand <see cref="SectionScript"/> (which would make every existing Phase 4 caller --
/// including every hand-authored-Look test already proven against the real track -- carry a Phase 6
/// concept it doesn't need), this produces an *effective* per-section <see cref="LookLibrary"/>: one
/// synthetic Look per section, cloned from the script's referenced base Look with its own declared
/// <see cref="SectionScript.PropBindings"/> applied, under a fresh per-section id. The resulting
/// <c>LookIdBySectionId"</c> map is exactly the shape <see cref="ChoreographyEngine.Input"/> already
/// accepts unchanged -- so Phase 4's engine (and everything it already does: Mode A/B selection, the
/// always-moving pass, legality/coverage checks) runs completely unmodified against Phase 6 output.
/// </summary>
public static class SectionScriptCompiler
{
    /// <summary>The synthetic per-section Look id this compiler mints for <paramref name="script"/> -- stable for one given script, never reused across a different section/base-Look pair.</summary>
    public static string EffectiveLookId(SectionScript script) =>
        $"{script.LookId}::section::{script.SectionId:N}";

    /// <exception cref="ChoreographyCompilationException">A script references a Look not present in <paramref name="baseLibrary"/>, or rebinds a layer z that Look does not have -- the same failures <see cref="SectionScriptValidator"/> should already have caught before this ever runs; re-checked here because this compiler must never silently fabricate a Look.</exception>
    public static (LookLibrary EffectiveLibrary, IReadOnlyDictionary<Guid, string> LookIdBySectionId, IReadOnlyDictionary<Guid, SectionFillerPlan> FillerPlanBySectionId) Compile(
        LookLibrary baseLibrary, IReadOnlyDictionary<Guid, SectionScript> scriptsBySectionId)
    {
        var (effectiveLibrary, lookIdBySectionId, _, fillerPlanBySectionId) = Compile(baseLibrary, scriptsBySectionId, phrasesBySectionId: null);
        return (effectiveLibrary, lookIdBySectionId, fillerPlanBySectionId);
    }

    /// <summary>
    /// Build-plan Phase 10 §3e task 1: the same base compile as the two-argument overload above, plus (when
    /// a script declares one or more <see cref="SectionScript.PhraseVariations"/> AND <paramref name="phrasesBySectionId"/>
    /// supplies that section's own real phrases) a per-section list of <see cref="SectionPhraseLookAssignment"/>s
    /// that together partition the section's full bar range into its own phrases -- a phrase named by a
    /// declared variation compiles against a synthetic clone with that variation's own layer subset/prop
    /// overrides applied; every other phrase of that same section compiles against the section's own base
    /// effective Look, completely unchanged, over just that phrase's own bar range.
    /// </summary>
    /// <param name="phrasesBySectionId">This song's own real phrases (<see cref="PhraseCandidate"/>), grouped by their own <see cref="PhraseCandidate.SectionLabelId"/> -- or <see langword="null"/> to skip phrase-scoped expansion entirely (every script's <see cref="SectionScript.PhraseVariations"/>, if any, is then ignored, same as the two-argument overload).</param>
    /// <exception cref="ChoreographyCompilationException">A script declares phrase variations but no phrase data is supplied for its section, declares a PhraseIndex beyond that section's own real phrase count, or a phrase-level layer/prop-binding reference does not resolve -- the same "never silently fabricate" discipline as every other check in this compiler.</exception>
    public static (LookLibrary EffectiveLibrary, IReadOnlyDictionary<Guid, string> LookIdBySectionId, IReadOnlyDictionary<Guid, IReadOnlyList<SectionPhraseLookAssignment>> PhraseLookAssignmentsBySectionId, IReadOnlyDictionary<Guid, SectionFillerPlan> FillerPlanBySectionId) Compile(
        LookLibrary baseLibrary, IReadOnlyDictionary<Guid, SectionScript> scriptsBySectionId, IReadOnlyDictionary<Guid, IReadOnlyList<PhraseCandidate>>? phrasesBySectionId)
    {
        ArgumentNullException.ThrowIfNull(baseLibrary);
        ArgumentNullException.ThrowIfNull(scriptsBySectionId);

        var effectiveLooks = new List<Look>(baseLibrary.Looks);
        var lookIdBySectionId = new Dictionary<Guid, string>();
        var phraseAssignmentsBySectionId = new Dictionary<Guid, IReadOnlyList<SectionPhraseLookAssignment>>();
        var fillerPlanBySectionId = new Dictionary<Guid, SectionFillerPlan>();

        foreach (var (sectionId, script) in scriptsBySectionId)
        {
            fillerPlanBySectionId[sectionId] = new SectionFillerPlan(
                script.FillerEffectsOrEmpty,
                script.FillerEffectsByRoleOrEmpty.Count == 0 ? null : script.FillerEffectsByRoleOrEmpty);

            var baseLook = baseLibrary.Find(script.LookId)
                ?? throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' references Look '{script.LookId}', which is not in the supplied Look library.");

            var effectiveId = EffectiveLookId(script);
            Look sectionLook;

            if (script.PropBindingsOrEmpty.Count == 0)
            {
                // No per-section rebinding at all -- the base Look is used verbatim, just under its own
                // real id, never duplicated as an identical synthetic clone.
                lookIdBySectionId[sectionId] = baseLook.Id;
                sectionLook = baseLook;
            }
            else
            {
                sectionLook = ApplyPropBindings(baseLook, effectiveId, script.PropBindingsOrEmpty, sectionId);
                effectiveLooks.Add(sectionLook);
                lookIdBySectionId[sectionId] = effectiveId;
            }

            if (script.PhraseVariationsOrEmpty.Count == 0)
            {
                continue;
            }

            if (phrasesBySectionId is null || !phrasesBySectionId.TryGetValue(sectionId, out var phrases) || phrases.Count == 0)
            {
                throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' declares {script.PhraseVariationsOrEmpty.Count} phrase-scoped variation(s), but no phrase segmentation was supplied for that section.");
            }

            var orderedPhrases = phrases.OrderBy(p => p.StartBarIndex).ToList();
            var variationsByIndex = new Dictionary<int, SectionPhraseVariation>();
            foreach (var variation in script.PhraseVariationsOrEmpty)
            {
                if (variation.PhraseIndex < 1 || variation.PhraseIndex > orderedPhrases.Count)
                {
                    throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' declares a phrase variation for phrase {variation.PhraseIndex}, but this section only has {orderedPhrases.Count} phrase(s).");
                }

                if (!variationsByIndex.TryAdd(variation.PhraseIndex, variation))
                {
                    throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' declares phrase {variation.PhraseIndex} more than once.");
                }
            }

            var sectionLookIdForUncoveredPhrases = lookIdBySectionId[sectionId];
            var assignments = new List<SectionPhraseLookAssignment>(orderedPhrases.Count);

            for (var i = 0; i < orderedPhrases.Count; i++)
            {
                var phraseIndex = i + 1;
                var phrase = orderedPhrases[i];

                if (!variationsByIndex.TryGetValue(phraseIndex, out var variation))
                {
                    // Not named by any declared variation -- compiles like today, the section's own base
                    // effective Look, unchanged, over just this phrase's own bar range.
                    assignments.Add(new SectionPhraseLookAssignment(new BarIndex(phrase.StartBarIndex), new BarIndex(phrase.EndBarIndexExclusive), sectionLookIdForUncoveredPhrases));
                    continue;
                }

                var phraseLookId = $"{effectiveId}::phrase::{phraseIndex}";
                var phraseLook = ApplyPhraseVariation(sectionLook, phraseLookId, variation, sectionId, phraseIndex);
                effectiveLooks.Add(phraseLook);
                assignments.Add(new SectionPhraseLookAssignment(new BarIndex(phrase.StartBarIndex), new BarIndex(phrase.EndBarIndexExclusive), phraseLookId));
            }

            phraseAssignmentsBySectionId[sectionId] = assignments;
        }

        return (new LookLibrary(effectiveLooks), lookIdBySectionId, phraseAssignmentsBySectionId, fillerPlanBySectionId);
    }

    /// <summary>
    /// Real-run finding (2026-10-31): a plain <c>Layers.ToDictionary(l =&gt; l.Z)</c> throws a raw,
    /// unhelpful framework <see cref="ArgumentException"/> ("An item with the same key has already been
    /// added. Key: 0") when a Look has two layers at the same Z -- <see cref="ShowBibleValidator"/> now
    /// rejects that upstream (forcing a real retry instead), but this compiler's own doc comment already
    /// promises it "must never silently fabricate a Look," so this is re-checked here too, as a real
    /// <see cref="ChoreographyCompilationException"/> rather than trusting the upstream check alone.
    /// </summary>
    private static Dictionary<int, LookLayer> BuildLayersByZ(IReadOnlyList<LookLayer> layers, string context)
    {
        var layersByZ = new Dictionary<int, LookLayer>();
        foreach (var layer in layers)
        {
            if (!layersByZ.TryAdd(layer.Z, layer))
            {
                throw new ChoreographyCompilationException($"{context} has more than one layer at z={layer.Z} -- every layer must have a distinct Z.");
            }
        }

        return layersByZ;
    }

    private static Look ApplyPropBindings(Look baseLook, string effectiveId, IReadOnlyList<SectionEventBinding> bindings, Guid sectionId)
    {
        var layersByZ = BuildLayersByZ(baseLook.Layers, $"Look '{baseLook.Id}'");
        foreach (var binding in bindings)
        {
            if (!layersByZ.TryGetValue(binding.LayerZ, out var layer))
            {
                throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' rebinds layer z={binding.LayerZ} of Look '{baseLook.Id}', which has no such layer.");
            }

            layersByZ[binding.LayerZ] = layer with { Props = binding.Props };
        }

        return baseLook with
        {
            Id = effectiveId,
            Layers = baseLook.Layers.Select(l => layersByZ[l.Z]).ToList(),
        };
    }

    /// <summary>Applies one phrase's own layer-subset/prop-binding overrides on top of <paramref name="sectionLook"/> (the section's own already-resolved effective Look, section-wide prop bindings already applied) -- never the raw base Look, so a phrase with no declared <see cref="SectionPhraseVariation.PropBindings"/> for a layer still carries that layer's own section-wide rebinding, if any.</summary>
    private static Look ApplyPhraseVariation(Look sectionLook, string phraseLookId, SectionPhraseVariation variation, Guid sectionId, int phraseIndex)
    {
        var layersByZ = BuildLayersByZ(sectionLook.Layers, $"Look '{sectionLook.Id}'");
        foreach (var binding in variation.PropBindingsOrEmpty)
        {
            if (!layersByZ.TryGetValue(binding.LayerZ, out var layer))
            {
                throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' phrase {phraseIndex} rebinds layer z={binding.LayerZ} of Look '{sectionLook.Id}', which has no such layer.");
            }

            if (string.IsNullOrWhiteSpace(binding.Props))
            {
                throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' phrase {phraseIndex} layer z={binding.LayerZ} rebinding has a blank props reference.");
            }

            layersByZ[binding.LayerZ] = layer with { Props = binding.Props };
        }

        List<LookLayer> activeLayers;
        if (variation.ActiveLayerZs is null)
        {
            activeLayers = sectionLook.Layers.Select(l => layersByZ[l.Z]).ToList();
        }
        else
        {
            activeLayers = new List<LookLayer>(variation.ActiveLayerZsOrEmpty.Count);
            foreach (var z in variation.ActiveLayerZsOrEmpty)
            {
                if (!layersByZ.TryGetValue(z, out var layer))
                {
                    throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' phrase {phraseIndex} declares active layer z={z} of Look '{sectionLook.Id}', which has no such layer.");
                }

                activeLayers.Add(layer);
            }

            activeLayers = activeLayers.OrderBy(l => l.Z).ToList();
        }

        if (activeLayers.Count == 0)
        {
            throw new ChoreographyCompilationException($"SectionScript for section '{sectionId}' phrase {phraseIndex} declares an empty active-layer set -- a phrase variation must keep at least one layer active.");
        }

        return sectionLook with { Id = phraseLookId, Layers = activeLayers };
    }
}
