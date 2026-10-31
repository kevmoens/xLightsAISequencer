namespace XlightsAI.Studio.Director;

/// <summary>
/// Architecture §14's three independently configurable LLM roles. Each role has its own
/// <see cref="RoleProviderConfig"/> (provider, endpoint, model, credential) -- a customer may run the
/// Director on a frontier cloud model while running the Choreographer locally on Ollama, exactly as
/// architecture §14's own cost/quality table describes.
/// </summary>
public enum AiRole
{
    /// <summary>Long context, strong reasoning, taste -- one call per song (L2, this phase's own scope).</summary>
    Director,

    /// <summary>Reliable structured output, cheap, parallel/batchable -- ~30 calls per song (L3, a later phase).</summary>
    Choreographer,

    /// <summary>Vision + judgment, a different model family from the Director -- few calls per iteration (L6, a later phase).</summary>
    Critic,
}
