namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Thrown by <see cref="NullOptionalAiPlanner.ProposeAsync"/> -- the only way
/// <see cref="IOptionalAiPlanner.ProposeAsync"/> can ever fail when no provider is enabled. Step 075's
/// own required acceptance, verbatim: "Provider off means no outbound calls" -- this exception is thrown
/// before any <see cref="HttpClient"/> is even constructed, let alone used; a caller catching this
/// exception can safely assume zero network activity occurred.
/// </summary>
public sealed class AiProviderDisabledException : Exception
{
    public AiProviderDisabledException()
        : base("No optional AI provider is enabled; no outbound request was made.")
    {
    }
}
