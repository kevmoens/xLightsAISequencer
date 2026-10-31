namespace XlightsAI.Studio.Director.Tests;

/// <summary>Build-plan Phase 6 acceptance: "cost per generation is measured... to calibrate the 'a few dollars per song' target." Never fabricates a number for an unrecognized model.</summary>
public sealed class AiCostEstimatorTests
{
    [Fact]
    public void EstimateUsd_UnrecognizedModel_ReturnsNull()
    {
        var cost = AiCostEstimator.EstimateUsd("qwen2.5-coder:7b-instruct", new AiUsage(10_000, 1_000, 0, 0));

        Assert.Null(cost);
    }

    [Fact]
    public void EstimateUsd_KnownModel_NonCachedTokensOnly_MatchesManualCalculation()
    {
        var cost = AiCostEstimator.EstimateUsd("claude-sonnet-5", new AiUsage(1_000_000, 1_000_000, 0, 0));

        // 1M input @ $3/M + 1M output @ $15/M = $18.
        Assert.Equal(18m, cost);
    }

    [Fact]
    public void EstimateUsd_WithCacheReadTokens_BillsCacheReadPortionAtDiscountRate()
    {
        // 1M total input, all of it a cache read -- none at the full input rate.
        var cost = AiCostEstimator.EstimateUsd("claude-sonnet-5", new AiUsage(1_000_000, 0, 1_000_000, 0));

        // 1M cache-read @ $0.30/M = $0.30.
        Assert.Equal(0.3m, cost);
    }

    [Fact]
    public void EstimateUsd_MatchesSubstring_CaseInsensitive()
    {
        var cost = AiCostEstimator.EstimateUsd("CLAUDE-SONNET-5-20260101", new AiUsage(0, 0, 0, 0));

        Assert.NotNull(cost);
        Assert.Equal(0m, cost);
    }
}
