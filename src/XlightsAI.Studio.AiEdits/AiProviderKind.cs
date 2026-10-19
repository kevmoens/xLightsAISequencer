namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 075 ("Add optional Ollama/cloud provider behind opt-in structured-output adapter"):
/// which optional, opt-in structured-output provider an <see cref="AiProviderSettings"/>/
/// <see cref="AiProviderOptions"/> targets. Implementation guidance, verbatim: "Provider interface
/// supports offline local and BYOK cloud." Both values share the exact same request/response envelope
/// and the exact same <see cref="HttpStructuredOutputAiPlanner"/> transport -- this enum only changes
/// which credential (if any) <see cref="ICredentialStore"/> is consulted for, never the wire shape.
/// </summary>
public enum AiProviderKind
{
    /// <summary>A locally-running, offline structured-output endpoint (e.g. Ollama) -- no credential, no per-token cost.</summary>
    OllamaLocal,

    /// <summary>A user-supplied ("Bring Your Own Key") remote structured-output endpoint -- requires an <see cref="ICredentialStore"/> entry.</summary>
    CloudByok,
}
