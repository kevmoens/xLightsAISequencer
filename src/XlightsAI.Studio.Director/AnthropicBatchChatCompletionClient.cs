using System.Text.Json;
using System.Text.Json.Nodes;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 6 task 2: the real Anthropic Message Batches API
/// (<c>POST /v1/messages/batches</c>, poll <c>GET /v1/messages/batches/{id}</c> until
/// <c>processing_status == "ended"</c>, then fetch <c>results_url</c>'s own JSONL body) -- "~50% off input
/// and output, and it stacks with prompt caching," exactly the mechanism architecture §14 names for the
/// ~30 per-section Choreographer calls. One request per element of <paramref name="requests"/>, addressed
/// by its own index-derived <c>custom_id</c> so results (which the API may return out of submission order)
/// are matched back correctly regardless of provider-side ordering.
/// </summary>
public sealed class AnthropicBatchChatCompletionClient : IChatCompletionClient, IBatchChatCompletionClient
{
    private const string ApiVersion = "2023-06-01";

    /// <summary>How often to re-poll the batch's own status while it is still processing -- the API itself documents batches as typically completing within 24h, so a short, bounded poll interval is appropriate here (never a tight spin-loop).</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>A hard ceiling on how long this client will keep polling one batch before giving up -- never block the caller forever on a stalled/misbehaving batch job.</summary>
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(30);

    private readonly RoleProviderConfig _config;
    private readonly string? _apiKey;
    private readonly HttpJsonRequestExecutor _executor;
    private readonly AnthropicChatCompletionClient _singleCallDelegate;

    public AnthropicBatchChatCompletionClient(RoleProviderConfig config, HttpJsonRequestExecutor executor, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(executor);
        if (config.ProviderType != AiProviderType.Anthropic)
        {
            throw new ArgumentException($"Config provider type must be {nameof(AiProviderType.Anthropic)}.", nameof(config));
        }

        _config = config;
        _executor = executor;
        _apiKey = apiKey;
        _singleCallDelegate = new AnthropicChatCompletionClient(config, executor, apiKey);
    }

