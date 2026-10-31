using System.Text.Json;
using System.Text.Json.Nodes;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 11 §2a: the real Azure OpenAI/OpenAI **Responses API** adapter
/// (<c>POST {ProviderEndpointUri.BuildResponses(config.Endpoint)}</c>, used verbatim including its own
/// <c>api-version</c> query string) -- speaks that API's own real wire shape (<c>input</c>, not
/// <c>messages</c>; <c>output[].content[].text</c>, not <c>choices[].message.content</c>) rather than the
/// classic chat-completions shape <see cref="OpenAiCompatibleChatCompletionClient"/> always sent, which is
/// this phase's motivating bug: a configured Responses API endpoint silently became a chat-completions
/// call to a different, unintended endpoint, with no way to set the Responses API's own reasoning-effort
/// control at all. Selected automatically by <see cref="ChatCompletionClientFactory"/> when
/// <see cref="RoleProviderConfig.Endpoint"/> names this shape (<see cref="ProviderEndpointUri.IsResponsesApiEndpoint"/>)
/// -- no new user-facing setting required to opt in.
/// </summary>
public sealed class OpenAiResponsesChatCompletionClient : IChatCompletionClient, IVisionCapableChatCompletionClient
{
    /// <summary>
    /// Build-plan Phase 11 §2b: the conservative default forwarded when <see cref="RoleProviderConfig.ReasoningEffort"/>
    /// is unset, so an unconfigured role never silently runs with the model's own unbounded reasoning budget --
    /// exactly what this phase's incident traced as a likely cause of a ~57-minute single call.
    /// </summary>
    internal const string DefaultReasoningEffort = "low";

    private readonly RoleProviderConfig _config;
    private readonly string? _apiKey;
    private readonly HttpJsonRequestExecutor _executor;

    public OpenAiResponsesChatCompletionClient(RoleProviderConfig config, HttpJsonRequestExecutor executor, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(executor);
        if (config.ProviderType != AiProviderType.OpenAiCompatible)
        {
            throw new ArgumentException($"Config provider type must be {nameof(AiProviderType.OpenAiCompatible)}.", nameof(config));
        }

        _config = config;
        _executor = executor;
        _apiKey = apiKey;
    }

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new JsonObject
        {
            ["model"] = _config.ModelName,
            ["max_output_tokens"] = request.MaxOutputTokens,
            ["reasoning"] = new JsonObject { ["effort"] = _config.ReasoningEffort ?? DefaultReasoningEffort },
            ["input"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt }),
        };

        return await PostAsync(body, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Real Responses API vision content: an <c>input</c> message's <c>content</c> as an array of
    /// <c>{"type":"input_text","text":...}</c>/<c>{"type":"input_image","image_url":"data:image/png;base64,..."}</c>
    /// parts -- that API's own documented multimodal input shape, distinct from both Anthropic's
    /// <c>image</c>/<c>source</c> blocks and the classic chat-completions <c>image_url</c> object wrapper.
    /// </summary>
    public async Task<ChatCompletionResult> CompleteAsync(MultimodalChatCompletionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userContent = new JsonArray { new JsonObject { ["type"] = "input_text", ["text"] = request.UserPrompt } };
        foreach (var png in request.ImagesPng)
        {
            userContent.Add(new JsonObject
            {
                ["type"] = "input_image",
                ["image_url"] = $"data:image/png;base64,{Convert.ToBase64String(png)}",
            });
        }

        var body = new JsonObject
        {
            ["model"] = _config.ModelName,
            ["max_output_tokens"] = request.MaxOutputTokens,
            ["reasoning"] = new JsonObject { ["effort"] = _config.ReasoningEffort ?? DefaultReasoningEffort },
            ["input"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userContent }),
        };

        return await PostAsync(body, ct).ConfigureAwait(false);
    }

    private async Task<ChatCompletionResult> PostAsync(JsonObject body, CancellationToken ct)
    {
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_apiKey))
        {
            headers["Authorization"] = $"Bearer {_apiKey}";
        }

        var uri = ProviderEndpointUri.BuildResponses(_config.Endpoint);
        var bodyJson = body.ToJsonString();
        var diagnostics = new RequestDiagnostics(
            uri, RequestLogTruncation.Truncate(bodyJson, _config.MaxResponseBytes),
            EndpointSanityChecker.CheckDivergence(_config.Endpoint, uri));

        string responseText;
        try
        {
            responseText = await _executor.PostAsync(
                uri, bodyJson, headers,
                TimeSpan.FromSeconds(_config.RequestTimeoutSeconds), _config.MaxRetries, _config.MaxResponseBytes, ct).ConfigureAwait(false);
        }
        catch (AiCallRejectedException ex)
        {
            throw ex.WithDiagnostics(diagnostics);
        }

        return Parse(responseText) with { Diagnostics = diagnostics };
    }

    private static ChatCompletionResult Parse(string responseText)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(responseText);
        }
        catch (JsonException ex)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Provider response was not valid JSON.", ex);
        }

        if (root is not JsonObject obj)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Provider response was not a JSON object.");
        }

        var text = ExtractOutputText(obj);
        if (string.IsNullOrEmpty(text))
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Provider response carried no output[].content[].text (and no top-level output_text).");
        }

        var usageNode = obj["usage"] as JsonObject;
        var inputDetails = usageNode?["input_tokens_details"] as JsonObject;

        var usage = new AiUsage(
            InputTokens: GetInt(usageNode, "input_tokens"),
            OutputTokens: GetInt(usageNode, "output_tokens"),
            CacheReadInputTokens: GetInt(inputDetails, "cached_tokens"),
            CacheCreationInputTokens: null); // Responses API has no client-marked cache-write concept, same as the classic chat-completions shape.

        return new ChatCompletionResult(text, usage);
    }

    /// <summary>
    /// Walks the real documented Responses API output shape: <c>output</c> is an array of items, the one
    /// with <c>"type":"message"</c> carries a <c>content</c> array whose <c>"type":"output_text"</c> entry
    /// holds the model's own text. Also accepts a top-level <c>output_text</c> string directly, in case a
    /// provider/SDK variant includes that convenience field in the raw response body.
    /// </summary>
    private static string? ExtractOutputText(JsonObject obj)
    {
        if (obj["output_text"] is JsonValue directValue && directValue.TryGetValue(out string? direct))
        {
            return direct;
        }

        var messageItem = obj["output"]?.AsArray().FirstOrDefault(n => n?["type"]?.GetValue<string>() == "message");
        return messageItem?["content"]?.AsArray().FirstOrDefault(n => n?["type"]?.GetValue<string>() == "output_text")?["text"]?.GetValue<string>();
    }

    private static int? GetInt(JsonObject? obj, string key) =>
        obj is not null && obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue(out int result)
            ? result
            : null;
}
