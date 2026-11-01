using XlightsAI.Studio.Pipeline;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 16 §2b: the real composition-root adapter implementing Pipeline's narrow
/// <see cref="IExemplarDescriptionClient"/> seam by wrapping an already-resolved
/// <see cref="IChatCompletionClient"/> -- reuses the Director role's own configured provider rather than
/// inventing a fourth <see cref="AiRole"/>, since exemplar descriptions are a one-off corpus-mining-time
/// enrichment, not a per-song generation call needing its own independently configurable role. Logs every
/// call through the same <see cref="JsonAiCallLogStore"/>/<see cref="AiCostEstimator"/> convention
/// <see cref="SectionScriptGenerator"/>/<see cref="ShowBibleGenerator"/> already use, tagged
/// <see cref="AiRole.Director"/> since that is the real provider/model actually billed.
/// </summary>
public sealed class ChatCompletionExemplarDescriptionClient : IExemplarDescriptionClient
{
    /// <summary>Short responses only -- one or two sentences, never a full Show Bible -- so this never needs the large token budgets Director/Choreographer calls do.</summary>
    private const int MaxOutputTokens = 300;

    private readonly IChatCompletionClient _client;
    private readonly string _modelName;
    private readonly JsonAiCallLogStore? _callLog;

    public ChatCompletionExemplarDescriptionClient(IChatCompletionClient client, string modelName, JsonAiCallLogStore? callLog = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        _modelName = modelName;
        _callLog = callLog;
    }

    public async Task<string> DescribeAsync(string prompt, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var request = new ChatCompletionRequest(
            SystemPrompt: "You are a concise music/lighting analyst annotating a choreography exemplar corpus.",
            UserPrompt: prompt,
            CacheSystemPrompt: false,
            MaxOutputTokens: MaxOutputTokens);

        var result = await _client.CompleteAsync(request, ct).ConfigureAwait(false);

        if (_callLog is not null)
        {
            var cost = AiCostEstimator.EstimateUsd(_modelName, result.Usage);
            await _callLog.AppendAsync(
                new AiCallLogEntry(Guid.Empty, AiRole.Director, _modelName, DateTimeOffset.UtcNow, result.Usage, cost, "exemplar description", result.Diagnostics),
                ct).ConfigureAwait(false);
        }

        return result.Text;
    }
}
