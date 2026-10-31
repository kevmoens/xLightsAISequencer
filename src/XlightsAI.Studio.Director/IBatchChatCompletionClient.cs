namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 6 task 2 / architecture §14: "issue the ~30 per-section calls via the provider's Batch
/// API where available... ~50% off input and output, and it stacks with prompt caching." An optional
/// capability on top of <see cref="IChatCompletionClient"/> -- not every provider has a real batch
/// endpoint (local Ollama and plain OpenAI-compatible chat completions do not), so this is a separate
/// interface a concrete client may additionally implement, rather than a required method every
/// <see cref="IChatCompletionClient"/> must support. <see cref="SectionScriptBatchGenerator"/> checks for
/// this via a type test and falls back to bounded-concurrency individual <see cref="IChatCompletionClient.CompleteAsync"/>
/// calls when it is absent -- literally "where available," per the build plan's own wording.
/// </summary>
public interface IBatchChatCompletionClient
{
    /// <summary>
    /// Submits every request in <paramref name="requests"/> as one provider-side batch job and returns
    /// each one's own result (or rejection) once the whole batch completes, in the same order as
    /// <paramref name="requests"/>. A single element's own <see cref="AiCallRejectedException"/> reason is
    /// carried per-element (not thrown for the whole batch) -- one malformed section response should never
    /// discard every other section's already-successful one.
    /// </summary>
    /// <exception cref="AiCallRejectedException">The batch job itself could not be submitted, or never reached a terminal state (every retry/poll exhausted).</exception>
    Task<IReadOnlyList<BatchItemResult>> CompleteBatchAsync(IReadOnlyList<ChatCompletionRequest> requests, CancellationToken ct);
}

/// <summary>One element's own outcome within a batch -- <see cref="Result"/> is <see langword="null"/> exactly when <see cref="Error"/> is not.</summary>
public sealed record BatchItemResult(ChatCompletionResult? Result, AiCallRejectedException? Error)
{
    public static BatchItemResult Success(ChatCompletionResult result) => new(result, null);

    public static BatchItemResult Failure(AiCallRejectedException error) => new(null, error);
}
