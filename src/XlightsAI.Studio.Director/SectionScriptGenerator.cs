using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>
/// One <see cref="SectionScriptGenerator.GenerateAsync"/> call's full, inspectable outcome -- mirrors
/// <see cref="ShowBibleGenerationReport"/>'s own shape.
/// </summary>
/// <param name="Timeline">
/// Build-plan Phase 28a: the full <see cref="SectionTimeline"/> this call validated against, including
/// any real <see cref="SectionTimeline.Cues"/> -- <see langword="null"/> whenever <see cref="Script"/>
/// is (the same "no validated result" case). Additive/trailing: every pre-existing caller that only
/// reads <see cref="Script"/> is unaffected.
/// </param>
public sealed record SectionScriptGenerationReport(
    SectionScript? Script,
    int AttemptCount,
    bool ValidatedOnFirstAttempt,
    IReadOnlyList<SectionScriptValidationError> FinalViolations,
    IReadOnlyList<AiUsage?> UsagePerAttempt,
    SectionTimeline? Timeline = null);

/// <summary>
/// Build-plan Phase 6 task 1: one section's own Choreographer call, with the same "validate, and on
/// violation retry with the exact violation list appended to the volatile suffix" discipline
/// <see cref="ShowBibleGenerator"/> already established for L2 -- the stable prefix (the Show Bible +
/// vocabulary + craft rules) is untouched by a retry, so its own cache eligibility survives. Used directly
/// by a single-section caller, and composed by <see cref="SectionScriptBatchGenerator"/> for the
/// whole-song, many-sections case (build-plan Phase 6 task 2).
/// </summary>
public sealed class SectionScriptGenerator
{
    private readonly StructuredJsonAiClient<SectionTimeline> _client;
    private readonly int _maxAttempts;
    private readonly JsonAiCallLogStore? _callLog;
    private readonly string? _modelName;

    public SectionScriptGenerator(IChatCompletionClient client, int maxAttempts = 3, JsonAiCallLogStore? callLog = null, string? modelName = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Must be positive.");
        }

