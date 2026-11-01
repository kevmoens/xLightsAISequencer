namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 16 §2b: the narrow seam <see cref="ExemplarDescriptionEnricher"/> calls to generate
/// one real model-written description per <see cref="ExemplarEntry"/>. Deliberately not
/// <c>XlightsAI.Studio.Director.IChatCompletionClient</c> directly -- Director already references
/// Pipeline (for <see cref="ExemplarIndex"/>/<see cref="CraftRules"/>), so the reverse reference this
/// enrichment step would otherwise need is cyclic. A real composition-root adapter wraps the Director
/// role's own configured <c>IChatCompletionClient</c> to implement this.
/// </summary>
public interface IExemplarDescriptionClient
{
    /// <summary>Returns the model's own free-text response to <paramref name="prompt"/>, trimmed of nothing -- the caller trims/validates.</summary>
    Task<string> DescribeAsync(string prompt, CancellationToken ct);
}
