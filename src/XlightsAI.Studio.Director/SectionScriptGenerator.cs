using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>One <see cref="SectionScriptGenerator.GenerateAsync"/> call's full, inspectable outcome -- mirrors <see cref="ShowBibleGenerationReport"/>'s own shape.</summary>
public sealed record SectionScriptGenerationReport(
    SectionScript? Script,
    int AttemptCount,
    bool ValidatedOnFirstAttempt,
    IReadOnlyList<SectionScriptValidationError> FinalViolations,
    IReadOnlyList<AiUsage?> UsagePerAttempt);

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
    private readonly StructuredJsonAiClient<SectionScript> _client;
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

        _client = new StructuredJsonAiClient<SectionScript>(client);
        _maxAttempts = maxAttempts;
        _callLog = callLog;
        _modelName = modelName;
    }

    public async Task<SectionScriptGenerationReport> GenerateAsync(
        string stablePrefix, string volatileSuffix, Guid songId, LookLibrary lookLibrary, DisplayGraph layout,
        int maxOutputTokens, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stablePrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(volatileSuffix);
        ArgumentNullException.ThrowIfNull(lookLibrary);
        ArgumentNullException.ThrowIfNull(layout);

        var usages = new List<AiUsage?>();
        IReadOnlyList<SectionScriptValidationError> violations = [];
        var correction = "";

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var userPrompt = correction.Length == 0
                ? volatileSuffix
                : $"{volatileSuffix}\n\n## Correction needed\nYour previous JSON response failed validation:\n{correction}\nReturn the full corrected JSON object again, fixing every issue above.";

            var request = new ChatCompletionRequest(stablePrefix, userPrompt, CacheSystemPrompt: true, maxOutputTokens);

            StructuredAiCallResult<SectionScript> result;
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
            violations = SectionScriptValidator.Validate(result.Value, lookLibrary, layout);
            await LogAsync(songId, result.Usage, $"attempt {attempt}/{_maxAttempts}: {(violations.Count == 0 ? "validated" : $"{violations.Count} violation(s)")}", result.Diagnostics, ct).ConfigureAwait(false);

            if (violations.Count == 0)
            {
                return new SectionScriptGenerationReport(result.Value, attempt, attempt == 1, [], usages);
            }

            correction = string.Join("\n", violations.Select(v => $"- [{v.Rule}] {v.Message}"));
        }

        return new SectionScriptGenerationReport(null, _maxAttempts, false, violations, usages);
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
