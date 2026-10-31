namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 6 acceptance criterion: "cost per generation is measured and logged... to calibrate
/// the 'a few dollars per song' target." $-per-million-token rates for the small set of real, named model
/// ids this build actually knows pricing for; an unrecognized <see cref="RoleProviderConfig.ModelName"/>
/// (a local Ollama tag, an Azure deployment alias, a model released after this table was written) returns
/// <see langword="null"/> -- never a fabricated number, same discipline <see cref="AiUsage.CacheReadInputTokens"/>
/// already applies to "this provider didn't report it." Rates below are the publicly documented Anthropic
/// API list prices as of this build (per million tokens, USD); cache-read tokens bill at Anthropic's own
/// documented 10% of the base input rate.
/// </summary>
public static class AiCostEstimator
{
    private sealed record Rates(decimal InputPerMillion, decimal OutputPerMillion, decimal CacheReadPerMillion);

    // Matched by substring against RoleProviderConfig.ModelName, most-specific first. Opus/Sonnet/Haiku
    // rates are Anthropic's own published per-tier pricing (Opus the premium tier, Sonnet the
    // mid/"balanced" tier, Haiku the small/fast tier); the legacy claude-3-* rows are the same tiers'
    // pricing from before the "opus-5/sonnet-5/haiku-4-5" naming generation, kept for back-compat so a
    // call log entry from an older configured model string still resolves to a real number rather than
    // silently going null the day a customer's settings still name an older model.
    private static readonly (string Substring, Rates Rates)[] KnownModels =
    [
        ("claude-opus-5", new Rates(15m, 75m, 1.5m)),
        ("claude-sonnet-5", new Rates(3m, 15m, 0.3m)),
        ("claude-haiku-4-5", new Rates(1m, 5m, 0.1m)),
        ("claude-opus-4", new Rates(15m, 75m, 1.5m)),
        ("claude-sonnet-4", new Rates(3m, 15m, 0.3m)),
        ("claude-3-opus", new Rates(15m, 75m, 1.5m)),
        ("claude-3-5-sonnet", new Rates(3m, 15m, 0.3m)),
        ("claude-3-haiku", new Rates(0.25m, 1.25m, 0.03m)),
    ];

    /// <summary>Estimated USD cost of one call's reported <paramref name="usage"/> against <paramref name="modelName"/>, or <see langword="null"/> when <paramref name="modelName"/> matches none of the known rate rows (e.g. a local Ollama model, which is genuinely free, or any model this table has not been updated for).</summary>
    public static decimal? EstimateUsd(string modelName, AiUsage usage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentNullException.ThrowIfNull(usage);

        var rates = KnownModels.FirstOrDefault(m => modelName.Contains(m.Substring, StringComparison.OrdinalIgnoreCase)).Rates;
        if (rates is null)
        {
            return null;
        }

        var cacheReadTokens = usage.CacheReadInputTokens ?? 0;
        var nonCachedInputTokens = Math.Max(0, (usage.InputTokens ?? 0) - cacheReadTokens);
        var outputTokens = usage.OutputTokens ?? 0;

        var cost =
            nonCachedInputTokens / 1_000_000m * rates.InputPerMillion +
            cacheReadTokens / 1_000_000m * rates.CacheReadPerMillion +
            outputTokens / 1_000_000m * rates.OutputPerMillion;

        return Math.Round(cost, 6);
    }
}
