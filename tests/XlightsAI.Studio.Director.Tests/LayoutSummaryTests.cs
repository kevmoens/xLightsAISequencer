using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// User feedback (2026-11-14): <see cref="LayoutSummary.HasTreeOrMatrixModel"/> and
/// <see cref="LayoutSummary.HasMultiModelGroupWithKnownPositions"/> -- the real facts
/// <c>DirectorPromptBuilder</c>'s new condition-tagged lesson system evaluates against.
/// </summary>
public sealed class LayoutSummaryTests
{
    private static DisplayGraph BuildLayout(IReadOnlyList<DisplayModel> models, IReadOnlyList<DisplayModelGroup> groups) =>
        new("TEST-HASH", models, groups, Warnings: []);

    [Fact]
    public void Build_LayoutWithTreeModel_HasTreeOrMatrixModelIsTrue()
    {
        var layout = BuildLayout(
            [new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 800)],
            []);

        Assert.True(LayoutSummary.Build(layout).HasTreeOrMatrixModel);
    }

    [Fact]
    public void Build_LayoutWithNoTreeOrMatrixModel_HasTreeOrMatrixModelIsFalse()
    {
        var layout = BuildLayout(
            [new DisplayModel(new ModelId("Arch1"), "Arch1", "Arches", [], [], NodeCount: 50)],
            []);

        Assert.False(LayoutSummary.Build(layout).HasTreeOrMatrixModel);
    }

    [Fact]
    public void Build_GroupWithThreePositionedMembers_HasMultiModelGroupWithKnownPositionsIsTrue()
    {
        var models = new[]
        {
            new DisplayModel(new ModelId("Arch1"), "Arch1", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(100, 0, 0)),
            new DisplayModel(new ModelId("Arch2"), "Arch2", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(200, 0, 0)),
            new DisplayModel(new ModelId("Arch3"), "Arch3", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(300, 0, 0)),
        };
        var groups = new[]
        {
            new DisplayModelGroup("Arches",
            [
                new DisplayGroupMember("Arch1", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch2", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch3", DisplayGroupMemberKind.Model),
            ]),
        };

        Assert.True(LayoutSummary.Build(BuildLayout(models, groups)).HasMultiModelGroupWithKnownPositions);
    }

    [Fact]
    public void Build_GroupWithOnlyTwoPositionedMembers_HasMultiModelGroupWithKnownPositionsIsFalse()
    {
        var models = new[]
        {
            new DisplayModel(new ModelId("Arch1"), "Arch1", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(100, 0, 0)),
            new DisplayModel(new ModelId("Arch2"), "Arch2", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(200, 0, 0)),
        };
        var groups = new[]
        {
            new DisplayModelGroup("Arches",
            [
                new DisplayGroupMember("Arch1", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch2", DisplayGroupMemberKind.Model),
            ]),
        };

        Assert.False(LayoutSummary.Build(BuildLayout(models, groups)).HasMultiModelGroupWithKnownPositions);
    }

    [Fact]
    public void Build_GroupWithThreeMembersButNoKnownPositions_HasMultiModelGroupWithKnownPositionsIsFalse()
    {
        // Three real members, but none has a real Bounds (never positioned in the xLights 3D preview) --
        // a Look.Travel sweep ordering by physical position would have nothing real to sort by.
        var models = new[]
        {
            new DisplayModel(new ModelId("Arch1"), "Arch1", "Arches", [], [], NodeCount: 50),
            new DisplayModel(new ModelId("Arch2"), "Arch2", "Arches", [], [], NodeCount: 50),
            new DisplayModel(new ModelId("Arch3"), "Arch3", "Arches", [], [], NodeCount: 50),
        };
        var groups = new[]
        {
            new DisplayModelGroup("Arches",
            [
                new DisplayGroupMember("Arch1", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch2", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch3", DisplayGroupMemberKind.Model),
            ]),
        };

        Assert.False(LayoutSummary.Build(BuildLayout(models, groups)).HasMultiModelGroupWithKnownPositions);
    }
}
