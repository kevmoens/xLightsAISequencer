using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 7d: "Wire directives back into Phase 6 to re-run only affected SectionScripts,
/// leaving the Show Bible untouched unless a directive specifically targets it." Architecture §9's own
/// "Revise" text, verbatim: "Only affected SectionScripts re-run; the expensive Show Bible call is usually
/// untouched. That is what makes iteration affordable." This class is the enforcement point for that rule:
/// it builds the batch item list handed to <see cref="SectionScriptBatchGenerator"/> from ONLY the
/// directed sections, and merges the regenerated subset back into the existing script dictionary -- every
/// section with no directive keeps its exact prior <see cref="SectionScript"/> instance, never
/// re-serialized, never re-sent to any <see cref="IChatCompletionClient"/> call.
/// </summary>
public static class TargetedSectionRegenerator
{
    /// <exception cref="ArgumentException">A directive targets a section id not present in <paramref name="volatileSuffixBySectionId"/> (nothing to regenerate it from).</exception>
    public static async Task<IReadOnlyDictionary<Guid, SectionScript>> RegenerateAsync(
        SectionScriptBatchGenerator batchGenerator,
        string stablePrefix,
        IReadOnlyDictionary<Guid, SectionScript> existingScripts,
        IReadOnlyList<RevisionDirective> directives,
        IReadOnlyDictionary<Guid, string> volatileSuffixBySectionId,
        Guid songId,
        LookLibrary lookLibrary,
        DisplayGraph layout,
        int maxOutputTokens,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(batchGenerator);
        ArgumentException.ThrowIfNullOrWhiteSpace(stablePrefix);
        ArgumentNullException.ThrowIfNull(existingScripts);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(volatileSuffixBySectionId);
        ArgumentNullException.ThrowIfNull(lookLibrary);
        ArgumentNullException.ThrowIfNull(layout);

        // One directive per targeted section, merging every directive's instruction text for sections
        // with more than one (a numeric finding and a visual loss can target the same section) -- never
        // more than one batch item per section, so a section is never double-counted in the call.
        var directivesBySection = directives
            .GroupBy(d => d.SectionId)
            .ToDictionary(g => g.Key, g => string.Join(" ", g.Select(d => d.Instruction)));

        if (directivesBySection.Count == 0)
        {
            return existingScripts;
        }

        var items = new List<SectionScriptBatchItem>();
        foreach (var sectionId in directivesBySection.Keys)
        {
            if (!volatileSuffixBySectionId.TryGetValue(sectionId, out var baseSuffix))
            {
                throw new ArgumentException($"Directive targets section '{sectionId}', which has no volatile prompt suffix supplied -- nothing to regenerate it from.", nameof(volatileSuffixBySectionId));
            }

            var directedSuffix = baseSuffix + Environment.NewLine + Environment.NewLine +
                "## Revision directive for THIS section only" + Environment.NewLine + directivesBySection[sectionId];
            items.Add(new SectionScriptBatchItem(sectionId, directedSuffix));
        }

        var regenerated = await batchGenerator.GenerateAllAsync(stablePrefix, items, songId, lookLibrary, layout, maxOutputTokens, ct).ConfigureAwait(false);

        // Start from the untouched existing dictionary (same instances, never re-created) and overwrite
        // only the sections that actually regenerated successfully -- a section whose regeneration never
        // validated keeps its prior script rather than being dropped.
        var merged = new Dictionary<Guid, SectionScript>(existingScripts);
        foreach (var (sectionId, report) in regenerated)
        {
            if (report.Script is not null)
            {
                merged[sectionId] = report.Script;
            }
        }

        return merged;
    }
}
