using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-09 Step 065 -- "Add route validation, conflicting-write checks and incompatible prop fallback."
/// Required acceptance: "Invalid path cannot silently produce mis-targeted XSQ effects." Fast, deterministic
/// tests for <see cref="DisplayPathEditorViewModel"/>'s own bindable route-validation/locked-node contract
/// (<see cref="DisplayPathEditorViewModel.ValidateRouteAsync"/>/<see cref="DisplayPathEditorViewModel.LockNodeAsync"/>/
/// <see cref="DisplayPathEditorViewModel.UnlockNodeAsync"/>) against a real <see cref="RouteValidationCatalog"/>/
/// <see cref="LockedNodeCatalog"/>/<see cref="JsonRouteValidationReportStore"/>/<see cref="JsonLockedNodeStore"/>
/// (not mocks) under a temporary directory. No real SCREEN 05/12 Avalonia/AXAML view exists (the project's
/// own pre-existing, consistent convention); this step's own required acceptance text is proven here at the
/// bindable view-model layer a real export/XSQ-generation action would gate on.
/// </summary>
public sealed class Step065DisplayPathEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step065-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static readonly ModelId NodeA = new("A");
    private static readonly ModelId NodeB = new("B");
    private static readonly ModelId NodeC = new("C");

    private static SpatialDisplayGraph BuildThreeNodeGraphWithPath(out SpatialPath path)
    {
        var imported = new DisplayGraph(
            "hash-065",
            [
                new DisplayModel(NodeA, "A", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(NodeB, "B", "Arches", [], [new Point2(1, 0)]),
                new DisplayModel(NodeC, "C", "Arches", [], [new Point2(2, 0)]),
            ],
            [],
            []);
        var built = new DisplayGraphBuilder().Build(imported, DefaultPolicy);
        path = new SpatialPath(Guid.NewGuid(), "ABC", [NodeA, NodeB, NodeC], IsClosed: false, Version: 0);
        return built with { Paths = [path] };
    }

    private RouteValidationCatalog BuildRouteValidationCatalog() => new(new JsonRouteValidationReportStore(_cacheDirectory), new RouteValidator());

    private LockedNodeCatalog BuildLockedNodeCatalog() => new(new JsonLockedNodeStore(_cacheDirectory));

    [Fact]
    public async Task ValidateRouteAsync_CleanPath_PopulatesEmptyRouteIssues_NotBlocking()
    {
        var graph = BuildThreeNodeGraphWithPath(out var path);
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), graph);

        var validated = await vm.ValidateRouteAsync(BuildRouteValidationCatalog(), BuildLockedNodeCatalog(), path.Id);

        Assert.True(validated);
        Assert.Null(vm.ActionErrorMessage);
        Assert.NotNull(vm.RouteValidationVersionId);
        Assert.Empty(vm.RouteIssues);
        Assert.False(vm.HasBlockingRouteIssues);
    }

    [Fact]
    public async Task LockNodeAsync_ThenValidateRouteAsync_ReportsBlockingLockedNodeIssue()
    {
        var graph = BuildThreeNodeGraphWithPath(out var path);
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), graph);
        var lockedNodeCatalog = BuildLockedNodeCatalog();

        var locked = await vm.LockNodeAsync(lockedNodeCatalog, NodeB);
        Assert.True(locked);
        Assert.Contains(NodeB, vm.LockedNodeIds);

        var validated = await vm.ValidateRouteAsync(BuildRouteValidationCatalog(), lockedNodeCatalog, path.Id);

        Assert.True(validated);
        Assert.True(vm.HasBlockingRouteIssues);
        Assert.Contains(vm.RouteIssues, i => i.Kind == RouteIssueKind.LockedNode && i.NodeId == NodeB);

        var unlocked = await vm.UnlockNodeAsync(lockedNodeCatalog, NodeB);
        Assert.True(unlocked);
        Assert.DoesNotContain(NodeB, vm.LockedNodeIds);
    }

    [Fact]
    public async Task OpenAsync_ReopeningADifferentLayout_ClearsPreviousRouteValidationAndLockedNodes()
    {
        var graph = BuildThreeNodeGraphWithPath(out var path);
        var vm = new DisplayPathEditorViewModel();
        var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));
        await vm.OpenAsync(editor, graph);
        var lockedNodeCatalog = BuildLockedNodeCatalog();
        await vm.LockNodeAsync(lockedNodeCatalog, NodeA);
        await vm.ValidateRouteAsync(BuildRouteValidationCatalog(), lockedNodeCatalog, path.Id);
        Assert.NotEmpty(vm.LockedNodeIds);
        Assert.True(vm.HasBlockingRouteIssues);

        var otherGraph = BuildThreeNodeGraphWithPath(out _) with { SourceLayoutHash = "hash-065-other" };
        await vm.OpenAsync(editor, otherGraph);

        Assert.Empty(vm.LockedNodeIds);
        Assert.Null(vm.LockedNodeSetVersion);
        Assert.Empty(vm.RouteIssues);
        Assert.False(vm.HasBlockingRouteIssues);
        Assert.Null(vm.RouteValidationVersionId);
    }

    [Fact]
    public async Task ValidateRouteAsync_NothingOpen_SetsActionErrorAndLeavesEmpty_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();

        var validated = await vm.ValidateRouteAsync(BuildRouteValidationCatalog(), BuildLockedNodeCatalog(), Guid.NewGuid());

        Assert.False(validated);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.RouteIssues);
        Assert.False(vm.HasBlockingRouteIssues);
    }

    [Fact]
    public async Task ValidateRouteAsync_UnknownPathId_RejectsWithoutPartialMutation_NegativePath()
    {
        var graph = BuildThreeNodeGraphWithPath(out _);
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), graph);

        var validated = await vm.ValidateRouteAsync(BuildRouteValidationCatalog(), BuildLockedNodeCatalog(), Guid.NewGuid());

        Assert.False(validated);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.RouteIssues);
    }

    [Fact]
    public async Task LockNodeAsync_UnknownNode_RejectsWithoutPartialMutation_NegativePath()
    {
        var graph = BuildThreeNodeGraphWithPath(out _);
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), graph);

        var locked = await vm.LockNodeAsync(BuildLockedNodeCatalog(), new ModelId("Does-Not-Exist"));

        Assert.False(locked);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.LockedNodeIds);
    }
}
