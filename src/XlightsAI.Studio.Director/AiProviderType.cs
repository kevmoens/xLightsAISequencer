namespace XlightsAI.Studio.Director;

/// <summary>
/// Which real wire format a role's configured endpoint speaks. Two values cover four real providers:
/// <see cref="Anthropic"/> (the Messages API, with real <c>cache_control</c> prompt caching) and
/// <see cref="OpenAiCompatible"/> (the <c>/v1/chat/completions</c> shape OpenAI, Azure AI Foundry, and
/// Ollama-local all three speak identically, distinguished only by <see cref="RoleProviderConfig.Endpoint"/>
/// and whether a credential is configured).
/// </summary>
public enum AiProviderType
{
    Anthropic,
    OpenAiCompatible,
}