        _client = new StructuredJsonAiClient<SectionTimeline>(client);
        _maxAttempts = maxAttempts;
        _callLog = callLog;
        _modelName = modelName;
    }

    /// <param name="anchorContext">
    /// Build-plan Phase 28a: this section's own real music context/signals, or <see langword="null"/>
    /// (the default, and every pre-existing caller's behavior) -- required for
    /// <see cref="SectionTimelineValidator"/> to run against a real response's <see cref="SectionTimeline.Cues"/>.
    /// A response with no real Cues (every caller today, and any caller that has not wired §4's new
    /// prompt sections to actually request them) always falls back to today's
    /// <see cref="SectionScriptValidator"/> path unchanged, regardless of whether this is supplied.
    /// </param>
    public async Task<SectionScriptGenerationReport> GenerateAsync(
        string stablePrefix, string volatileSuffix, Guid songId, LookLibrary lookLibrary, DisplayGraph layout,
        EffectVocabularyCatalog vocabulary, int maxOutputTokens, CancellationToken ct,
        IReadOnlyDictionary<Guid, int>? phraseCountBySectionId = null, SectionTimelineAnchorContext? anchorContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stablePrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(volatileSuffix);
        ArgumentNullException.ThrowIfNull(lookLibrary);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(vocabulary);

        var usages = new List<AiUsage?>();
        IReadOnlyList<SectionScriptValidationError> violations = [];
        var correction = "";
        SectionTimeline? lastParsedValue = null;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var userPrompt = correction.Length == 0
                ? volatileSuffix
                : $"{volatileSuffix}\n\n## Correction needed\nYour previous JSON response failed validation:\n{correction}\nReturn the full corrected JSON object again, fixing every issue above.";

            var request = new ChatCompletionRequest(stablePrefix, userPrompt, CacheSystemPrompt: true, maxOutputTokens);

            StructuredAiCallResult<SectionTimeline> result;
            try
            {
                result = await _client.CompleteAsync(request, ct).ConfigureAwait(false);
            }
            catch (AiCallRejectedException ex)
            {
                usages.Add(null);
                violations = [new SectionScriptValidationError("transport", ex.Message)];
                correction = ex.Message;
                await LogAsync(songId, new AiUsage(null, null, null, null), $"attempt {attempt}/{_maxAttempts}: transport rejection -- {ex.Message}", ex.Diagnostics, ct).ConfigureAwait(false);
                continue;
            }

            usages.Add(result.Usage);
            lastParsedValue = result.Value;
            violations = Validate(result.Value, lookLibrary, layout, vocabulary, phraseCountBySectionId, anchorContext);
            // TEMPORARY DIAGNOSTIC (not a logic change -- revert before merging): full violation detail +
            // raw response JSON, so a real retry's actual cause is inspectable from the call log instead
            // of only a bare count.
            var diagnosticDetail = violations.Count == 0
                ? "validated"
                : $"{violations.Count} violation(s): {string.Join(" | ", violations.Select(v => $"[{v.Rule}] {v.Message}"))}";
            await LogAsync(songId, result.Usage, $"attempt {attempt}/{_maxAttempts}: {diagnosticDetail}\nRAW RESPONSE: {result.RawJson}", result.Diagnostics, ct).ConfigureAwait(false);

            if (violations.Count == 0)
            {
                return new SectionScriptGenerationReport(result.Value.ToSectionScript(), attempt, attempt == 1, [], usages, result.Value);
            }

            correction = string.Join("\n", violations.Select(v => $"- [{v.Rule}] {v.Message}"));
        }

        // Build-plan Phase 28c: "a model that can't produce valid cues should degrade to the old
        // behavior, not fail the whole section." Every retry above re-validated the FULL response
        // (including Cues) through SectionTimelineValidator. If the retry budget is exhausted but the
        // LAST real attempt still parsed into a structurally sound SectionTimeline, try once more
        // dropping its own Cues entirely and validating the resulting flat SectionScript through
        // today's unchanged SectionScriptValidator -- the exact old flat-LookId-only path, reused as a
        // last-resort fallback rather than a hard dependency on the model ever producing valid cues.
        // Never attempted for a transport-only failure (lastParsedValue stays null when every attempt
        // was a transport rejection, never a real parsed response to fall back to).
        if (lastParsedValue is { CuesOrEmpty.Count: > 0 } cuedValue)
        {
            var flatScript = cuedValue.ToSectionScript();
            var flatViolations = SectionScriptValidator.Validate(flatScript, lookLibrary, layout, vocabulary, phraseCountBySectionId);
            if (flatViolations.Count == 0)
            {
                return new SectionScriptGenerationReport(flatScript, _maxAttempts, false, [], usages);
            }
        }

        return new SectionScriptGenerationReport(null, _maxAttempts, false, violations, usages);
    }

    /// <summary>
    /// Build-plan Phase 28a §6's own backward-compatibility seam: a response with real
    /// <see cref="SectionTimeline.Cues"/> AND a real <paramref name="anchorContext"/> to check anchors
    /// against validates through <see cref="SectionTimelineValidator"/>; every other response (no real
    /// Cues, or no anchor context supplied) validates through today's unchanged
    /// <see cref="SectionScriptValidator"/> path, via <see cref="SectionTimeline.ToSectionScript"/>.
    /// </summary>
    internal static IReadOnlyList<SectionScriptValidationError> Validate(
        SectionTimeline timeline, LookLibrary lookLibrary, DisplayGraph layout, EffectVocabularyCatalog vocabulary,
        IReadOnlyDictionary<Guid, int>? phraseCountBySectionId, SectionTimelineAnchorContext? anchorContext)
    {
        if (timeline.CuesOrEmpty.Count > 0 && anchorContext is not null)
        {
            return SectionTimelineValidator.Validate(
                timeline, lookLibrary, layout, anchorContext.Context, anchorContext.Phrases, anchorContext.TransientHits,
                anchorContext.VocalNotes, anchorContext.LeadInstrumentWindows, vocabulary);
        }

        return SectionScriptValidator.Validate(timeline.ToSectionScript(), lookLibrary, layout, vocabulary, phraseCountBySectionId);
    }

    private async Task LogAsync(Guid songId, AiUsage usage, string note, RequestDiagnostics? diagnostics, CancellationToken ct)
    {
        if (_callLog is null || _modelName is null)
        {
            return;
        }

        var cost = AiCostEstimator.EstimateUsd(_modelName, usage);
        await _callLog.AppendAsync(new AiCallLogEntry(songId, AiRole.Choreographer, _modelName, DateTimeOffset.UtcNow, usage, cost, note, diagnostics), ct).ConfigureAwait(false);
    }
}
