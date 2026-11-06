using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XlightsAI.Studio.AiEdits;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Resolves one <see cref="AiRole"/>'s own configured <see cref="IChatCompletionClient"/>, reading its
/// credential (if any) from the same <see cref="ICredentialStore"/> AiEdits already uses for BYOK --
/// reused directly, not reimplemented.
/// </summary>
public sealed class ChatCompletionClientFactory
{
    private readonly ICredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ChatCompletionClientFactory> _logger;

    public ChatCompletionClientFactory(ICredentialStore credentialStore, HttpClient httpClient, ILoggerFactory? loggerFactory = null)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<ChatCompletionClientFactory>();
    }

    /// <summary>Returns <see langword="null"/> when <paramref name="config"/> is not <see cref="RoleProviderConfig.Enabled"/> -- an explicit "this role is not configured" result, never a silently-constructed client that would immediately fail.</summary>
    public async Task<IChatCompletionClient?> TryCreateAsync(RoleProviderConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.Enabled)
        {
            _logger.LogDebug("Provider config for {ProviderType} is disabled; no client created.", config.ProviderType);
            return null;
        }

        var apiKey = config.CredentialName is { Length: > 0 } name
            ? await _credentialStore.TryGetAsync(name, ct).ConfigureAwait(false)
            : null;

        var executor = new HttpJsonRequestExecutor(_httpClient, logger: _loggerFactory.CreateLogger<HttpJsonRequestExecutor>());

        _logger.LogInformation("Resolved chat-completion client: provider={ProviderType}, model={Model}, endpoint={Endpoint}",
            config.ProviderType, config.ModelName, config.Endpoint);

        return config.ProviderType switch
        {
            // Build-plan Phase 6 task 2: returns the batch-capable Anthropic client so a caller holding
            // an IChatCompletionClient can always probe it for IBatchChatCompletionClient (a type test,
            // not a second factory method) and use the real Batch API "where available" -- never a
            // separate client instance per role per call, since both interfaces share one transport.
            AiProviderType.Anthropic => new AnthropicBatchChatCompletionClient(config, executor, apiKey),
            // Build-plan Phase 11 §2a: an endpoint naming the Responses API shape (.../openai/responses)
            // speaks a different wire format entirely -- routed here automatically, with no new
            // user-facing setting required, since this phase's own incident confirmed the classic
            // chat-completions client silently mishandles that shape instead of ever failing loudly.
            AiProviderType.OpenAiCompatible when ProviderEndpointUri.IsResponsesApiEndpoint(config.Endpoint) =>
                new OpenAiResponsesChatCompletionClient(config, executor, apiKey),
            AiProviderType.OpenAiCompatible => new OpenAiCompatibleChatCompletionClient(config, executor, apiKey),
            _ => throw new ArgumentOutOfRangeException(nameof(config), config.ProviderType, "Unknown provider type."),
        };
    }
}
