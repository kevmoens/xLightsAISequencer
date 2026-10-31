using System.Text.Json;
using System.Text.Json.Nodes;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Real OpenAI-compatible chat-completions adapter (<c>POST {endpoint}/v1/chat/completions</c>) -- covers
/// OpenAI, Azure AI Foundry (reached through its own OpenAI-compatible endpoint, confirmed as the
/// intended access path), and local Ollama (verified directly against a real running local install on
/// this machine: <c>http://127.0.0.1:11434/v1/chat/completions</c> with a real model, including its own
/// real <c>usage.prompt_tokens_details.cached_tokens</c> field -- not guessed from documentation).
/// <see cref="ChatCompletionRequest.CacheSystemPrompt"/> is a no-op on the request itself for this
/// provider family (OpenAI/Azure cache automatically server-side for a byte-identical repeated prefix
/// above their own internal length threshold, with no client-side opt-in marker to set); this client
/// still reads back whatever <c>cached_tokens</c> the provider reports.
/// </summary>
public sealed class OpenAiCompatibleChatCompletionClient : IChatCompletionClient, IVisionCapableChatCompletionClient
{
    private readonly RoleProviderConfig _config;
    private readonly string? _apiKey;
    private readonly HttpJsonRequestExecutor _executor;

    public OpenAiCompatibleChatCompletionClient(RoleProviderConfig config, HttpJsonRequestExecutor executor, string? apiKey)
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

        JsonObject BuildBody(string maxTokensFieldName) => new()
        {
            ["model"] = _config.ModelName,
            [maxTokensFieldName] = request.MaxOutputTokens,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt }),
        };

        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_apiKey))
        {
            headers["Authorization"] = $"Bearer {_apiKey}";
        }

        var uri = ProviderEndpointUri.Build(_config.Endpoint, "v1/chat/completions");
        var (responseText, sentBody) = await PostWithMaxTokensFallbackAsync(uri, BuildBody, headers, ct).ConfigureAwait(false);

        return Parse(responseText) with { Diagnostics = BuildDiagnostics(uri, sentBody) };
    }

    private RequestDiagnostics BuildDiagnostics(Uri uri, string bodyJson) =>
        new(uri, RequestLogTruncation.Truncate(bodyJson, _config.MaxResponseBytes), EndpointSanityChecker.CheckDivergence(_config.Endpoint, uri));

    /// <summary>
    /// Confirmed against a real OpenAI-compatible endpoint: newer reasoning-family models (observed on a
    /// model reached through an OpenAI-compatible connection) reject the classic <c>max_tokens</c> field
    /// outright ("Unsupported parameter: 'max_tokens' is not supported with this model. Use
    /// 'max_completion_tokens' instead") while older OpenAI models, Azure, and Ollama's OpenAI-compat
    /// layer all expect/accept <c>max_tokens</c> -- there is no reliable way to tell which a given
    /// <see cref="RoleProviderConfig.ModelName"/> wants without calling it, so this sends <c>max_tokens</c>
    /// first (the common case) and falls back to <c>max_completion_tokens</c> only on that exact rejection,
    /// rather than guessing from the model name.
    /// </summary>
    private async Task<(string ResponseText, string SentBody)> PostWithMaxTokensFallbackAsync(
        Uri uri, Func<string, JsonObject> buildBody, Dictionary<string, string> headers, CancellationToken ct)
    {
        var firstBody = buildBody("max_tokens").ToJsonString();
        try
        {
            var responseText = await _executor.PostAsync(
                uri, firstBody, headers,
                TimeSpan.FromSeconds(_config.RequestTimeoutSeconds), _config.MaxRetries, _config.MaxResponseBytes, ct).ConfigureAwait(false);
            return (responseText, firstBody);
        }
        catch (AiCallRejectedException ex) when (
            ex.Reason == AiCallRejectionReason.ProviderUnavailable &&
            ex.Message.Contains("max_tokens", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("max_completion_tokens", StringComparison.OrdinalIgnoreCase))
        {
            var secondBody = buildBody("max_completion_tokens").ToJsonString();
            try
            {
                var responseText = await _executor.PostAsync(
                    uri, secondBody, headers,
                    TimeSpan.FromSeconds(_config.RequestTimeoutSeconds), _config.MaxRetries, _config.MaxResponseBytes, ct).ConfigureAwait(false);
                return (responseText, secondBody);
            }
            catch (AiCallRejectedException retryEx)
            {
                throw retryEx.WithDiagnostics(BuildDiagnostics(uri, secondBody));
            }
        }
        catch (AiCallRejectedException ex)
        {
            throw ex.WithDiagnostics(BuildDiagnostics(uri, firstBody));
        }
    }

    /// <summary>
    /// Real OpenAI-compatible vision content: a <c>content</c> array mixing <c>{"type":"text","text":...}</c>
    /// with <c>{"type":"image_url","image_url":{"url":"data:image/png;base64,..."}}</c> entries -- the
    /// standard OpenAI chat-completions vision shape, which Ollama's own OpenAI-compatibility layer
    /// (<c>/v1/chat/completions</c>, confirmed against Ollama's published OpenAI-compatibility docs)
    /// accepts identically for a vision-capable local model tag (e.g. <c>llava</c>) -- no provider-specific
    /// branching needed beyond what <see cref="CompleteAsync(ChatCompletionRequest,CancellationToken)"/>
    /// already does for OpenAI/Azure/Ollama uniformly.
    /// </summary>
    public async Task<ChatCompletionResult> CompleteAsync(MultimodalChatCompletionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userContent = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = request.UserPrompt } };
        foreach (var png in request.ImagesPng)
        {
            userContent.Add(new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject { ["url"] = $"data:image/png;base64,{Convert.ToBase64String(png)}" },
            });
        }

        JsonObject BuildBody(string maxTokensFieldName) => new()
        {
            ["model"] = _config.ModelName,
            [maxTokensFieldName] = request.MaxOutputTokens,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userContent }),
        };

        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_apiKey))
        {
            headers["Authorization"] = $"Bearer {_apiKey}";
        }

        var uri = ProviderEndpointUri.Build(_config.Endpoint, "v1/chat/completions");
        var (responseText, sentBody) = await PostWithMaxTokensFallbackAsync(uri, BuildBody, headers, ct).ConfigureAwait(false);

        return Parse(responseText) with { Diagnostics = BuildDiagnostics(uri, sentBody) };
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

        var text = obj["choices"]?.AsArray().FirstOrDefault()?["message"]?["content"]?.GetValue<string>();
        if (string.IsNullOrEmpty(text))
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, "Provider response carried no choices[0].message.content.");
        }

        var usageNode = obj["usage"] as JsonObject;
        var detailsNode = usageNode?["prompt_tokens_details"] as JsonObject;

        var usage = new AiUsage(
            InputTokens: GetInt(usageNode, "prompt_tokens"),
            OutputTokens: GetInt(usageNode, "completion_tokens"),
            CacheReadInputTokens: GetInt(detailsNode, "cached_tokens"),
            CacheCreationInputTokens: null); // OpenAI-compatible providers have no client-marked cache-write concept.

        return new ChatCompletionResult(text, usage);
    }

    private static int? GetInt(JsonObject? obj, string key) =>
        obj is not null && obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue(out int result)
            ? result
            : null;
}
