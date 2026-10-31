using System.Net;
using System.Text;
using System.Text.Json;
using XlightsAI.Studio.Director;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Confirmed as a real bug against a real OpenAI-compatible endpoint (reached via the Settings screen's
/// "OpenAI compatible" connection type): a newer reasoning-family model rejects the classic
/// <c>max_tokens</c> request field outright with HTTP 400 ("Unsupported parameter: 'max_tokens' is not
/// supported with this model. Use 'max_completion_tokens' instead"), which previously bubbled straight up
/// as a Show-Bible-generation failure after exhausting all retries -- every retry resent the exact same
/// rejected field. <see cref="OpenAiCompatibleChatCompletionClient"/> now falls back to
/// <c>max_completion_tokens</c> on that exact rejection instead of guessing the right field from the
/// model name up front (older OpenAI models, Azure, and Ollama's OpenAI-compat layer all still expect
/// plain <c>max_tokens</c>, so that stays the first attempt).
/// </summary>
public sealed class OpenAiCompatibleChatCompletionClientTests
{
    private const string MaxTokensRejectionBody = """
        {"error":{"message":"Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.","type":"invalid_request_error","param":"max_tokens","code":"unsupported_parameter"}}
        """;

    private const string SuccessBody = """
        {"choices":[{"message":{"content":"ok"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}
        """;

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

        public RecordingHandler(params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(ct));
            var (status, body) = _responses.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static RoleProviderConfig BuildConfig() => new(
        AiRole.Director, Enabled: true, AiProviderType.OpenAiCompatible, "https://example.invalid", "some-model",
        "cred", RequestTimeoutSeconds: 60, MaxRetries: 2, MaxResponseBytes: 262_144);

    [Fact]
    public async Task CompleteAsync_ProviderRejectsMaxTokens_RetriesOnceWithMaxCompletionTokens()
    {
        var handler = new RecordingHandler(
            (HttpStatusCode.BadRequest, MaxTokensRejectionBody),
            (HttpStatusCode.OK, SuccessBody));
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiCompatibleChatCompletionClient(BuildConfig(), executor, apiKey: "test-key");

        var result = await client.CompleteAsync(
            new ChatCompletionRequest("system", "user", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None);

        Assert.Equal("ok", result.Text);
        Assert.Equal(2, handler.RequestBodies.Count);

        var firstBody = JsonDocument.Parse(handler.RequestBodies[0]).RootElement;
        Assert.True(firstBody.TryGetProperty("max_tokens", out _));
        Assert.False(firstBody.TryGetProperty("max_completion_tokens", out _));

        var secondBody = JsonDocument.Parse(handler.RequestBodies[1]).RootElement;
        Assert.True(secondBody.TryGetProperty("max_completion_tokens", out var value));
        Assert.Equal(4000, value.GetInt32());
        Assert.False(secondBody.TryGetProperty("max_tokens", out _));
    }

    [Fact]
    public async Task CompleteAsync_ProviderAcceptsMaxTokens_NeverRetriesWithMaxCompletionTokens()
    {
        var handler = new RecordingHandler((HttpStatusCode.OK, SuccessBody));
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiCompatibleChatCompletionClient(BuildConfig(), executor, apiKey: "test-key");

        var result = await client.CompleteAsync(
            new ChatCompletionRequest("system", "user", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None);

        Assert.Equal("ok", result.Text);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task CompleteAsync_UnrelatedBadRequest_NeverRetriesWithMaxCompletionTokens()
    {
        const string unrelatedRejection = """{"error":{"message":"Invalid API key.","type":"invalid_request_error"}}""";
        var handler = new RecordingHandler((HttpStatusCode.BadRequest, unrelatedRejection));
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiCompatibleChatCompletionClient(BuildConfig(), executor, apiKey: "test-key");

        await Assert.ThrowsAsync<AiCallRejectedException>(() => client.CompleteAsync(
            new ChatCompletionRequest("system", "user", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None));

        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task CompleteAsync_LogsTheRealRequestUriAndBoundedBody_OnTheReturnedResult()
    {
        var handler = new RecordingHandler((HttpStatusCode.OK, SuccessBody));
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiCompatibleChatCompletionClient(BuildConfig(), executor, apiKey: "test-key");

        var result = await client.CompleteAsync(
            new ChatCompletionRequest("system", "user", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None);

        Assert.NotNull(result.Diagnostics);
        Assert.Equal("https://example.invalid/v1/chat/completions", result.Diagnostics!.RequestUri.ToString());
        Assert.Contains("\"messages\"", result.Diagnostics.RequestBody);
    }

    /// <summary>
    /// Build-plan Phase 11 §2c task 2: this is the exact real bug -- a Responses API endpoint's own
    /// configured path (<c>/openai/responses</c>) silently becomes a different actual request path
    /// (<c>/openai/v1/chat/completions</c>). Before the sanity check, nothing surfaced this divergence
    /// anywhere; now it lands directly on the logged diagnostics.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_ConfiguredEndpointPathDivergesFromActualRequestPath_SurfacesSanityWarning()
    {
        var handler = new RecordingHandler((HttpStatusCode.OK, SuccessBody));
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var misconfigured = BuildConfig() with { Endpoint = "https://my-resource.openai.azure.com/openai/responses?api-version=2025-04-01-preview" };
        var client = new OpenAiCompatibleChatCompletionClient(misconfigured, executor, apiKey: "test-key");

        var result = await client.CompleteAsync(
            new ChatCompletionRequest("system", "user", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None);

        Assert.NotNull(result.Diagnostics!.EndpointSanityWarning);
        Assert.Contains("/openai/responses", result.Diagnostics.EndpointSanityWarning);
        Assert.Contains("/openai/v1/chat/completions", result.Diagnostics.EndpointSanityWarning);
    }

    [Fact]
    public async Task CompleteAsync_BareHostEndpoint_NeverSurfacesASanityWarning()
    {
        var handler = new RecordingHandler((HttpStatusCode.OK, SuccessBody));
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiCompatibleChatCompletionClient(BuildConfig(), executor, apiKey: "test-key");

        var result = await client.CompleteAsync(
            new ChatCompletionRequest("system", "user", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None);

        Assert.Null(result.Diagnostics!.EndpointSanityWarning);
    }
}
