using System.Text.Json;
using System.Text.Json.Nodes;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Real Anthropic Messages API adapter (<c>POST {endpoint}/v1/messages</c>), with real prompt caching:
/// when <see cref="ChatCompletionRequest.CacheSystemPrompt"/> is set, the system block carries
/// <c>cache_control: {"type":"ephemeral"}</c> (Anthropic's own documented mechanism for marking a prefix
/// cacheable) -- architecture §14's "put stable content first... verify with cache_read_input_tokens"
/// requires a provider that actually supports this, which Anthropic does natively.
/// </summary>
public sealed class AnthropicChatCompletionClient : IChatCompletionClient, IVisionCapableChatCompletionClient
{
    private const string ApiVersion = "2023-06-01";

    private readonly RoleProviderConfig _config;
    private readonly string? _apiKey;
    private readonly HttpJsonRequestExecutor _executor;

    public AnthropicChatCompletionClient(RoleProviderConfig config, HttpJsonRequestExecutor executor, string? apiKey)
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
    }

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var systemBlock = new JsonObject { ["type"] = "text", ["text"] = request.SystemPrompt };
        if (request.CacheSystemPrompt)
        {
            systemBlock["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
        }

        var body = new JsonObject
        {
            ["model"] = _config.ModelName,
            ["max_tokens"] = request.MaxOutputTokens,
            ["system"] = new JsonArray(systemBlock),
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt }),
        };

        var headers = new Dictionary<string, string> { ["anthropic-version"] = ApiVersion };
        if (!string.IsNullOrEmpty(_apiKey))
        {
            headers["x-api-key"] = _apiKey;
        }

        var uri = ProviderEndpointUri.Build(_config.Endpoint, "v1/messages");
        var bodyJson = body.ToJsonString();
        var diagnostics = BuildDiagnostics(uri, bodyJson);

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

    /// <summary>
    /// Real Anthropic multimodal content blocks: <c>{"type":"image","source":{"type":"base64","media_type":"image/png","data":...}}</c>
    /// alongside a trailing <c>{"type":"text","text":...}</c> block in the same user message (Anthropic
    /// Messages API's own documented multi-content-block message shape) -- images precede the text block
    /// per Anthropic's own documented best practice ("images placed before text... produces the best
    /// results").
    /// </summary>
    public async Task<ChatCompletionResult> CompleteAsync(MultimodalChatCompletionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var systemBlock = new JsonObject { ["type"] = "text", ["text"] = request.SystemPrompt };
        if (request.CacheSystemPrompt)
        {
            systemBlock["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
        }

        var content = new JsonArray();
        foreach (var png in request.ImagesPng)
        {
            content.Add(new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = "image/png",
                    ["data"] = Convert.ToBase64String(png),
                },
            });
        }

        content.Add(new JsonObject { ["type"] = "text", ["text"] = request.UserPrompt });

        var body = new JsonObject
        {
            ["model"] = _config.ModelName,
            ["max_tokens"] = request.MaxOutputTokens,
            ["system"] = new JsonArray(systemBlock),
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = content }),
        };

        var headers = new Dictionary<string, string> { ["anthropic-version"] = ApiVersion };
        if (!string.IsNullOrEmpty(_apiKey))
        {
            headers["x-api-key"] = _apiKey;
        }

        var uri = ProviderEndpointUri.Build(_config.Endpoint, "v1/messages");
        var bodyJson = body.ToJsonString();
        var diagnostics = BuildDiagnostics(uri, bodyJson);

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

    private RequestDiagnostics BuildDiagnostics(Uri uri, string bodyJson) =>
        new(uri, RequestLogTruncation.Truncate(bodyJson, _config.MaxResponseBytes), EndpointSanityChecker.CheckDivergence(_config.Endpoint, uri));

    private static ChatCompletionResult Parse(string responseText)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(responseText);
        }
        catch (JsonException ex)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Anthropic response was not valid JSON.", ex);
        }

        if (root is not JsonObject obj)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Anthropic response was not a JSON object.");
        }

        var text = obj["content"]?.AsArray().FirstOrDefault(n => n?["type"]?.GetValue<string>() == "text")?["text"]?.GetValue<string>();
        if (string.IsNullOrEmpty(text))
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Anthropic response carried no text content block.");
        }

        var usageNode = obj["usage"] as JsonObject;
        var usage = new AiUsage(
            InputTokens: GetInt(usageNode, "input_tokens"),
            OutputTokens: GetInt(usageNode, "output_tokens"),
            CacheReadInputTokens: GetInt(usageNode, "cache_read_input_tokens"),
            CacheCreationInputTokens: GetInt(usageNode, "cache_creation_input_tokens"));

        return new ChatCompletionResult(text, usage);
    }

    private static int? GetInt(JsonObject? obj, string key) =>
        obj is not null && obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue(out int result)
            ? result
            : null;
}
