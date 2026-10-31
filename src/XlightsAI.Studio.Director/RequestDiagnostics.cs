namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 11 §2c: what was actually sent for one chat-completion call -- captured so the
/// incident this phase fixes (reconstructing a real request URL from source, by hand) never has to happen
/// again. Carried on both a successful <see cref="ChatCompletionResult"/> and a failed
/// <see cref="AiCallRejectedException"/>, and from there into <see cref="AiCallLogEntry"/>, so the call log
/// alone answers "what did we actually send" without re-deriving it.
/// </summary>
/// <param name="RequestUri">The real URI this call sent to the provider.</param>
/// <param name="RequestBody">
/// The real request body sent, bounded by <see cref="RequestLogTruncation.Truncate"/> the same way
/// <see cref="HttpJsonRequestExecutor"/> already bounds response bodies against
/// <see cref="RoleProviderConfig.MaxResponseBytes"/> -- <see langword="null"/> only when no single
/// per-call body exists to report (e.g. one item inside a shared Anthropic batch submission).
/// </param>
/// <param name="EndpointSanityWarning">
/// Set by <see cref="EndpointSanityChecker.CheckDivergence"/> when <paramref name="RequestUri"/>'s own path
/// diverges from <see cref="RoleProviderConfig.Endpoint"/>'s own configured path in a way that suggests the
/// request is not actually reaching the configured API surface -- a warning, never a hard failure, since a
/// deliberate compatibility redirect may be intentional for some other provider.
/// </param>
public sealed record RequestDiagnostics(Uri RequestUri, string? RequestBody, string? EndpointSanityWarning);

/// <summary>
/// Build-plan Phase 11 §2c task 2: the "first-call sanity check" comparing a constructed request URL's own
/// path against the configured <see cref="RoleProviderConfig.Endpoint"/>'s own path -- a Responses-API
/// endpoint silently becoming a chat-completions call (this phase's real incident) produced no warning
/// anywhere; this is the cheap, always-on replacement.
/// </summary>
internal static class EndpointSanityChecker
{
    public static string? CheckDivergence(string configuredEndpoint, Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var configuredUri))
        {
            return null;
        }

        var configuredPath = configuredUri.AbsolutePath.TrimEnd('/');
        if (configuredPath.Length == 0)
        {
            // A bare host endpoint (e.g. "https://api.openai.com") names no API path of its own to compare
            // against -- the client appending its own target path is the expected convention, not a divergence.
            return null;
        }

        var requestPath = requestUri.AbsolutePath.TrimEnd('/');
        if (string.Equals(configuredPath, requestPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"Configured endpoint path '{configuredPath}' does not match the actual request path " +
               $"'{requestPath}' -- the configured API surface may not be the one actually being called.";
    }
}

/// <summary>Shared truncation for a request/response body headed into a diagnostic record, bounded by byte count (not char count) since providers report sizes in bytes.</summary>
internal static class RequestLogTruncation
{
    public static string Truncate(string value, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (System.Text.Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        var truncated = value[..Math.Min(value.Length, maxBytes)];
        while (truncated.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(truncated) > maxBytes)
        {
            truncated = truncated[..^1];
        }

        return truncated + "...(truncated)";
    }
}
