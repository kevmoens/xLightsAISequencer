using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 4's own Look schema (architecture §4 <c>propDelays</c>).</summary>
public sealed class PropDelayResolverTests
{
    [Theory]
    [InlineData("+60ms", 600_000)]
    [InlineData("-10ms", -100_000)]
    [InlineData("0ms", 0)]
    [InlineData("+0ms", 0)]
    public void ParseOffsetTicks_ParsesSignedMillisecondStrings(string offset, long expectedTicks)
    {
        Assert.Equal(expectedTicks, PropDelayResolver.ParseOffsetTicks(offset));
    }

    [Fact]
    public void AssignTiers_HeaviestThirdIsMass_LightestThirdIsAccent()
    {
        var props = new Dictionary<ModelId, int>
        {
            [new ModelId("Heavy1")] = 1000,
            [new ModelId("Heavy2")] = 900,
            [new ModelId("Heavy3")] = 850,
            [new ModelId("Mid1")] = 500,
            [new ModelId("Mid2")] = 450,
            [new ModelId("Mid3")] = 400,
            [new ModelId("Light1")] = 50,
            [new ModelId("Light2")] = 30,
            [new ModelId("Light3")] = 20,
        };

        var tiers = PropDelayResolver.AssignTiers(props);

        Assert.Equal(PropDelayResolver.TierMass, tiers[new ModelId("Heavy1")]);
        Assert.Equal(PropDelayResolver.TierMass, tiers[new ModelId("Heavy2")]);
        Assert.Equal(PropDelayResolver.TierMass, tiers[new ModelId("Heavy3")]);
        Assert.Equal(PropDelayResolver.TierMid, tiers[new ModelId("Mid1")]);
        Assert.Equal(PropDelayResolver.TierAccent, tiers[new ModelId("Light1")]);
        Assert.Equal(PropDelayResolver.TierAccent, tiers[new ModelId("Light3")]);
    }

    [Fact]
    public void ResolveOffsets_UndeclaredTier_DefaultsToZero_NeverFabricated()
    {
        var props = new Dictionary<ModelId, int> { [new ModelId("Solo")] = 100 };
        var offsets = PropDelayResolver.ResolveOffsets(props, new Dictionary<string, string>());

        Assert.Equal(0, offsets[new ModelId("Solo")]);
    }

    [Fact]
    public void ResolveOffsets_UsesDeclaredPropDelays_ByTier()
    {
        var props = new Dictionary<ModelId, int>
        {
            [new ModelId("Heavy")] = 1000,
            [new ModelId("Mid")] = 500,
            [new ModelId("Light")] = 10,
        };
        var propDelays = new Dictionary<string, string>
        {
            [PropDelayResolver.TierMass] = "+0ms",
            [PropDelayResolver.TierMid] = "+60ms",
            [PropDelayResolver.TierAccent] = "+120ms",
        };

        var offsets = PropDelayResolver.ResolveOffsets(props, propDelays);

        Assert.Equal(0, offsets[new ModelId("Heavy")]);
        Assert.Equal(600_000, offsets[new ModelId("Mid")]);
        Assert.Equal(1_200_000, offsets[new ModelId("Light")]);
    }
}
