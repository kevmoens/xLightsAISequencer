using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// No real Anthropic credential is available in this environment, so this exercises the real
/// <see cref="AnthropicChatCompletionClient"/> against an in-process fake <see cref="HttpMessageHandler"/>
/// -- the exact convention <c>XlightsAI.Studio.AiEdits.Tests</c> already established for this class of
/// code (<c>CountingHandler</c>), applied here to verify the real Anthropic wire shape (cache_control,
/// x-api-key, usage fields) this client builds/parses.
/// </summary>
public sealed class AnthropicChatCompletionClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            return respond(request, LastRequestBody);
        }
    }

    private static RoleProviderConfig Config() => new(
        AiRole.Director, Enabled: true, AiProviderType.Anthropic, "https://api.anthropic.com", "claude-opus-5",
        CredentialName: "test-cred", RequestTimeoutSeconds: 30, MaxRetries: 1, MaxResponseBytes: 65_536);

    [Fact]
    public async Task CompleteAsync_SendsCacheControlOnSystemBlock_AndApiKeyHeader_WhenCachingRequested()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"content":[{"type":"text","text":"{\"ok\":true}"}],"usage":{"input_tokens":100,"output_tokens":10,"cache_read_input_tokens":80,"cache_creation_input_tokens":0}}""",
                Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new AnthropicChatCompletionClient(Config(), executor, apiKey: "sk-test-secret");

        var result = await client.CompleteAsync(new ChatCompletionRequest("SYSTEM", "USER", CacheSystemPrompt: true, MaxOutputTokens: 100), CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result.Text);
        Assert.Equal(80, result.Usage.CacheReadInputTokens);
        Assert.Equal(100, result.Usage.InputTokens);

        Assert.True(handler.LastRequest!.Headers.TryGetValues("x-api-key", out var keys));
        Assert.Equal("sk-test-secret", keys!.Single());
        Assert.True(handler.LastRequest.Headers.TryGetValues("anthropic-version", out _));

        var sentBody = JsonNode.Parse(handler.LastRequestBody!)!;
        Assert.Equal("ephemeral", sentBody["system"]![0]!["cache_control"]!["type"]!.GetValue<string>());
        Assert.Equal("SYSTEM", sentBody["system"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("USER", sentBody["messages"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task CompleteAsync_NoCredential_SendsNoApiKeyHeader()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"content":[{"type":"text","text":"{}"}],"usage":{}}""", Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new AnthropicChatCompletionClient(Config(), executor, apiKey: null);

        await client.CompleteAsync(new ChatCompletionRequest("S", "U", false, 10), CancellationToken.None);

        Assert.False(handler.LastRequest!.Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task CompleteAsync_UnauthorizedResponse_ThrowsWithUnauthorizedReason_NeverRetried()
    {
        var attempts = 0;
        var handler = new FakeHandler((_, _) =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("denied") };
        });
        using var httpClient = new HttpClient(handler);
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new AnthropicChatCompletionClient(Config(), executor, apiKey: "bad-key");

        var ex = await Assert.ThrowsAsync<AiCallRejectedException>(
            () => client.CompleteAsync(new ChatCompletionRequest("S", "U", false, 10), CancellationToken.None));

        Assert.Equal(AiCallRejectionReason.Unauthorized, ex.Reason);
        Assert.Equal(1, attempts); // never retried -- credential rejection is not transient.
    }

    [Fact]
    public async Task CompleteAsync_OversizedResponse_ThrowsResponseTooLarge()
    {
        var hugeText = new string('x', 200_000);
        var body = "{\"content\":[{\"type\":\"text\",\"text\":\"" + hugeText + "\"}],\"usage\":{}}";
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var configWithSmallCap = Config() with { MaxResponseBytes = 1024 };
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new AnthropicChatCompletionClient(configWithSmallCap, executor, apiKey: "k");

        var ex = await Assert.ThrowsAsync<AiCallRejectedException>(
            () => client.CompleteAsync(new ChatCompletionRequest("S", "U", false, 10), CancellationToken.None));

        Assert.Equal(AiCallRejectionReason.ResponseTooLarge, ex.Reason);
    }
}
