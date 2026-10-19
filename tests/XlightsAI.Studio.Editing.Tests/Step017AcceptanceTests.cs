using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 017 ("Build prop rows, group expansion and stable mapping to model IDs"). Required
/// acceptance: "Visual rows match loaded layout including group nesting." Required failure/edge
/// case: "Invalid model/format/version must fail safely without rewriting source files."
/// </summary>
public sealed class Step017AcceptanceTests
{
    private static DisplayModel Model(string name, params string[] groupNames) =>
        new(new ModelId(name), name, "Arch", groupNames, []);

    private static DisplayModelGroup Group(string name, params (string RawName, DisplayGroupMemberKind Kind)[] members) =>
        new(name, members.Select(m => new DisplayGroupMember(m.RawName, m.Kind)).ToList());

    [Fact]
    public void Step017_Acceptance_Passes_RowsMatchLoadedLayout_IncludingGroupNesting()
    {
        // Front-Yard
        //   Arch-01 (model)
        //   Spot-Group
        //     Arch-02 (model)
        // MegaTree-Center (ungrouped model)
        var arch1 = Model("Arch-01", "Front-Yard");
        var arch2 = Model("Arch-02", "Spot-Group");
        var megaTree = Model("MegaTree-Center");
        var spotGroup = Group("Spot-Group", (arch2.XlightsName, DisplayGroupMemberKind.Model));
        var frontYard = Group(
            "Front-Yard",
            (arch1.XlightsName, DisplayGroupMemberKind.Model),
            ("Spot-Group", DisplayGroupMemberKind.Group));
        var layout = new DisplayGraph(
            "hash-1",
            [arch1, arch2, megaTree],
            [spotGroup, frontYard],
            []);

        var tree = TimelineRowTreeBuilder.Build(layout);

        Assert.Empty(tree.Warnings);
        // Spot-Group is referenced by Front-Yard, so it is not its own top-level root.
        Assert.Equal(2, tree.Roots.Count);

        var frontYardNode = Assert.Single(tree.Roots, r => r.Id == TimelineRowId.ForGroup("Front-Yard"));
        Assert.True(frontYardNode.IsGroup);
        Assert.Equal(2, frontYardNode.Children.Count);

        var arch1Node = Assert.Single(frontYardNode.Children, c => c.Id == TimelineRowId.ForModel(arch1.Id));
        Assert.False(arch1Node.IsGroup);
        Assert.Equal(arch1.Id, arch1Node.ModelId);

        var spotGroupNode = Assert.Single(frontYardNode.Children, c => c.Id == TimelineRowId.ForGroup("Spot-Group"));
        Assert.True(spotGroupNode.IsGroup);
        var arch2Node = Assert.Single(spotGroupNode.Children);
        Assert.Equal(TimelineRowId.ForModel(arch2.Id), arch2Node.Id);

        var megaTreeNode = Assert.Single(tree.Roots, r => r.Id == TimelineRowId.ForModel(megaTree.Id));
        Assert.False(megaTreeNode.IsGroup);
    }

    [Fact]
    public void Step017_Acceptance_Passes_StableMappingToModelIds_SameModelUnderOverlappingGroups()
    {
        // A model belonging to two different groups must keep one single stable TimelineRowId
        // wherever it appears (PHASE-02 Section 3: "Groups may overlap; do not flatten user group
        // membership") — never a different identity per occurrence, and never a display row index.
        var star = Model("Star-Left", "Stars", "Whole-Display");
        var stars = Group("Stars", (star.XlightsName, DisplayGroupMemberKind.Model));
        var wholeDisplay = Group("Whole-Display", (star.XlightsName, DisplayGroupMemberKind.Model));
        var layout = new DisplayGraph("hash-2", [star], [stars, wholeDisplay], []);

        var tree = TimelineRowTreeBuilder.Build(layout);

        Assert.Equal(2, tree.Roots.Count);
        var occurrence1 = Assert.Single(tree.Roots.Single(r => r.Id == TimelineRowId.ForGroup("Stars")).Children);
        var occurrence2 = Assert.Single(tree.Roots.Single(r => r.Id == TimelineRowId.ForGroup("Whole-Display")).Children);

        Assert.Equal(TimelineRowId.ForModel(star.Id), occurrence1.Id);
        Assert.Equal(occurrence1.Id, occurrence2.Id);
        Assert.Equal(star.Id, occurrence1.ModelId);
        Assert.Equal(star.Id, occurrence2.ModelId);
    }

