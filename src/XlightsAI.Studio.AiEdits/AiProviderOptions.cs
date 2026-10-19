namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 075's own runtime configuration for <see cref="OptionalAiPlannerFactory.Create"/>:
/// everything <see cref="HttpStructuredOutputAiPlanner"/> needs to make one bounded, timed-out,
/// retried request. Deliberately distinct from the persisted <see cref="AiProviderSettings"/> (plain
/// <see langword="int"/> seconds, JSON-friendly) -- this record exists only at the call boundary, once a
/// caller has already decided <see cref="Enabled"/>, and carries a real <see cref="TimeSpan"/> rather
/// than a serialization-friendly primitive. Never carries an API key or other secret -- see
/// <see cref="ICredentialStore"/>.
/// </summary>
/// <param name="Enabled">
/// Required acceptance, verbatim: "Provider off means no outbound calls" -- when
/// <see langword="false"/>, <see cref="OptionalAiPlannerFactory.Create"/> returns
/// <see cref="NullOptionalAiPlanner"/> and never constructs or touches an <see cref="HttpClient"/>.
/// </param>
/// <param name="Kind">Which provider family this targets; see <see cref="AiProviderKind"/>.</param>
/// <param name="Endpoint">The structured-output HTTP endpoint to POST the redacted request to.</param>
/// <param name="ModelName">The provider-side model identifier to request, if applicable; never validated here (the provider itself rejects an unknown model).</param>
/// <param name="RequestTimeout">Per-attempt timeout; a slow/unresponsive provider must not hang the caller indefinitely ("enforce size/timeouts," implementation guidance).</param>
/// <param name="MaxRetries">How many additional attempts to make after a transient (network/5xx/timeout) failure; a schema/validation rejection is never retried.</param>
/// <param name="MaxResponseBytes">The largest response body accepted before it is rejected as too large, independent of whether it would otherwise parse ("enforce size," implementation guidance).</param>
public sealed record AiProviderOptions(
    bool Enabled,
    AiProviderKind Kind,
    Uri Endpoint,
    string ModelName,
    TimeSpan RequestTimeout,
    int MaxRetries,
    int MaxResponseBytes)
{
    /// <summary>The safe, no-outbound-calls default -- matches <see cref="AiProviderSettings.Disabled"/>.</summary>
    public static AiProviderOptions Disabled { get; } = new(
        Enabled: false,
        Kind: AiProviderKind.OllamaLocal,
        Endpoint: new Uri("http://localhost:11434/api/generate"),
        ModelName: string.Empty,
        RequestTimeout: TimeSpan.FromSeconds(30),
        MaxRetries: 2,
        MaxResponseBytes: 65_536);
}
