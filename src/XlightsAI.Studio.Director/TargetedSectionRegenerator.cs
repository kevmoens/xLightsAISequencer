using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;
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
///
/// <para>
/// A targeted section's own prior <see cref="SectionScript"/> content (when one already exists) is
/// included directly in that section's own regeneration prompt (user feedback, 2026-11-03) -- a directive
/// asking the Choreographer to change ONE specific thing and "keep everything else as-is" is otherwise an
/// instruction it has no way to honor, since nothing else would ever show it what "as-is" currently means.
/// Confirmed as a real defect, not a hypothetical: a real post-processing touch-up came back measurably
/// thinner (smaller exported <c>.xsq</c>) than the section it replaced before this fix.
/// </para>
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
        EffectVocabularyCatalog vocabulary,
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
        ArgumentNullException.ThrowIfNull(vocabulary);

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

            var directedSuffix = baseSuffix;

            // User feedback (2026-11-03): a real touch-up came back noticeably thinner than the section it
            // replaced, despite the directive below explicitly asking to "keep everything else as close to
            // current form as reasonable" -- confirmed, via the real persisted project files and LLM call
            // log, that this was a genuine blind spot: the Choreographer was never shown what the section
            // currently contained, so "keep the rest as-is" was an instruction with nothing to keep
            // anything AS. Including the section's own current SectionScript here gives every targeted
            // regeneration (this self-healing repair pass AND a user's own post-processing touch-up) a real
            // reference to preserve, not just a verbal request it has no way to honor.
            if (existingScripts.TryGetValue(sectionId, out var currentScript))
            {
                var currentScriptJson = System.Text.Json.JsonSerializer.Serialize(
                    currentScript, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                directedSuffix += Environment.NewLine + Environment.NewLine +
                    "## This section's CURRENT content -- keep it as-is except for what the directive below specifically asks you to change" +
                    Environment.NewLine + currentScriptJson;
            }

            directedSuffix += Environment.NewLine + Environment.NewLine +
                "## Revision directive for THIS section only" + Environment.NewLine + directivesBySection[sectionId];
            items.Add(new SectionScriptBatchItem(sectionId, directedSuffix));
        }

        var regenerated = await batchGenerator.GenerateAllAsync(stablePrefix, items, songId, lookLibrary, layout, vocabulary, maxOutputTokens, ct).ConfigureAwait(false);

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
