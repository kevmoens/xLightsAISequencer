using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>One <see cref="ShowBibleGenerator.GenerateAsync"/> run's full, inspectable outcome.</summary>
/// <param name="Bible">The validated, generated Show Bible, or <see langword="null"/> if every attempt was exhausted without one validating.</param>
/// <param name="AttemptCount">How many calls were actually made.</param>
/// <param name="ValidatedOnFirstAttempt">Whether the very first call's own output validated with zero violations -- the acceptance criterion's own "validates on first attempt" signal.</param>
/// <param name="FinalViolations">The last attempt's own violations -- empty when <see cref="Bible"/> is non-null.</param>
/// <param name="UsagePerAttempt">Every attempt's own reported usage, in call order -- <see langword="null"/> entries mark an attempt that failed at the transport level before any usage was reported.</param>
public sealed record ShowBibleGenerationReport(
    ShowBible? Bible,
    int AttemptCount,
    bool ValidatedOnFirstAttempt,
    IReadOnlyList<ShowBibleValidationError> FinalViolations,
    IReadOnlyList<AiUsage?> UsagePerAttempt);

/// <summary>
/// Build-plan Phase 5 task 5: "Validate every returned Look against [the generated] schema, rejecting
/// and retrying on violations." Orchestrates prompt -&gt; structured call -&gt; <see cref="ShowBibleValidator"/>
/// -&gt; (on violation) a corrective retry appending the exact violation list to the volatile prompt
/// suffix (the stable prefix, and therefore its own cache eligibility, is untouched by a retry) -&gt; on
/// success, the caller persists via <see cref="JsonShowBibleStore"/>.
/// </summary>
public sealed class ShowBibleGenerator
{
    private readonly StructuredJsonAiClient<ShowBibleContent> _client;
    private readonly int _maxAttempts;
    private readonly JsonAiCallLogStore? _callLog;
    private readonly string? _modelName;

    /// <param name="client">The configured Director-role transport.</param>
    /// <param name="maxAttempts">Validation-retry budget.</param>
    /// <param name="callLog">
    /// Architecture §13's own <c>llm/</c> call log -- optional and nullable (defaults to no logging) so
    /// every existing call site/test keeps compiling unchanged; when supplied, every attempt (successful
    /// or not) is appended via <see cref="JsonAiCallLogStore.AppendAsync"/>.
    /// </param>
    /// <param name="modelName">The Director role's own configured model id, used only for the call-log entry and <see cref="AiCostEstimator"/> -- never sent to the provider by this type.</param>
    public ShowBibleGenerator(IChatCompletionClient client, int maxAttempts = 3, JsonAiCallLogStore? callLog = null, string? modelName = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Must be positive.");
        }

        _client = new StructuredJsonAiClient<ShowBibleContent>(client);
        _maxAttempts = maxAttempts;
        _callLog = callLog;
        _modelName = modelName;
    }

    public async Task<ShowBibleGenerationReport> GenerateAsync(
        DirectorPrompt prompt, Guid songId, DisplayGraph layout, EffectVocabularyCatalog vocabulary, EffectRecipeCatalog recipeCatalog,
        int maxOutputTokens, CancellationToken ct, MultiLayerTemplateCatalog? templates = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(recipeCatalog);

        var usages = new List<AiUsage?>();
        IReadOnlyList<ShowBibleValidationError> violations = [];
        var correction = "";

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var userPrompt = correction.Length == 0
                ? prompt.VolatileSuffix
                : $"{prompt.VolatileSuffix}\n\n## Correction needed\nYour previous JSON response failed validation:\n{correction}\nReturn the full corrected JSON object again, fixing every issue above.";

            var request = new ChatCompletionRequest(prompt.StablePrefix, userPrompt, CacheSystemPrompt: true, maxOutputTokens);

            StructuredAiCallResult<ShowBibleContent> result;
            try
            {
                result = await _client.CompleteAsync(request, ct).ConfigureAwait(false);
            }
            catch (AiCallRejectedException ex)
            {
                usages.Add(null);
                violations = [new ShowBibleValidationError("transport", ex.Message)];
                correction = ex.Message;
                await LogAsync(songId, new AiUsage(null, null, null, null), $"attempt {attempt}/{_maxAttempts}: transport rejection -- {ex.Message}", ex.Diagnostics, ct).ConfigureAwait(false);
                continue;
            }

            usages.Add(result.Usage);
            violations = ShowBibleValidator.Validate(result.Value, layout, vocabulary, recipeCatalog, templates);
            await LogAsync(songId, result.Usage, $"attempt {attempt}/{_maxAttempts}: {(violations.Count == 0 ? "validated" : $"{violations.Count} violation(s)")}", result.Diagnostics, ct).ConfigureAwait(false);

            if (violations.Count == 0)
            {
                var bible = new ShowBible(Guid.NewGuid(), songId, result.Value, DateTimeOffset.UtcNow);
                return new ShowBibleGenerationReport(bible, attempt, attempt == 1, [], usages);
            }

            correction = string.Join("\n", violations.Select(v => $"- [{v.Rule}] {v.Message}"));
        }

        return new ShowBibleGenerationReport(null, _maxAttempts, false, violations, usages);
    }

    private async Task LogAsync(Guid songId, AiUsage usage, string note, RequestDiagnostics? diagnostics, CancellationToken ct)
    {
        if (_callLog is null || _modelName is null)
        {
            return;
        }

        var cost = AiCostEstimator.EstimateUsd(_modelName, usage);
        await _callLog.AppendAsync(new AiCallLogEntry(songId, AiRole.Director, _modelName, DateTimeOffset.UtcNow, usage, cost, note, diagnostics), ct).ConfigureAwait(false);
    }
}