    /// <summary>Single-call passthrough -- lets this type stand in anywhere a plain <see cref="IChatCompletionClient"/> is expected (e.g. a Show Bible's own one-call Director role), with batching available only where a caller actually submits a batch via <see cref="CompleteBatchAsync"/>.</summary>
    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct) =>
        _singleCallDelegate.CompleteAsync(request, ct);

    public async Task<IReadOnlyList<BatchItemResult>> CompleteBatchAsync(IReadOnlyList<ChatCompletionRequest> requests, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            return [];
        }

        var headers = BuildHeaders();
        var baseUri = ProviderEndpointUri.BuildBase(_config.Endpoint);
        var timeout = TimeSpan.FromSeconds(_config.RequestTimeoutSeconds);

        var submitUri = new Uri(baseUri, "v1/messages/batches");
        var submitBody = BuildSubmitBody(requests);
        var submitResponseText = await _executor.PostAsync(
            submitUri, submitBody, headers, timeout, _config.MaxRetries, _config.MaxResponseBytes, ct).ConfigureAwait(false);

        var batchId = ParseField(submitResponseText, "id")
            ?? throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Anthropic batch submission response carried no 'id'.");

        var deadline = DateTimeOffset.UtcNow + MaxWait;
        string? resultsUrl = null;
        while (true)
        {
            var statusResponseText = await _executor.GetAsync(
                new Uri(baseUri, $"v1/messages/batches/{batchId}"), headers, timeout, _config.MaxRetries, _config.MaxResponseBytes, ct).ConfigureAwait(false);

            var status = ParseField(statusResponseText, "processing_status");
            if (status == "ended")
            {
                resultsUrl = ParseField(statusResponseText, "results_url");
                break;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new AiCallRejectedException(AiCallRejectionReason.ProviderUnavailable, $"Anthropic batch '{batchId}' did not reach 'ended' within {MaxWait}.");
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }

        if (resultsUrl is null)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, $"Anthropic batch '{batchId}' ended with no 'results_url'.");
        }

        var resultsJsonl = await _executor.GetAsync(new Uri(resultsUrl), headers, timeout, _config.MaxRetries, _config.MaxResponseBytes, ct).ConfigureAwait(false);
        return ParseResults(resultsJsonl, requests.Count, submitUri);
    }

    private Dictionary<string, string> BuildHeaders()
    {
        var headers = new Dictionary<string, string> { ["anthropic-version"] = ApiVersion };
        if (!string.IsNullOrEmpty(_apiKey))
        {
            headers["x-api-key"] = _apiKey;
        }

        return headers;
    }

    private string BuildSubmitBody(IReadOnlyList<ChatCompletionRequest> requests)
    {
        var batchRequests = new JsonArray();
        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var systemBlock = new JsonObject { ["type"] = "text", ["text"] = request.SystemPrompt };
            if (request.CacheSystemPrompt)
            {
                systemBlock["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
            }

            var @params = new JsonObject
            {
                ["model"] = _config.ModelName,
                ["max_tokens"] = request.MaxOutputTokens,
                ["system"] = new JsonArray(systemBlock),
                ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt }),
            };

            batchRequests.Add(new JsonObject { ["custom_id"] = CustomId(i), ["params"] = @params });
        }

        return new JsonObject { ["requests"] = batchRequests }.ToJsonString();
    }

    private static string CustomId(int index) => $"section-{index:D5}";

    private static int IndexFromCustomId(string customId) => int.Parse(customId.AsSpan("section-".Length));

    private static string? ParseField(string json, string field)
    {
        var node = JsonNode.Parse(json);
        return node?[field]?.GetValue<string>();
    }

    /// <summary><paramref name="submitUri"/> is the only real request URI there is to report for a batch item -- the individual item's own body was never sent on its own, only as part of the shared batch submission, so <see cref="RequestDiagnostics.RequestBody"/> is left <see langword="null"/> here (a disclosed simplification, not a hidden one).</summary>
    private static List<BatchItemResult> ParseResults(string jsonl, int requestCount, Uri submitUri)
    {
        var results = new BatchItemResult?[requestCount];

        foreach (var line in jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            JsonNode? entry;
            try
            {
                entry = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                continue; // One malformed result line never discards the rest of the batch.
            }

            var customId = entry?["custom_id"]?.GetValue<string>();
            if (customId is null)
            {
                continue;
            }

            var index = IndexFromCustomId(customId);
            if (index < 0 || index >= requestCount)
            {
                continue;
            }

            var result = entry?["result"];
            var resultType = result?["type"]?.GetValue<string>();

            if (resultType == "succeeded")
            {
                var message = result?["message"];
                var text = message?["content"]?.AsArray().FirstOrDefault(n => n?["type"]?.GetValue<string>() == "text")?["text"]?.GetValue<string>();
                if (string.IsNullOrEmpty(text))
                {
                    results[index] = BatchItemResult.Failure(new AiCallRejectedException(AiCallRejectionReason.MalformedJson, $"Batch item '{customId}' succeeded with no text content block."));
                    continue;
                }

                var usageNode = message?["usage"] as JsonObject;
                var usage = new AiUsage(
                    InputTokens: GetInt(usageNode, "input_tokens"),
                    OutputTokens: GetInt(usageNode, "output_tokens"),
                    CacheReadInputTokens: GetInt(usageNode, "cache_read_input_tokens"),
                    CacheCreationInputTokens: GetInt(usageNode, "cache_creation_input_tokens"));

                results[index] = BatchItemResult.Success(new ChatCompletionResult(text, usage, new RequestDiagnostics(submitUri, RequestBody: null, EndpointSanityWarning: null)));
            }
            else
            {
                var errorMessage = result?["error"]?["message"]?.GetValue<string>() ?? $"Batch item '{customId}' ended with status '{resultType}'.";
                results[index] = BatchItemResult.Failure(new AiCallRejectedException(AiCallRejectionReason.ProviderUnavailable, errorMessage));
            }
        }

        for (var i = 0; i < requestCount; i++)
        {
            results[i] ??= BatchItemResult.Failure(new AiCallRejectedException(AiCallRejectionReason.MalformedJson, $"Batch results carried no entry for 'section-{i:D5}'."));
        }

        return results.Select(r => r!).ToList();
    }

    private static int? GetInt(JsonObject? obj, string key) =>
        obj is not null && obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue(out int result)
            ? result
            : null;
}
