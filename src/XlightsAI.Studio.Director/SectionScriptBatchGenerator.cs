using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>One section this batch is asked to produce a <see cref="SectionScript"/> for.</summary>
/// <param name="SectionId">The section's own id -- the key the returned report dictionary is indexed by.</param>
/// <param name="VolatileSuffix">This section's own volatile prompt suffix (<see cref="SectionScriptPromptBuilder.BuildVolatileSuffix"/>), already built by the caller.</param>
public sealed record SectionScriptBatchItem(Guid SectionId, string VolatileSuffix);

/// <summary>
/// Build-plan Phase 6 task 2: "issue the ~30 per-section calls via the provider's Batch API where
/// available, sharing the cached Show Bible + vocabulary + craft-rules prefix." Every section's first
/// attempt is submitted together, sharing one <paramref name="stablePrefix"/>, through
/// <see cref="IBatchChatCompletionClient"/> when the configured client implements it (a real Anthropic
/// batch job); when it does not (local Ollama, plain OpenAI-compatible), the same first-round work runs as
/// bounded-concurrency individual calls instead -- "where available" literally, not a feature flag. Any
/// section whose first-round response fails to validate (batch or fallback path alike) is handed to
/// <see cref="SectionScriptGenerator"/>'s own normal per-section retry loop -- a validation failure is rare
/// enough that re-running it as a plain call, rather than engineering a second batch round-trip just for
/// stragglers, is the simpler and still-correct choice (a disclosed simplification, not a hidden one).
/// </summary>
public sealed class SectionScriptBatchGenerator
{
    private readonly IChatCompletionClient _client;
    private readonly int _maxAttempts;
    private readonly int _maxConcurrency;
    private readonly JsonAiCallLogStore? _callLog;
    private readonly string? _modelName;

    public SectionScriptBatchGenerator(
        IChatCompletionClient client, int maxAttempts = 3, int maxConcurrency = 6, JsonAiCallLogStore? callLog = null, string? modelName = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Must be positive.");
        }

        if (maxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be positive.");
        }

        _client = client;
        _maxAttempts = maxAttempts;
        _maxConcurrency = maxConcurrency;
        _callLog = callLog;
        _modelName = modelName;
    }

    public async Task<IReadOnlyDictionary<Guid, SectionScriptGenerationReport>> GenerateAllAsync(
        string stablePrefix, IReadOnlyList<SectionScriptBatchItem> items, Guid songId, LookLibrary lookLibrary, DisplayGraph layout,
        int maxOutputTokens, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stablePrefix);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(lookLibrary);
        ArgumentNullException.ThrowIfNull(layout);

        if (items.Count == 0)
        {
            return new Dictionary<Guid, SectionScriptGenerationReport>();
        }

        var results = new Dictionary<Guid, SectionScriptGenerationReport>();
        var needsIndividualRetry = new List<SectionScriptBatchItem>();

        if (_client is IBatchChatCompletionClient batchClient)
        {
            var requests = items.Select(i => new ChatCompletionRequest(stablePrefix, i.VolatileSuffix, CacheSystemPrompt: true, maxOutputTokens)).ToList();

            IReadOnlyList<BatchItemResult> batchResults;
            try
            {
                batchResults = await batchClient.CompleteBatchAsync(requests, ct).ConfigureAwait(false);
            }
            catch (AiCallRejectedException)
            {
                // The whole batch job itself failed to submit/complete (not a per-item failure) -- every
                // section falls through to the individual-call fallback below rather than losing the run.
                needsIndividualRetry.AddRange(items);
                batchResults = [];
            }

            for (var i = 0; i < batchResults.Count; i++)
            {
                var item = items[i];
                var batchResult = batchResults[i];

                if (batchResult.Result is null)
                {
                    await LogAsync(songId, new AiUsage(null, null, null, null), $"section {item.SectionId}: batch item failed -- {batchResult.Error!.Message}", batchResult.Error!.Diagnostics, ct).ConfigureAwait(false);
                    needsIndividualRetry.Add(item);
                    continue;
                }

                string json;
                SectionScript? script = null;
                IReadOnlyList<SectionScriptValidationError> violations;
                try
                {
                    json = StructuredJsonExtractor.ExtractJson(batchResult.Result.Text);
                    script = System.Text.Json.JsonSerializer.Deserialize<SectionScript>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
                        PropertyNameCaseInsensitive = true,
                    });
                    violations = script is null
                        ? [new SectionScriptValidationError("transport", "Batch item's JSON deserialized to null.")]
                        : SectionScriptValidator.Validate(script, lookLibrary, layout);
                }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or AiCallRejectedException)
                {
                    violations = [new SectionScriptValidationError("transport", ex.Message)];
                }

                await LogAsync(songId, batchResult.Result.Usage, $"section {item.SectionId}: batch attempt 1/{_maxAttempts}: {(violations.Count == 0 ? "validated" : $"{violations.Count} violation(s)")}", batchResult.Result.Diagnostics, ct).ConfigureAwait(false);

                if (violations.Count == 0 && script is not null)
                {
                    results[item.SectionId] = new SectionScriptGenerationReport(script, 1, true, [], [batchResult.Result.Usage]);
                }
                else
                {
                    needsIndividualRetry.Add(item);
                }
            }
        }
        else
        {
            needsIndividualRetry.AddRange(items);
        }

        if (needsIndividualRetry.Count > 0)
        {
            using var semaphore = new SemaphoreSlim(_maxConcurrency);
            var generator = new SectionScriptGenerator(_client, _maxAttempts, _callLog, _modelName);

            var tasks = needsIndividualRetry.Select(async item =>
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var report = await generator.GenerateAsync(stablePrefix, item.VolatileSuffix, songId, lookLibrary, layout, maxOutputTokens, ct).ConfigureAwait(false);
                    return (item.SectionId, report);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var completed = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var (sectionId, report) in completed)
            {
                results[sectionId] = report;
            }
        }

        return results;
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
