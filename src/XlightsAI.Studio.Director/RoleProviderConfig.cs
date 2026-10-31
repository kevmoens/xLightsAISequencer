namespace XlightsAI.Studio.Director;

/// <summary>
/// One <see cref="AiRole"/>'s own persisted provider configuration -- never carries a secret itself
/// (mirrors <c>XlightsAI.Studio.AiEdits.AiProviderSettings</c>'s own "<see cref="CredentialName"/> is a
/// lookup key into <c>ICredentialStore</c>, never the secret" discipline). The Settings screen writes
/// this (via <see cref="IRoleProviderConfigStore"/>) and the credential (via
/// <c>XlightsAI.Studio.AiEdits.ICredentialStore</c>) as two independent writes, exactly like
/// <c>AiProviderSettings</c>/<c>DpapiCredentialStore</c> already do for the single-role AiEdits planner.
/// </summary>
/// <param name="Role">Which of the three independently configurable roles this is.</param>
/// <param name="Enabled">Whether this role is configured to actually call a provider -- disabled roles (the default) never attempt a network call.</param>
/// <param name="ProviderType">Which wire format <see cref="Endpoint"/> speaks.</param>
/// <param name="Endpoint">The base API endpoint -- e.g. <c>https://api.anthropic.com</c>, <c>https://api.openai.com</c>, an Azure AI Foundry deployment URL, or <c>http://127.0.0.1:11434</c> for local Ollama.</param>
/// <param name="ModelName">The provider's own model identifier (e.g. <c>claude-opus-5</c>, <c>gpt-5</c>, a local Ollama tag).</param>
/// <param name="CredentialName">The lookup key into <c>ICredentialStore</c> for this role's own API key, or <see langword="null"/> for a provider needing none (local Ollama).</param>
/// <param name="RequestTimeoutSeconds">Per-attempt HTTP timeout.</param>
/// <param name="MaxRetries">Maximum retry attempts after a transient failure, exponential backoff between attempts.</param>
/// <param name="MaxResponseBytes">Response size cap, enforced against both the declared <c>Content-Length</c> and the actually-read body.</param>
/// <param name="ReasoningEffort">
/// Build-plan Phase 11 §2b: an optional reasoning-effort/thinking-budget hint (e.g. <c>"low"</c>/<c>"medium"</c>/<c>"high"</c>),
/// forwarded only by a client whose provider/model actually supports it (currently <see cref="OpenAiResponsesChatCompletionClient"/>'s
/// own <c>reasoning.effort</c> field) -- <see langword="null"/> (the default) does not mean "unbounded": that
/// client still sends its own conservative default rather than omitting the field and leaving the model's own
/// unbounded default in effect, which is what produced the ~57-minute single call this phase's incident traced.
/// </param>
public sealed record RoleProviderConfig(
    AiRole Role,
    bool Enabled,
    AiProviderType ProviderType,
    string Endpoint,
    string ModelName,
    string? CredentialName,
    int RequestTimeoutSeconds,
    int MaxRetries,
    int MaxResponseBytes,
    string? ReasoningEffort = null)
{
    /// <summary>A disabled placeholder for <paramref name="role"/> -- the safe default before a user ever opens the Settings screen.</summary>
    public static RoleProviderConfig Disabled(AiRole role) => new(
        role, Enabled: false, AiProviderType.OpenAiCompatible, Endpoint: "http://127.0.0.1:11434",
        ModelName: "", CredentialName: null, RequestTimeoutSeconds: 60, MaxRetries: 2, MaxResponseBytes: 262_144);
}
