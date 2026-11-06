using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XlightsAI.Studio.AiEdits;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Composition-root-level helper promoting the credential/client resolution logic
/// <c>AiProviderSettingsView.axaml.cs</c>'s own code-behind already does for the Settings screen's editor
/// into something the Shell's AI choreography wiring can call for real roles at generation time, rather
/// than duplicating the <see cref="JsonRoleProviderConfigStore"/>/<see cref="DpapiCredentialStore"/>/
/// <see cref="ChatCompletionClientFactory"/> wiring a third time.
/// </summary>
public sealed class AiRoleClientResolver
{
    private readonly IRoleProviderConfigStore _configStore;
    private readonly ChatCompletionClientFactory _clientFactory;
    private readonly ILogger<AiRoleClientResolver> _logger;

    public AiRoleClientResolver(
        IRoleProviderConfigStore configStore, ICredentialStore credentialStore, HttpClient httpClient,
        ILoggerFactory? loggerFactory = null, ILogger<AiRoleClientResolver>? logger = null)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(httpClient);
        _clientFactory = new ChatCompletionClientFactory(credentialStore, httpClient, loggerFactory);
        _logger = logger ?? NullLogger<AiRoleClientResolver>.Instance;
    }

    /// <summary>Resolves <paramref name="role"/>'s configured client and model name, or throws when the role is disabled/unconfigured -- never returns a client that would immediately fail.</summary>
    /// <exception cref="AiProviderNotConfiguredException"><paramref name="role"/> has no enabled provider configured.</exception>
    public async Task<(IChatCompletionClient Client, string ModelName)> ResolveRequiredAsync(AiRole role, CancellationToken ct)
    {
        _logger.LogDebug("Resolving AI client for role {Role}.", role);
        var config = await _configStore.LoadAsync(role, ct).ConfigureAwait(false);
        var client = await _clientFactory.TryCreateAsync(config, ct).ConfigureAwait(false);
        if (client is null)
        {
            _logger.LogWarning("Role {Role} has no enabled AI provider configured.", role);
            throw new AiProviderNotConfiguredException(role);
        }

        _logger.LogInformation("Role {Role} resolved to provider {ProviderType}, model {Model}.", role, config.ProviderType, config.ModelName);
        return (client, config.ModelName);
    }
}
