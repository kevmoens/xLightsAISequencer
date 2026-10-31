namespace XlightsAI.Studio.Director;

/// <summary>
/// Token usage reported by a provider for one call, generalized across wire formats. Architecture §14:
/// "instrument cache_read_input_tokens to catch silent invalidation" -- <see cref="CacheReadInputTokens"/>
/// is populated from Anthropic's own <c>usage.cache_read_input_tokens</c> or the OpenAI-compatible
/// <c>usage.prompt_tokens_details.cached_tokens</c>, whichever the responding provider's wire format
/// actually carries; <see langword="null"/> (never <c>0</c>) when the provider's response carried no such
/// field at all, so "zero cache reads" and "this provider doesn't report caching" are never confused.
/// </summary>
public sealed record AiUsage(int? InputTokens, int? OutputTokens, int? CacheReadInputTokens, int? CacheCreationInputTokens);
