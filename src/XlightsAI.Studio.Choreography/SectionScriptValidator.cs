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
    public static IReadOnlyList<SectionScriptValidationError> Validate(SectionScript script, LookLibrary lookLibrary, DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(lookLibrary);
        ArgumentNullException.ThrowIfNull(layout);

        var errors = new List<SectionScriptValidationError>();

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
