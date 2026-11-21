namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Build-plan Phase 30 step 30.1: "Build a compatibility signal from mined data: which blend modes
/// commonly pair with which effect-type pairs and read well (not muddy)." Deliberately does not invent a
/// new quality metric -- the "read well, not muddy" judgment is a real human's, already baked into every
/// mined <see cref="MultiLayerTemplate"/> (a real, repeatedly-authored stack is evidence it read well;
/// nobody repeats a muddy combination across multiple real shows), and the existing
/// <see cref="EffectVisualCharacterCatalog"/>/<see cref="BrightnessTier"/> brightness signal
/// (<c>XlightsAI.Studio.Choreography.EmphasisContrastPlanner</c>'s own "two tiers differ by at least one
/// step = contrast, not muddy" rule) is reused as-is to break ties among several candidates with equal
/// mined support, rather than a fresh metric being invented here.
/// </summary>
public static class BlendModeCompatibilityIndex
{
    /// <summary>One real, confirmed blend-mode candidate's own mined support for one real effect-type pair.</summary>
    /// <param name="CandidateName">The <see cref="ConfirmedLayerBlendMode.CandidateName"/> this score is for.</param>
    /// <param name="MinedTemplateCount">How many distinct catalog templates recorded this exact (effect pair, candidate) combination -- never a song count (the catalog itself has already applied its own song-repetition promotion bar; this just counts shippable templates), but still real, mined evidence, never fabricated.</param>
    /// <param name="CorpusValidatedCount">Of <paramref name="MinedTemplateCount"/>, how many came from a <c>"corpus-validated"</c> (not merely <c>"single-song-showcase"</c>) template -- the stronger form of evidence.</param>
    /// <param name="BrightnessContrast">
    /// <see langword="true"/> when both effects in the pair have a known <see cref="BrightnessTier"/> and
    /// those tiers differ (the exact same non-muddy signal
    /// <c>XlightsAI.Studio.Choreography.EmphasisContrastFinding.HasContrast</c> already uses) --
    /// <see langword="false"/> when they are the same tier or either is unresolved. A same-tier pairing
    /// is not rejected (mined evidence always wins when it exists), only deprioritized as a tie-breaker.
    /// </param>
    public sealed record BlendModeCompatibilityScore(string CandidateName, int MinedTemplateCount, int CorpusValidatedCount, bool BrightnessContrast)
    {
        /// <summary>Corpus-validated evidence counts double a single-song-showcase's, and genuine brightness contrast breaks an otherwise-tied score -- deliberately simple, since the mined counts themselves (not this weighting) are the real signal.</summary>
        public int Weight => (MinedTemplateCount + CorpusValidatedCount) * 10 + (BrightnessContrast ? 1 : 0);
    }

    /// <summary>
    /// Every confirmed candidate this catalog has real mined support for, between <paramref name="prevEffect"/>
    /// (the layer below) and <paramref name="nextEffect"/> (the layer stacked on top of it), ordered by
    /// strongest evidence first. Empty when the catalog records no real occurrence of this exact effect
    /// pair ever carrying a confirmed blend mode -- never a guessed/default entry.
    /// </summary>
    public static IReadOnlyList<BlendModeCompatibilityScore> ScoresFor(
        MultiLayerTemplateCatalog templates, EffectVisualCharacterCatalog brightnessCatalog, string prevEffect, string nextEffect)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(brightnessCatalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(prevEffect);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextEffect);

        var prevTier = brightnessCatalog.Find(prevEffect)?.BrightnessTier;
        var nextTier = brightnessCatalog.Find(nextEffect)?.BrightnessTier;
        var brightnessContrast = prevTier is { } p && nextTier is { } n && p != n;

        var tallies = new Dictionary<string, (int Total, int CorpusValidated)>(StringComparer.Ordinal);

        foreach (var template in templates.Templates)
        {
            for (var i = 1; i < template.Layers.Count; i++)
            {
                var candidate = template.Layers[i].BlendModeToPrevious;
                if (candidate is null)
                {
                    continue;
                }

                if (!string.Equals(template.Layers[i - 1].Effect, prevEffect, StringComparison.Ordinal) ||
                    !string.Equals(template.Layers[i].Effect, nextEffect, StringComparison.Ordinal))
                {
                    continue;
                }

                tallies.TryGetValue(candidate, out var existing);
                var corpusValidated = string.Equals(template.ConfidenceTier, "corpus-validated", StringComparison.Ordinal) ? 1 : 0;
                tallies[candidate] = (existing.Total + 1, existing.CorpusValidated + corpusValidated);
            }
        }

        return tallies
            .Select(kv => new BlendModeCompatibilityScore(kv.Key, kv.Value.Total, kv.Value.CorpusValidated, brightnessContrast))
            .OrderByDescending(s => s.Weight)
            .ThenBy(s => s.CandidateName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The single strongest real, mined, confirmed candidate for this effect pair, or <see langword="null"/> when the catalog has no real evidence at all for it -- never a guessed fallback.</summary>
    public static string? SelectBlendMode(
        MultiLayerTemplateCatalog templates, EffectVisualCharacterCatalog brightnessCatalog, string prevEffect, string nextEffect) =>
        ScoresFor(templates, brightnessCatalog, prevEffect, nextEffect).FirstOrDefault()?.CandidateName;
}
