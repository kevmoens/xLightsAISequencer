namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// A genuine end-to-end call against a real, locally running Ollama install (verified present on this
/// machine via <c>OLLAMA_API_BASE</c>/a live <c>/api/tags</c> response before writing this) -- no fake
/// transport, no API key needed. Skips cleanly when Ollama is not reachable, mirroring this codebase's
/// own established "real-machine test, skip if absent" convention.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RealOllamaSmokeTests
{
    private const string OllamaEndpoint = "http://127.0.0.1:11434";
    private const string OllamaModel = "qwen2.5-coder:7b-instruct";

    private static async Task<bool> IsOllamaReachableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync($"{OllamaEndpoint}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task OpenAiCompatibleClient_RealOllamaCall_ReturnsStructuredJson()
    {
        if (!await IsOllamaReachableAsync())
        {
            return;
        }

        var config = new RoleProviderConfig(
            AiRole.Director, Enabled: true, AiProviderType.OpenAiCompatible, OllamaEndpoint, OllamaModel,
            CredentialName: null, RequestTimeoutSeconds: 60, MaxRetries: 1, MaxResponseBytes: 65_536);

        using var httpClient = new HttpClient();
        using var executor = new HttpJsonRequestExecutor(httpClient);
        var client = new OpenAiCompatibleChatCompletionClient(config, executor, apiKey: null);
        var structured = new StructuredJsonAiClient<SmokeTestResponse>(client);

        var request = new ChatCompletionRequest(
            SystemPrompt: "You respond only with a single JSON object, no prose, no markdown fences.",
            UserPrompt: "Respond with exactly this JSON object: {\"greeting\":\"hello\",\"number\":42}",
            CacheSystemPrompt: true,
            MaxOutputTokens: 100);

        var result = await structured.CompleteAsync(request, CancellationToken.None);

        Assert.Equal("hello", result.Value.Greeting);
        Assert.Equal(42, result.Value.Number);
        Assert.True(result.Usage.InputTokens > 0);
        Assert.True(result.Usage.OutputTokens > 0);
        // CacheReadInputTokens is a real field Ollama's own OpenAI-compatible endpoint reports
        // (usage.prompt_tokens_details.cached_tokens, confirmed via a direct curl against this exact
        // endpoint before writing this test) -- not null, even though its value is usually 0 for a
        // single one-off call with no shared prior context.
        Assert.NotNull(result.Usage.CacheReadInputTokens);
    }

    private sealed record SmokeTestResponse(string Greeting, int Number);
}
