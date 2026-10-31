using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using XlightsAI.Studio.AiEdits;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 11 §2a/§2b: proves the fix for the real endpoint-construction bug confirmed in
/// <c>docs/AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md</c> -- a configured Azure OpenAI Responses API
/// endpoint must be called verbatim (path + <c>api-version</c> query string intact), using that API's own
/// <c>input</c>/<c>output</c> wire shape and its own <c>reasoning.effort</c> control, not the classic
/// chat-completions shape <see cref="OpenAiCompatibleChatCompletionClient"/> always sent.
/// </summary>
public sealed class OpenAiResponsesChatCompletionClientTests
{
    private const string RealIncidentEndpoint = "https://my-resource.openai.azure.com/openai/responses?api-version=2025-04-01-preview";

    private const string SuccessBody = """
        {"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"ok"}]}],"usage":{"input_tokens":123,"output_tokens":45,"input_tokens_details":{"cached_tokens":10}}}
        """;

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static RoleProviderConfig Config(string? reasoningEffort = null) => new(
        AiRole.Director, Enabled: true, AiProviderType.OpenAiCompatible, RealIncidentEndpoint, "gpt-5",
        CredentialName: "cred", RequestTimeoutSeconds: 60, MaxRetries: 1, MaxResponseBytes: 262_144, ReasoningEffort: reasoningEffort);

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public Task StoreAsync(string name, string secret, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> TryGetAsync(string name, CancellationToken ct = default) => Task.FromResult<string?>("test-key");
        public Task DeleteAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ChatCompletionClientFactory_RealIncidentEndpoint_SelectsResponsesClient_NotTheClassicOne()
    {
        // The real root cause this phase fixes: before this, every OpenAiCompatible config -- including
        // this one -- resolved to OpenAiCompatibleChatCompletionClient regardless of endpoint shape.
        using var httpClient = new HttpClient(new RecordingHandler(HttpStatusCode.OK, SuccessBody));
        var factory = new ChatCompletionClientFactory(new FakeCredentialStore(), httpClient);

        var client = await factory.TryCreateAsync(Config(), CancellationToken.None);

        Assert.IsType<OpenAiResponsesChatCompletionClient>(client);
    }

    [Fact]
    public async Task CompleteAsync_RealIncidentEndpoint_SendsToTheConfiguredPathAndApiVersion_NotADifferentEndpoint()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiResponsesChatCompletionClient(Config(), executor, apiKey: "test-key");

        await client.CompleteAsync(new ChatCompletionRequest("SYSTEM", "USER", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None);

        // The actual bug: the old client would have sent this to ".../openai/v1/chat/completions" instead,
        // silently dropping both the real path and the api-version query string.
        Assert.Equal(RealIncidentEndpoint, handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteAsync_SendsResponsesApiWireShape_InputNotMessages_WithConservativeDefaultReasoningEffort()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiResponsesChatCompletionClient(Config(), executor, apiKey: "test-key");

        await client.CompleteAsync(new ChatCompletionRequest("SYSTEM", "USER", CacheSystemPrompt: false, MaxOutputTokens: 4000), CancellationToken.None);

        var sentBody = JsonNode.Parse(handler.LastRequestBody!)!;
        Assert.Equal("gpt-5", sentBody["model"]!.GetValue<string>());
        Assert.Equal(4000, sentBody["max_output_tokens"]!.GetValue<int>());
        Assert.Null(sentBody["messages"]); // never the classic chat-completions field.
        Assert.Equal("SYSTEM", sentBody["input"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("USER", sentBody["input"]![1]!["content"]!.GetValue<string>());

        // §2b: unset RoleProviderConfig.ReasoningEffort must not mean "unbounded" -- a conservative default
        // is still sent, which is the actual fix for the ~57-minute single-call latency this phase traced.
        Assert.Equal("low", sentBody["reasoning"]!["effort"]!.GetValue<string>());
    }

    [Fact]
    public async Task CompleteAsync_ConfiguredReasoningEffort_IsForwardedInsteadOfTheDefault()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiResponsesChatCompletionClient(Config(reasoningEffort: "high"), executor, apiKey: "test-key");

        await client.CompleteAsync(new ChatCompletionRequest("S", "U", false, 10), CancellationToken.None);

        var sentBody = JsonNode.Parse(handler.LastRequestBody!)!;
        Assert.Equal("high", sentBody["reasoning"]!["effort"]!.GetValue<string>());
    }

    [Fact]
    public async Task CompleteAsync_ParsesRealResponsesApiOutputShape_NotChoicesMessageContent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiResponsesChatCompletionClient(Config(), executor, apiKey: "test-key");

        var result = await client.CompleteAsync(new ChatCompletionRequest("S", "U", false, 10), CancellationToken.None);

        Assert.Equal("ok", result.Text);
        Assert.Equal(123, result.Usage.InputTokens);
        Assert.Equal(45, result.Usage.OutputTokens);
        Assert.Equal(10, result.Usage.CacheReadInputTokens);
    }

    [Fact]
    public async Task CompleteAsync_LogsTheRealRequestUriAndBoundedBody_OnTheReturnedResult()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessBody);
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiResponsesChatCompletionClient(Config(), executor, apiKey: "test-key");

        var result = await client.CompleteAsync(new ChatCompletionRequest("S", "U", false, 10), CancellationToken.None);

        Assert.NotNull(result.Diagnostics);
        Assert.Equal(RealIncidentEndpoint, result.Diagnostics!.RequestUri.ToString());
        Assert.Contains("\"input\"", result.Diagnostics.RequestBody);
        // The configured endpoint's own path and the actual request path match exactly for this client --
        // no sanity-check warning should fire for the fixed path.
        Assert.Null(result.Diagnostics.EndpointSanityWarning);
    }

    [Fact]
    public async Task CompleteAsync_RejectedCall_StillCarriesRequestDiagnostics()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, "denied");
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiResponsesChatCompletionClient(Config(), executor, apiKey: "bad-key");

        var ex = await Assert.ThrowsAsync<AiCallRejectedException>(
            () => client.CompleteAsync(new ChatCompletionRequest("S", "U", false, 10), CancellationToken.None));

        Assert.Equal(AiCallRejectionReason.Unauthorized, ex.Reason);
        Assert.NotNull(ex.Diagnostics);
        Assert.Equal(RealIncidentEndpoint, ex.Diagnostics!.RequestUri.ToString());
    }
}
