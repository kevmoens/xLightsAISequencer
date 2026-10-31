namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 7c "visual critique": the same stable-prefix/volatile-suffix split as
/// <see cref="ChatCompletionRequest"/>, with <see cref="ImagesPng"/> carrying the rendered frame(s) a
/// vision-capable provider attaches alongside <see cref="UserPrompt"/>. A separate record rather than an
/// optional field on <see cref="ChatCompletionRequest"/>, because only some providers/roles ever need
/// this shape (architecture §14: Critic is the one role that needs vision) -- see <see cref="IVisionCapableChatCompletionClient"/>.
/// </summary>
/// <param name="SystemPrompt">The stable, cacheable prefix -- same role as <see cref="ChatCompletionRequest.SystemPrompt"/>.</param>
/// <param name="UserPrompt">The volatile per-call text -- the section's declared intent, the pairwise-ordering instructions, and what verdict shape is expected.</param>
/// <param name="ImagesPng">One or more PNG-encoded frames (<see cref="Rendering.PreviewFrameRasterizer"/>'s output), in the order they should be presented.</param>
/// <param name="CacheSystemPrompt">Same meaning as <see cref="ChatCompletionRequest.CacheSystemPrompt"/>.</param>
/// <param name="MaxOutputTokens">Same meaning as <see cref="ChatCompletionRequest.MaxOutputTokens"/>.</param>
public sealed record MultimodalChatCompletionRequest(
    string SystemPrompt, string UserPrompt, IReadOnlyList<byte[]> ImagesPng, bool CacheSystemPrompt, int MaxOutputTokens);

/// <summary>
/// Optional capability on top of <see cref="IChatCompletionClient"/> -- not every configured provider
/// supports image input (a local non-vision Ollama tag, a plain text-only OpenAI-compatible deployment),
/// so this mirrors <see cref="IBatchChatCompletionClient"/>'s own established pattern exactly: a separate
/// interface, type-tested against whatever concrete client a role resolved to, never a required method on
/// the base interface and never a second client-construction path.
/// </summary>
public interface IVisionCapableChatCompletionClient
{
    /// <exception cref="AiCallRejectedException">Same meanings as <see cref="IChatCompletionClient.CompleteAsync"/>.</exception>
    Task<ChatCompletionResult> CompleteAsync(MultimodalChatCompletionRequest request, CancellationToken ct);
}
