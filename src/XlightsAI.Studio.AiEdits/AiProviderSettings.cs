namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 075's own persisted provider configuration, written/read by
/// <see cref="IAiProviderSettingsStore"/>. Section 4's own rule, verbatim: "Store... provider/model
/// config (without plaintext secrets)... Never log API keys or whole lyrics by default" -- this record
/// carries no credential of any kind; a BYOK cloud API key lives only in <see cref="ICredentialStore"/>,
/// addressed by <see cref="CredentialName"/> (a lookup key, never the secret itself). Plain
/// JSON-serializer-friendly primitives throughout (no <see cref="TimeSpan"/>) -- call
/// <see cref="ToOptions"/> to obtain the richer runtime <see cref="AiProviderOptions"/>.
/// </summary>
/// <param name="Enabled">The single, explicit, stored opt-in toggle -- "Provider off means no outbound calls" (required acceptance) traces to this flag; never defaults to <see langword="true"/>.</param>
/// <param name="Kind">Which provider family this targets.</param>
/// <param name="Endpoint">The structured-output HTTP endpoint.</param>
/// <param name="ModelName">The provider-side model identifier.</param>
/// <param name="CredentialName">
/// The <see cref="ICredentialStore"/> lookup key for this provider's API key, or <see langword="null"/>
/// for <see cref="AiProviderKind.OllamaLocal"/> (no credential required).
/// </param>
/// <param name="RequestTimeoutSeconds">Per-attempt timeout, in whole seconds.</param>
/// <param name="MaxRetries">How many additional attempts after a transient failure.</param>
/// <param name="MaxResponseBytes">The largest accepted response body, in bytes.</param>
public sealed record AiProviderSettings(
    bool Enabled,
    AiProviderKind Kind,
    Uri Endpoint,
    string ModelName,
    string? CredentialName,
    int RequestTimeoutSeconds,
    int MaxRetries,
    int MaxResponseBytes)
{
    /// <summary>The safe, no-outbound-calls default a fresh install (or a corrupt settings file) falls back to.</summary>
    public static AiProviderSettings Disabled { get; } = new(
        Enabled: false,
        Kind: AiProviderKind.OllamaLocal,
        Endpoint: new Uri("http://localhost:11434/api/generate"),
        ModelName: string.Empty,
        CredentialName: null,
        RequestTimeoutSeconds: 30,
        MaxRetries: 2,
        MaxResponseBytes: 65_536);

    /// <summary>Projects this persisted shape into the richer runtime <see cref="AiProviderOptions"/> an <see cref="OptionalAiPlannerFactory"/> call consumes.</summary>
    public AiProviderOptions ToOptions() => new(
        Enabled,
        Kind,
        Endpoint,
        ModelName,
        TimeSpan.FromSeconds(RequestTimeoutSeconds),
        MaxRetries,
        MaxResponseBytes);
}
