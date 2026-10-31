namespace XlightsAI.Studio.Director;

/// <summary>
/// Builds a provider's real request URI from <see cref="RoleProviderConfig.Endpoint"/> -- tolerant of a
/// confirmed recurring real-world mistake: a user re-editing the Settings screen pastes the API's own
/// full path (e.g. <c>https://api.anthropic.com/v1/messages</c>) into the base <c>ENDPOINT</c> field
/// instead of just <c>https://api.anthropic.com</c>, so the client's own path-appending doubles it
/// (<c>.../v1/messages/v1/messages</c>), which the provider rejects with a 404. Stripping a known
/// trailing API path before appending the real one makes both conventions work, rather than relying on
/// every future edit of that one field getting it right.
/// </summary>
public static class ProviderEndpointUri
{
    private static readonly string[] KnownRedundantSuffixes = ["v1/messages", "v1/chat/completions"];

    public static Uri Build(string endpoint, string apiPath) => new(BuildBase(endpoint), apiPath);

    /// <summary>The normalized base (trailing-slash) <see cref="Uri"/>, for a caller combining it with more than one relative path (e.g. batch submit/poll).</summary>
    public static Uri BuildBase(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        foreach (var suffix in KnownRedundantSuffixes)
        {
            if (trimmed.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^(suffix.Length + 1)];
                break;
            }
        }

        return new Uri(trimmed + "/");
    }

    /// <summary>
    /// Phase 11 §2a: true when <paramref name="endpoint"/> names the Azure OpenAI/OpenAI **Responses API**
    /// surface itself (<c>.../openai/responses</c>, with or without a trailing <c>api-version</c> query
    /// string) rather than a base host a relative path can be appended to. Confirmed as the real
    /// configured shape in the incident this phase fixes: <see cref="Build"/>'s suffix-stripping only knew
    /// about <c>v1/messages</c>/<c>v1/chat/completions</c>, so this shape fell through untouched and had
    /// its own path and <c>api-version</c> silently dropped in favor of a different, unintended endpoint.
    /// </summary>
    public static bool IsResponsesApiEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.AbsolutePath.TrimEnd('/').EndsWith("/openai/responses", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The real Responses API request URI, used exactly as configured -- including its own <c>api-version</c>
    /// query string -- since (unlike <see cref="Build"/>'s base-host convention) this endpoint shape already
    /// names the full target URL, with no relative path left to append. Only meaningful when
    /// <see cref="IsResponsesApiEndpoint"/> is true.
    /// </summary>
    public static Uri BuildResponses(string endpoint) => new(endpoint);
}
