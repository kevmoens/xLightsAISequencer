namespace XlightsAI.Studio.Director.Tests;

/// <summary>An in-process fake implementing both <see cref="IChatCompletionClient"/> and <see cref="IBatchChatCompletionClient"/> -- lets <see cref="SectionScriptBatchGeneratorTests"/> exercise the real batch-capable code path without a real Anthropic account (same "no real network call" convention as <see cref="FakeChatCompletionClient"/>).</summary>
public sealed class FakeBatchChatCompletionClient : IChatCompletionClient, IBatchChatCompletionClient
{
    private readonly Func<IReadOnlyList<ChatCompletionRequest>, IReadOnlyList<BatchItemResult>> _batchResponder;

    public FakeBatchChatCompletionClient(Func<IReadOnlyList<ChatCompletionRequest>, IReadOnlyList<BatchItemResult>> batchResponder)
    {
        _batchResponder = batchResponder;
    }

    public List<IReadOnlyList<ChatCompletionRequest>> BatchCalls { get; } = [];

    public int SingleCallCount { get; private set; }

    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        SingleCallCount++;
        throw new InvalidOperationException("Test did not expect an individual CompleteAsync call.");
    }

    public Task<IReadOnlyList<BatchItemResult>> CompleteBatchAsync(IReadOnlyList<ChatCompletionRequest> requests, CancellationToken ct)
    {
        BatchCalls.Add(requests);
        return Task.FromResult(_batchResponder(requests));
    }
}
