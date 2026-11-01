using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 6 task 1/4: every <see cref="SectionScript"/> a Choreographer call returns must
/// validate before it is trusted -- same "reject and retry on violations" discipline
/// <c>XlightsAI.Studio.Director.ShowBibleValidator</c> already established for L2. Never throws; always
/// returns the full violation list so the Director-project generator orchestrating these calls can decide
/// whether to retry.
/// </summary>
public static class SectionScriptValidator
{
    /// <param name="vocabulary">
    /// Build-plan Phase 15 §2e: the same generated effect vocabulary the Choreographer prompt itself is
    /// built from -- every name in <see cref="SectionScript.FillerEffects"/>/<see cref="SectionScript.FillerEffectsByRole"/>
    /// must be a real, registered effect in it, never an invented one.
    /// </param>
    public static IReadOnlyList<SectionScriptValidationError> Validate(SectionScript script, LookLibrary lookLibrary, DisplayGraph layout, EffectVocabularyCatalog vocabulary)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(lookLibrary);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(vocabulary);

        var errors = new List<SectionScriptValidationError>();

        ValidateFillerEffects(script, vocabulary, errors);

        var look = lookLibrary.Find(script.LookId);
        if (look is null)
        {
            errors.Add(new SectionScriptValidationError("look-ref", $"SectionScript for section '{script.SectionId}' references Look '{script.LookId}', which is not in the supplied Show Bible's Look library."));
            return errors; // Nothing below is checkable without a real Look to check it against.
        }

        if (script.Variation is { Length: > 0 } variation && !look.VariationsOrEmpty.Contains(variation, StringComparer.Ordinal))
        {
            errors.Add(new SectionScriptValidationError("variation-ref", $"SectionScript for section '{script.SectionId}' declares variation '{variation}', which Look '{look.Id}' does not declare as one of its own Variations."));
        }

        var layerZSet = look.Layers.Select(l => l.Z).ToHashSet();
        foreach (var binding in script.PropBindingsOrEmpty)
        {
            if (!layerZSet.Contains(binding.LayerZ))
            {
                errors.Add(new SectionScriptValidationError("prop-binding-layer", $"SectionScript for section '{script.SectionId}' rebinds layer z={binding.LayerZ} of Look '{look.Id}', which has no such layer."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.Props))
            {
                errors.Add(new SectionScriptValidationError("prop-binding-props", $"SectionScript for section '{script.SectionId}' layer z={binding.LayerZ} rebinding has a blank props reference."));
                continue;
            }

            try
            {
                PropGroupResolver.Resolve(binding.Props, layout);
            }
            catch (ArgumentException)
            {
                errors.Add(new SectionScriptValidationError("prop-binding-props", $"SectionScript for section '{script.SectionId}' layer z={binding.LayerZ} rebinds to '{binding.Props}', which does not resolve against the real layout."));
            }
        }

        ValidatePhraseVariations(script, look, layerZSet, layout, errors);

        return errors;
    }

    private static readonly string[] KnownRoleKeys = ["Foundation", "Subject", "Accent"];

    /// <summary>
    /// Build-plan Phase 15 §2d/§2e: the flat <see cref="SectionScript.FillerEffects"/> list is required
    /// (missing/empty is retry-worthy, same discipline an invalid <see cref="SectionScript.LookId"/>
    /// already goes through), must have at least <see cref="FillerPool.MinimumDistinctStates"/> distinct
    /// names, and every name must be a real effect in <paramref name="vocabulary"/> -- never invented.
    /// Every present <see cref="SectionScript.FillerEffectsByRole"/> entry is held to the identical
    /// distinct-count/known-effect rules, plus its own key must be one of the real <c>BandRole</c> names.
    /// Checked independently of (and before) the Look-reference check above, since a missing filler plan
    /// is its own retry-worthy defect regardless of whether the declared Look is valid.
    /// </summary>
    private static void ValidateFillerEffects(SectionScript script, EffectVocabularyCatalog vocabulary, List<SectionScriptValidationError> errors)
    {
        if (script.FillerEffectsOrEmpty.Count == 0)
        {
            errors.Add(new SectionScriptValidationError("filler-effects-missing", $"SectionScript for section '{script.SectionId}' does not declare FillerEffects -- every section must declare a flat rotating filler pool of at least {FillerPool.MinimumDistinctStates} distinct real effect names."));
        }
        else
        {
            ValidateFillerEffectList(script.SectionId, "FillerEffects", script.FillerEffectsOrEmpty, vocabulary, "filler-effects-distinct-count", "filler-effects-unknown-effect", errors);
        }

        foreach (var (roleKey, names) in script.FillerEffectsByRoleOrEmpty)
        {
            if (!KnownRoleKeys.Contains(roleKey, StringComparer.Ordinal))
            {
                errors.Add(new SectionScriptValidationError("filler-effects-by-role-unknown-role", $"SectionScript for section '{script.SectionId}' declares FillerEffectsByRole key '{roleKey}', which is not one of the real band roles ({string.Join("/", KnownRoleKeys)})."));
            }

            ValidateFillerEffectList(script.SectionId, $"FillerEffectsByRole['{roleKey}']", names, vocabulary, "filler-effects-by-role-distinct-count", "filler-effects-by-role-unknown-effect", errors);
        }
    }

