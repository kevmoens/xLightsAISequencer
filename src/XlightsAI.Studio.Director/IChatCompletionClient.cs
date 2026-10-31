namespace XlightsAI.Studio.Director;

/// <summary>
/// One chat-completion call to a configured role's own provider. <see cref="SystemPrompt"/> is the
/// stable, cacheable prefix (effect vocabulary, craft rules, exemplars -- architecture §14: "put stable
/// content first"); <see cref="UserPrompt"/> is the small, volatile per-song suffix (music model, layout,
/// style). Kept as this two-part split (not a richer multi-turn message list) because that is exactly
/// the cache boundary every supported provider's own prompt-caching mechanism keys on.
/// </summary>
public sealed record ChatCompletionRequest(string SystemPrompt, string UserPrompt, bool CacheSystemPrompt, int MaxOutputTokens);

/// <summary>
/// One call's raw result: the model's own text output plus whatever usage the provider reported.
/// <see cref="Diagnostics"/> (Phase 11 §2c) is optional and defaults to <see langword="null"/> so every
/// existing fake-transport test constructing this with just (Text, Usage) keeps compiling unchanged.
/// </summary>
public sealed record ChatCompletionResult(string Text, AiUsage Usage, RequestDiagnostics? Diagnostics = null);

/// <summary>A provider-specific wire adapter. Implementations: <see cref="AnthropicChatCompletionClient"/>, <see cref="OpenAiCompatibleChatCompletionClient"/>.</summary>
public interface IChatCompletionClient
{
    /// <exception cref="AiCallRejectedException">The provider is unreachable after every retry, rejected the credential, or returned a response this client cannot parse/that exceeds the configured size cap.</exception>
    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct);
}
