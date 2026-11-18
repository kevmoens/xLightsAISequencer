using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

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

    [Fact]
    public void ResolveTravelWindows_AssignsOneMarkPerOrderedModel_InOrder()
    {
        var orderedModels = new[] { new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3") };
        var marks = new[]
        {
            new TimingMark("1", 0, 1_000_000),
            new TimingMark("2", 1_000_000, 2_000_000),
            new TimingMark("3", 2_000_000, 3_000_000),
        };

        var windows = PropDelayResolver.ResolveTravelWindows(orderedModels, marks, sectionStart: 0, sectionEnd: 3_000_000);

        Assert.Equal(3, windows.Count);
        Assert.Equal(new TimeWindow(0, 1_000_000), windows[new ModelId("Arch1")]);
        Assert.Equal(new TimeWindow(1_000_000, 2_000_000), windows[new ModelId("Arch2")]);
        Assert.Equal(new TimeWindow(2_000_000, 3_000_000), windows[new ModelId("Arch3")]);
    }

    [Fact]
    public void ResolveTravelWindows_FewerMarksThanModels_ExtraModelsGetNoWindow_NeverFabricated()
    {
        var orderedModels = new[] { new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3") };
        var marks = new[] { new TimingMark("1", 0, 1_000_000) };

        var windows = PropDelayResolver.ResolveTravelWindows(orderedModels, marks, sectionStart: 0, sectionEnd: 3_000_000);

        var window = Assert.Single(windows);
        Assert.Equal(new ModelId("Arch1"), window.Key);
        Assert.DoesNotContain(new ModelId("Arch2"), windows.Keys);
        Assert.DoesNotContain(new ModelId("Arch3"), windows.Keys);
    }

    [Fact]
    public void ResolveTravelWindows_MarksOutsideSectionWindow_ExcludedOrClamped()
    {
        var orderedModels = new[] { new ModelId("Arch1"), new ModelId("Arch2") };
        var marks = new[]
        {
            new TimingMark("before", -1_000_000, -500_000), // entirely before the section -- excluded.
            new TimingMark("straddling", -500_000, 500_000), // starts before, ends inside -- clamped to sectionStart.
            new TimingMark("inside", 500_000, 1_500_000),
        };

        var windows = PropDelayResolver.ResolveTravelWindows(orderedModels, marks, sectionStart: 0, sectionEnd: 2_000_000);

        Assert.Equal(new TimeWindow(0, 500_000), windows[new ModelId("Arch1")]);
        Assert.Equal(new TimeWindow(500_000, 1_500_000), windows[new ModelId("Arch2")]);
    }
}
