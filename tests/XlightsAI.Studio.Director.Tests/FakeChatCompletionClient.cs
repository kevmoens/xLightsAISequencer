namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// An in-process fake <see cref="IChatCompletionClient"/> double -- mirrors
/// <c>XlightsAI.Studio.AiEdits.Tests</c>'s own established "no real network call, no mocking library"
/// convention (that project uses a fake <c>HttpMessageHandler</c>; this one fakes one layer higher, at
/// the chat-completion boundary, since <see cref="ShowBibleGenerator"/>/<see cref="DirectorPromptBuilder"/>
/// tests need to control responses, not HTTP framing).
/// </summary>
public sealed class FakeChatCompletionClient : IChatCompletionClient
{
    private readonly Queue<Func<ChatCompletionRequest, ChatCompletionResult>> _responses;

    public FakeChatCompletionClient(IEnumerable<Func<ChatCompletionRequest, ChatCompletionResult>> responses)
    {
        _responses = new Queue<Func<ChatCompletionRequest, ChatCompletionResult>>(responses);
    }

    public List<ChatCompletionRequest> Requests { get; } = [];

    public int CallCount { get; private set; }

    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        CallCount++;
        Requests.Add(request);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeChatCompletionClient ran out of canned responses.");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}