    [Fact]
    public void Step017_Acceptance_Passes_RetainCollapseState_FlattenerSkipsCollapsedGroupChildren()
    {
        var arch1 = Model("Arch-01", "Front-Yard");
        var frontYard = Group("Front-Yard", (arch1.XlightsName, DisplayGroupMemberKind.Model));
        var layout = new DisplayGraph("hash-3", [arch1], [frontYard], []);
        var tree = TimelineRowTreeBuilder.Build(layout);

        var expanded = TimelineRowFlattener.Flatten(tree.Roots, new HashSet<TimelineRowId>());
        Assert.Equal(2, expanded.Count);
        Assert.True(expanded[0].IsExpanded);
        Assert.Equal(0, expanded[0].Depth);
        Assert.Equal(1, expanded[1].Depth);

        var collapsed = TimelineRowFlattener.Flatten(tree.Roots, new HashSet<TimelineRowId> { TimelineRowId.ForGroup("Front-Yard") });
        var onlyRow = Assert.Single(collapsed);
        Assert.False(onlyRow.IsExpanded);
        Assert.True(onlyRow.HasChildren);
    }

    [Fact]
    public void Step017_Acceptance_Passes_EmptyLayout_ProducesEmptyTree_SilenceEdgeCase()
    {
        var tree = TimelineRowTreeBuilder.Build(new DisplayGraph("hash-4", [], [], []));

        Assert.Empty(tree.Roots);
        Assert.Empty(tree.Warnings);
    }

    [Fact]
    public void Step017_Acceptance_Passes_NullLayout_ProducesEmptyTree_MissingMediaEdgeCase()
    {
        var tree = TimelineRowTreeBuilder.Build(null);

        Assert.Empty(tree.Roots);
        Assert.Empty(tree.Warnings);
    }

    [Fact]
    public void Step017_Acceptance_Passes_DirectSelfReferencingGroup_FailsSafely_NegativePath()
    {
        // A group referencing itself (a malformed/adversarial layout, never producible by
        // ShowLayoutImporter today, but not guaranteed never to occur in hand-edited XML) must
        // never crash/stack-overflow the row builder, and must never silently disappear from the
        // tree — the required failure/edge case: "Invalid model/format/version must fail safely."
        var selfReferencing = Group("Self-Loop", ("Self-Loop", DisplayGroupMemberKind.Group));
        var layout = new DisplayGraph("hash-5", [], [selfReferencing], []);

        var tree = TimelineRowTreeBuilder.Build(layout);

        var root = Assert.Single(tree.Roots);
        Assert.Equal(TimelineRowId.ForGroup("Self-Loop"), root.Id);
        Assert.Single(root.Children); // The cycle-truncated child occurrence, not infinite recursion.
        Assert.NotEmpty(tree.Warnings);
    }

    [Fact]
    public void Step017_Acceptance_Passes_MutualCycleWithNoTrueRoot_StillVisible_NegativePath()
    {
        // Two groups referencing only each other, with no non-cyclic parent: without explicit
        // handling, neither would ever be selected as a root (each is "referenced by someone"),
        // and both would silently vanish from the timeline. This must not happen.
        var groupA = Group("Group-A", ("Group-B", DisplayGroupMemberKind.Group));
        var groupB = Group("Group-B", ("Group-A", DisplayGroupMemberKind.Group));
        var layout = new DisplayGraph("hash-6", [], [groupA, groupB], []);

        var tree = TimelineRowTreeBuilder.Build(layout);

        Assert.Contains(tree.Roots, r => r.Id == TimelineRowId.ForGroup("Group-A"));
        Assert.NotEmpty(tree.Warnings);
    }

    [Fact]
    public void Step017_Acceptance_Passes_UnresolvedGroupMember_StillShownAsVisibleRow_NegativePath()
    {
        var withBrokenRef = Group("Broken-Group", ("Does-Not-Exist", DisplayGroupMemberKind.Unresolved));
        var layout = new DisplayGraph("hash-7", [], [withBrokenRef], []);

        var tree = TimelineRowTreeBuilder.Build(layout);

        var root = Assert.Single(tree.Roots);
        var brokenChild = Assert.Single(root.Children);
        Assert.Equal("Does-Not-Exist", brokenChild.DisplayName);
        Assert.False(brokenChild.IsGroup);
        Assert.Null(brokenChild.ModelId);
    }
}
