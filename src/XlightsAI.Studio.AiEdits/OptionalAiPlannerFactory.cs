namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// The single, required composition point for obtaining an <see cref="IOptionalAiPlanner"/>. Required
/// acceptance, verbatim: "Provider off means no outbound calls" -- this factory is the one place that
/// decision is made: when <paramref name="options"/> is disabled, it returns
/// <see cref="NullOptionalAiPlanner.Instance"/> and the <paramref name="httpClient"/>/<paramref name="apiKey"/>
/// arguments are never touched, never stored and never used -- not even to validate them. No
/// application code should construct <see cref="HttpStructuredOutputAiPlanner"/> directly for a
/// possibly-disabled configuration; only this factory may.
/// </summary>
public static class OptionalAiPlannerFactory
{
    /// <param name="options">The provider configuration to honor.</param>
    /// <param name="httpClient">An optional caller-supplied transport (e.g. for test doubles); if <see langword="null"/> and <paramref name="options"/> is enabled, a new owned <see cref="HttpClient"/> is created and disposed with the returned planner. Ignored entirely when <paramref name="options"/> is disabled.</param>
    /// <param name="apiKey">A BYOK cloud API key already retrieved from <see cref="ICredentialStore"/>, or <see langword="null"/>. Ignored entirely when <paramref name="options"/> is disabled.</param>
    public static IOptionalAiPlanner Create(AiProviderOptions options, HttpClient? httpClient = null, string? apiKey = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return NullOptionalAiPlanner.Instance;
        }

        return httpClient is null
            ? new HttpStructuredOutputAiPlanner(options, new HttpClient(), ownsHttpClient: true, apiKey)
            : new HttpStructuredOutputAiPlanner(options, httpClient, ownsHttpClient: false, apiKey);
    }
}
