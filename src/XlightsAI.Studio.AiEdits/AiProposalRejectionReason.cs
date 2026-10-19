namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Why <see cref="HttpStructuredOutputAiPlanner.ProposeAsync"/> rejected an optional-provider response,
/// the same stable-non-string-reason role <see cref="XlightsAI.Studio.Editing.EditRejectionReason"/>
/// already plays for the local interpreter. PHASE-11 Section 2's own rule, verbatim: "Treat model output
/// as untrusted: strict JSON schema; reject unknown properties, unknown models, stale revision, time
/// scope extension... and unsupported recipes" -- every value below is one of those named rejections,
/// plus the two transport-level failure modes Step 075's own implementation guidance names
/// ("enforce size/timeouts... and retries").
/// </summary>
public enum AiProposalRejectionReason
{
    /// <summary>The response body could not be parsed as JSON, deserialized to <see langword="null"/>, or carried a property outside the declared schema (Section 2: "reject unknown properties").</summary>
    MalformedJson,

    /// <summary>An operation's <c>Kind</c> is not one of <see cref="StructuredEditOperationKinds"/>'s own closed vocabulary.</summary>
    UnknownOperationKind,

    /// <summary>An operation targets a model outside the originating <see cref="SelectionContext.Scope"/> (Section 2: never let AI output "widen... the model set a user actually selected").</summary>
    OutOfScopeTarget,

    /// <summary>An operation's time range falls outside, or is not strictly ordered within, the originating <see cref="SelectionContext.Scope"/> (Section 2: "time scope extension").</summary>
    OutOfScopeTime,

    /// <summary>The <see cref="SelectionContext"/>'s own captured revision no longer matches the document's current revision -- checked before any outbound request is even sent.</summary>
    StaleRevision,

    /// <summary>The response declared, or actually was, larger than <see cref="AiProviderOptions.MaxResponseBytes"/> -- rejected independent of whether it would otherwise parse.</summary>
    ResponseTooLarge,

    /// <summary>The response carried zero operations -- never a valid, actionable proposal.</summary>
    EmptyOperations,

    /// <summary>Every attempt (initial plus <see cref="AiProviderOptions.MaxRetries"/> retries) failed for a transient (network/timeout/5xx) reason; the provider itself, not the response shape, is the problem.</summary>
    ProviderUnavailable,
}
