namespace XlightsAI.Studio.Director;

/// <summary>
/// Why a chat-completion call was rejected before any content was trusted -- mirrors
/// <c>XlightsAI.Studio.AiEdits.AiProposalRejectionReason</c>'s own typed-rejection-taxonomy discipline,
/// scoped to transport/transcription concerns only (schema-level Show Bible validation failures are
/// reported separately by <see cref="ShowBibleValidator"/>, never conflated with a transport failure).
/// </summary>
public enum AiCallRejectionReason
{
    /// <summary>The provider's own response body was not well-formed JSON, or no JSON object could be located inside the model's text output.</summary>
    MalformedJson,

    /// <summary>The response exceeded the configured size cap, per the declared <c>Content-Length</c> or the actually-read body.</summary>
    ResponseTooLarge,

    /// <summary>Every retry attempt failed transiently (timeout/connection failure) or the provider returned a non-success HTTP status.</summary>
    ProviderUnavailable,

    /// <summary>The configured credential was rejected by the provider (HTTP 401/403).</summary>
    Unauthorized,
}

/// <summary>Thrown by <see cref="IChatCompletionClient"/>/<see cref="StructuredJsonAiClient{TResponse}"/> implementations -- never retried by the caller for a non-transient reason (mirrors <c>AiProposalRejectedException</c>'s own "the response itself is wrong, not the network" rule).</summary>
public sealed class AiCallRejectedException : Exception
{
    public AiCallRejectedException(AiCallRejectionReason reason, string message, Exception? inner = null, RequestDiagnostics? diagnostics = null)
        : base(message, inner)
    {
        Reason = reason;
        Diagnostics = diagnostics;
    }

    public AiCallRejectionReason Reason { get; }

    /// <summary>Phase 11 §2c: the real request this rejected call actually sent, when the throwing client knows it -- <see langword="null"/> for a rejection with no real request attached (e.g. thrown before any URI/body existed).</summary>
    public RequestDiagnostics? Diagnostics { get; }

    /// <summary>Returns a copy of this exception carrying <paramref name="diagnostics"/> -- used by a chat-completion client that already knows the real request URI/body it sent, to attach them to a rejection <see cref="HttpJsonRequestExecutor"/> threw without that context.</summary>
    public AiCallRejectedException WithDiagnostics(RequestDiagnostics diagnostics) =>
        new(Reason, Message, InnerException, diagnostics);
}
