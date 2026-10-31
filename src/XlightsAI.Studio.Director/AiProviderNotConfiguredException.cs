namespace XlightsAI.Studio.Director;

/// <summary>
/// Thrown when an <see cref="AiRole"/> this caller requires has no enabled <see cref="RoleProviderConfig"/>
/// (the user has never configured a provider for it, or explicitly disabled it) -- the same "recoverable,
/// explicit, user-actionable state" shape as <c>Stems.ModelAssetConsentRequiredException</c>, not a
/// generic failure. A caller cannot proceed without a real <see cref="IChatCompletionClient"/> for the
/// role, so this is surfaced rather than silently falling back to a degraded/no-op generator.
/// </summary>
public sealed class AiProviderNotConfiguredException : Exception
{
    public AiRole Role { get; }

    public AiProviderNotConfiguredException(AiRole role)
        : base($"No AI provider is configured for the {role} role. Configure one in Settings before generating a whole-song plan.")
    {
        Role = role;
    }
}
