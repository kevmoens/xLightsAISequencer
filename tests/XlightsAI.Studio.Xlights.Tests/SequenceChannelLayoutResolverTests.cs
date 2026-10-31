using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

public sealed class SequenceChannelLayoutResolverTests
{
    private static DisplayModel Model(string name, int nodeCount) =>
        new(new ModelId(name), name, "Single Line", [], [], StringType: "RGB Nodes", NodeCount: nodeCount);

    private static XsqStructuralElement DisplayElement(string name) => new("model", name, Effects: []);

    /// <summary>Mirrors the real, directly-verified render: MiniStar1 (20 nodes) then Front Door (100 nodes), in that order, produced exactly 360 channels (20*3 + 100*3), MiniStar1 at offset 0 and Front Door at offset 60.</summary>
    [Fact]
    public void Resolve_ConcatenatesModelsInDisplayElementsOrder_StartingAtZero()
    {
        var layout = new DisplayGraph("hash", [Model("MiniStar1", 20), Model("Front Door", 100)], [], []);
        var displayElements = new[] { DisplayElement("MiniStar1"), DisplayElement("Front Door") };

        var spans = SequenceChannelLayoutResolver.Resolve(displayElements, layout);

        Assert.Equal([new ModelStringChannelSpan(0, 20)], spans[new ModelId("MiniStar1")]);
        Assert.Equal([new ModelStringChannelSpan(60, 100)], spans[new ModelId("Front Door")]);
    }

    /// <summary>Mirrors the real, directly-verified render: the "Arches" modelGroup (4 real 100-node members) produced exactly 1200 channels (4*100*3).</summary>
    [Fact]
    public void Resolve_ExpandsModelGroupToMembersInDeclaredOrder()
    {
        var layout = new DisplayGraph(
            "hash",
            [Model("Arches-1 Outside", 100), Model("Arches-2", 100), Model("Arches-3", 100), Model("Arches-4", 100)],
            [new DisplayModelGroup("Arches", [
                new DisplayGroupMember("Arches-1 Outside", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arches-2", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arches-3", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arches-4", DisplayGroupMemberKind.Model),
            ])],
            []);
        var displayElements = new[] { DisplayElement("Arches") };

        var spans = SequenceChannelLayoutResolver.Resolve(displayElements, layout);

        Assert.Equal([new ModelStringChannelSpan(0, 100)], spans[new ModelId("Arches-1 Outside")]);
        Assert.Equal([new ModelStringChannelSpan(300, 100)], spans[new ModelId("Arches-2")]);
        Assert.Equal([new ModelStringChannelSpan(600, 100)], spans[new ModelId("Arches-3")]);
        Assert.Equal([new ModelStringChannelSpan(900, 100)], spans[new ModelId("Arches-4")]);
    }

    [Fact]
    public void Resolve_NestedGroup_ExpandsRecursively()
    {
        var layout = new DisplayGraph(
            "hash",
            [Model("Inner1", 5), Model("Inner2", 5)],
            [
                new DisplayModelGroup("Outer", [new DisplayGroupMember("Inner", DisplayGroupMemberKind.Group)]),
                new DisplayModelGroup("Inner", [
                    new DisplayGroupMember("Inner1", DisplayGroupMemberKind.Model),
                    new DisplayGroupMember("Inner2", DisplayGroupMemberKind.Model),
                ]),
            ],
            []);
        var displayElements = new[] { DisplayElement("Outer") };

        var spans = SequenceChannelLayoutResolver.Resolve(displayElements, layout);

        Assert.Equal([new ModelStringChannelSpan(0, 5)], spans[new ModelId("Inner1")]);
        Assert.Equal([new ModelStringChannelSpan(15, 5)], spans[new ModelId("Inner2")]);
    }

    [Fact]
    public void Resolve_UnresolvedMemberOrZeroNodeCount_ContributesNoChannelsAndIsSkipped()
    {
        var layout = new DisplayGraph("hash", [Model("Known", 5)], [], []);
        var displayElements = new[] { DisplayElement("Unknown"), DisplayElement("Known") };

        var spans = SequenceChannelLayoutResolver.Resolve(displayElements, layout);

        Assert.False(spans.ContainsKey(new ModelId("Unknown")));
        Assert.Equal([new ModelStringChannelSpan(0, 5)], spans[new ModelId("Known")]); // Known still starts at 0 -- the skipped unknown contributed nothing.
    }

    [Fact]
    public void Resolve_TimingElement_IsIgnored()
    {
        var layout = new DisplayGraph("hash", [Model("Known", 5)], [], []);
        var displayElements = new[] { new XsqStructuralElement("timing", "Beats", Effects: []), DisplayElement("Known") };

        var spans = SequenceChannelLayoutResolver.Resolve(displayElements, layout);

        Assert.Equal([new ModelStringChannelSpan(0, 5)], spans[new ModelId("Known")]);
    }
}