    private static void ValidateFillerEffectList(
        Guid sectionId, string fieldLabel, IReadOnlyList<string> names, EffectVocabularyCatalog vocabulary,
        string distinctCountRule, string unknownEffectRule, List<SectionScriptValidationError> errors)
    {
        var distinctCount = names.Distinct(StringComparer.Ordinal).Count();
        if (distinctCount < FillerPool.MinimumDistinctStates)
        {
            errors.Add(new SectionScriptValidationError(distinctCountRule, $"SectionScript for section '{sectionId}' declares {fieldLabel} with only {distinctCount} distinct name(s); at least {FillerPool.MinimumDistinctStates} are required."));
        }

        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            if (!vocabulary.Effects.ContainsKey(name))
            {
                errors.Add(new SectionScriptValidationError(unknownEffectRule, $"SectionScript for section '{sectionId}' declares {fieldLabel} name '{name}', which is not a real effect in the supplied vocabulary."));
            }
        }
    }

    /// <summary>Build-plan Phase 10 §3e: structural checks on <see cref="SectionScript.PhraseVariations"/> this validator CAN make without knowing the real phrase count for this section (that cross-check -- a declared PhraseIndex beyond how many phrases this section actually has -- belongs to <see cref="SectionScriptCompiler"/>, the one place that is ever handed the real <c>PhraseSegmentationAnalysis</c>, same "re-checked here because this compiler must never silently fabricate a Look" discipline its own doc comment already states for layer/prop-binding references).</summary>
    private static void ValidatePhraseVariations(SectionScript script, Look look, HashSet<int> layerZSet, DisplayGraph layout, List<SectionScriptValidationError> errors)
    {
        if (script.PhraseVariationsOrEmpty.Count > 3)
        {
            errors.Add(new SectionScriptValidationError("phrase-variation-count", $"SectionScript for section '{script.SectionId}' declares {script.PhraseVariationsOrEmpty.Count} phrase-scoped variations; at most 3 are supported (build-plan Phase 10 §2e: a small number of staged phrases, not a per-phrase rewrite)."));
        }

        var seenPhraseIndexes = new HashSet<int>();
        foreach (var variation in script.PhraseVariationsOrEmpty)
        {
            if (variation.PhraseIndex < 1)
            {
                errors.Add(new SectionScriptValidationError("phrase-variation-index", $"SectionScript for section '{script.SectionId}' declares a phrase variation with PhraseIndex={variation.PhraseIndex}; PhraseIndex is 1-based and must be >= 1."));
            }
            else if (!seenPhraseIndexes.Add(variation.PhraseIndex))
            {
                errors.Add(new SectionScriptValidationError("phrase-variation-duplicate", $"SectionScript for section '{script.SectionId}' declares phrase {variation.PhraseIndex} more than once."));
            }

            if (variation.ActiveLayerZs is { Count: 0 })
            {
                errors.Add(new SectionScriptValidationError("phrase-variation-layers", $"SectionScript for section '{script.SectionId}' phrase {variation.PhraseIndex} declares an empty ActiveLayerZs -- omit the field to use every layer, or name at least one."));
            }
            else if (variation.ActiveLayerZs is not null)
            {
                foreach (var z in variation.ActiveLayerZs)
                {
                    if (!layerZSet.Contains(z))
                    {
                        errors.Add(new SectionScriptValidationError("phrase-variation-layers", $"SectionScript for section '{script.SectionId}' phrase {variation.PhraseIndex} names active layer z={z} of Look '{look.Id}', which has no such layer."));
                    }
                }
            }

            foreach (var binding in variation.PropBindingsOrEmpty)
            {
                if (!layerZSet.Contains(binding.LayerZ))
                {
                    errors.Add(new SectionScriptValidationError("phrase-variation-prop-binding-layer", $"SectionScript for section '{script.SectionId}' phrase {variation.PhraseIndex} rebinds layer z={binding.LayerZ} of Look '{look.Id}', which has no such layer."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.Props))
                {
                    errors.Add(new SectionScriptValidationError("phrase-variation-prop-binding-props", $"SectionScript for section '{script.SectionId}' phrase {variation.PhraseIndex} layer z={binding.LayerZ} rebinding has a blank props reference."));
                    continue;
                }

                try
                {
                    PropGroupResolver.Resolve(binding.Props, layout);
                }
                catch (ArgumentException)
                {
                    errors.Add(new SectionScriptValidationError("phrase-variation-prop-binding-props", $"SectionScript for section '{script.SectionId}' phrase {variation.PhraseIndex} layer z={binding.LayerZ} rebinds to '{binding.Props}', which does not resolve against the real layout."));
                }
            }
        }
    }
}
