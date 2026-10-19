using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-09 Step 063 -- "Persist role tags: lead, rhythm, ambient, hero and accent." Required acceptance:
/// "Role assignments are available in choreography UI." Fast, deterministic tests for
/// <see cref="DisplayPathEditorViewModel"/>'s own bindable role-assignment contract
/// (<see cref="DisplayPathEditorViewModel.LoadRoleAssignmentsAsync"/>/<see cref="DisplayPathEditorViewModel.AssignRoleAsync"/>/
/// <see cref="DisplayPathEditorViewModel.ClearRoleAsync"/>) against a real
/// <see cref="RoleAssignmentCatalog"/>/<see cref="JsonRoleAssignmentStore"/> (not mocks) under a temporary
/// directory. No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists (the project's own pre-existing,
/// consistent convention -- see <c>implementation-evidence/PHASE-09/STEP-063.md</c> §7); this step's own
/// required acceptance text is proven at the bindable view-model layer the real choreography UI would bind
/// to, the same precedent Step 059-062's own evidence already established for SCREEN 05.
/// </summary>
public sealed class Step063DisplayPathEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step063-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static SpatialDisplayGraph BuildGraph()
    {
        var imported = new DisplayGraph(
            "hash-063",
            [
                new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(new ModelId("B"), "B", "Arches", [], [new Point2(1, 0)]),
            ],
            [],
            []);
        return new DisplayGraphBuilder().Build(imported, DefaultPolicy);
    }

    private RoleAssignmentCatalog BuildCatalog() => new(new JsonRoleAssignmentStore(_cacheDirectory));

    private static readonly ModelId NodeA = new("A");

    [Fact]
    public async Task LoadRoleAssignmentsAsync_NothingSavedYet_ReturnsEmptyRoleAssignments()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());

        var loaded = await vm.LoadRoleAssignmentsAsync(BuildCatalog());

        Assert.True(loaded);
        Assert.Empty(vm.RoleAssignments);
        Assert.Equal(0, vm.RoleAssignmentSetVersion);
    }

    [Fact]
    public async Task AssignRoleAsync_SamePropDifferentScopes_BothAvailableInChoreographyUi()
    {
        // The required acceptance text, verbatim: "Role assignments are available in choreography UI," via
        // the implementation guidance's own worked example: "same prop can play lead in one phrase and
        // background later."
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());
        var catalog = BuildCatalog();
        var phraseA = Guid.NewGuid();
        var phraseB = Guid.NewGuid();

        var assignedLead = await vm.AssignRoleAsync(catalog, NodeA, RoleTag.Lead, phraseA);
        Assert.True(assignedLead);
        Assert.Null(vm.ActionErrorMessage);
        Assert.Equal(1, vm.RoleAssignmentSetVersion);

        var assignedAmbient = await vm.AssignRoleAsync(catalog, NodeA, RoleTag.Ambient, phraseB);
        Assert.True(assignedAmbient);
        Assert.Equal(2, vm.RoleAssignmentSetVersion);

        Assert.Equal(2, vm.RoleAssignments.Count);
        Assert.Equal(RoleTag.Lead, vm.RoleAssignments.Single(a => a.ScopeId == phraseA).Role);
        Assert.Equal(RoleTag.Ambient, vm.RoleAssignments.Single(a => a.ScopeId == phraseB).Role);
    }

    [Fact]
    public async Task ClearRoleAsync_RemovesAssignmentFromRoleAssignments()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());
        var catalog = BuildCatalog();
        var scope = Guid.NewGuid();
        await vm.AssignRoleAsync(catalog, NodeA, RoleTag.Hero, scope);

        var cleared = await vm.ClearRoleAsync(catalog, NodeA, scope);

        Assert.True(cleared);
        Assert.Null(vm.ActionErrorMessage);
        Assert.Empty(vm.RoleAssignments);
    }

    [Fact]
    public async Task AssignRoleAsync_NothingOpen_SetsActionErrorAndLeavesRoleAssignmentsEmpty_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();

        var assigned = await vm.AssignRoleAsync(BuildCatalog(), NodeA, RoleTag.Lead, Guid.NewGuid());

        Assert.False(assigned);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.RoleAssignments);
    }

    [Fact]
    public async Task AssignRoleAsync_UnrecognizedRole_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());
        var catalog = BuildCatalog();
        var scope = Guid.NewGuid();
        await vm.AssignRoleAsync(catalog, NodeA, RoleTag.Lead, scope);
        var before = vm.RoleAssignments;

        var assigned = await vm.AssignRoleAsync(catalog, NodeA, "Soloist", scope);

        Assert.False(assigned);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(before.Select(a => (a.NodeId, a.Role, a.ScopeId, a.Version)), vm.RoleAssignments.Select(a => (a.NodeId, a.Role, a.ScopeId, a.Version)));
    }

    [Fact]
    public async Task ClearRoleAsync_UnknownPair_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());
        var catalog = BuildCatalog();
        await vm.LoadRoleAssignmentsAsync(catalog);

        var cleared = await vm.ClearRoleAsync(catalog, NodeA, Guid.NewGuid());

        Assert.False(cleared);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.RoleAssignments);
    }
}
