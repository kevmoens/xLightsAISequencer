using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-09 Step 062 -- "Define named custom paths and semantic spatial motions," SCREEN 05 DISPLAY
/// PATHS. Fast, deterministic tests for <see cref="DisplayPathEditorViewModel"/>'s own bindable named-path
/// contract (<see cref="DisplayPathEditorViewModel.LoadPathsAsync"/>/<see cref="DisplayPathEditorViewModel.CreatePathAsync"/>/
/// <see cref="DisplayPathEditorViewModel.ReorderPathAsync"/>/<see cref="DisplayPathEditorViewModel.ReversePathAsync"/>)
/// against a real <see cref="SpatialPathCatalog"/>/<see cref="JsonSpatialPathStore"/> (not mocks) under a
/// temporary directory.
/// </summary>
public sealed class Step062DisplayPathEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step062-vm-{Guid.NewGuid():N}");

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
            "hash-062",
            [
                new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(new ModelId("B"), "B", "Arches", [], [new Point2(1, 0)]),
                new DisplayModel(new ModelId("C"), "C", "Arches", [], [new Point2(2, 0)]),
            ],
            [],
            []);
        return new DisplayGraphBuilder().Build(imported, DefaultPolicy);
    }

    private SpatialPathCatalog BuildCatalog() =>
        new(new JsonSpatialPathStore(_cacheDirectory), new SpatialPathSuggester(), new SpatialPathEditor());

    private static readonly ModelId[] AllNodes = [new("A"), new("B"), new("C")];

    [Fact]
    public async Task LoadPathsAsync_NothingSavedYet_ReturnsEmptyNamedPaths()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());

        var loaded = await vm.LoadPathsAsync(BuildCatalog());

        Assert.True(loaded);
        Assert.Empty(vm.NamedPaths);
        Assert.Equal(0, vm.PathSetVersion);
    }

    [Fact]
    public async Task CreatePathAsync_ReorderPathAsync_ReversePathAsync_UpdateNamedPaths()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());
        var catalog = BuildCatalog();

        var created = await vm.CreatePathAsync(catalog, "Arch Route", AllNodes, DirectionPolicy.LeftToRight, isClosed: false, allowReplaceExisting: false);
        Assert.True(created);
        Assert.Null(vm.ActionErrorMessage);
        var path = Assert.Single(vm.NamedPaths);
        Assert.Equal(AllNodes, path.OrderedModels);
        Assert.Equal(1, vm.PathSetVersion);

        var reversed = await vm.ReversePathAsync(catalog, path.Id);
        Assert.True(reversed);
        Assert.Equal(AllNodes.Reverse(), Assert.Single(vm.NamedPaths).OrderedModels);
        Assert.Equal(2, vm.PathSetVersion);

        var manualOrder = new[] { AllNodes[1], AllNodes[0], AllNodes[2] };
        var reordered = await vm.ReorderPathAsync(catalog, path.Id, manualOrder);
        Assert.True(reordered);
        Assert.Equal(manualOrder, Assert.Single(vm.NamedPaths).OrderedModels);
        Assert.Equal(3, vm.PathSetVersion);
    }

    [Fact]
    public async Task CreatePathAsync_NothingOpen_SetsActionErrorAndLeavesNamedPathsEmpty_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();

        var created = await vm.CreatePathAsync(BuildCatalog(), "Arch Route", AllNodes, DirectionPolicy.LeftToRight, isClosed: false, allowReplaceExisting: false);

        Assert.False(created);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.NamedPaths);
    }

    [Fact]
    public async Task CreatePathAsync_DuplicateNameWithoutReplace_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());
        var catalog = BuildCatalog();
        await vm.CreatePathAsync(catalog, "Arch Route", AllNodes, DirectionPolicy.LeftToRight, isClosed: false, allowReplaceExisting: false);
        var pathsBefore = vm.NamedPaths;

        var created = await vm.CreatePathAsync(catalog, "Arch Route", AllNodes, DirectionPolicy.RightToLeft, isClosed: false, allowReplaceExisting: false);

        Assert.False(created);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(pathsBefore.Select(p => (p.Id, p.Version, p.OrderedModels)), vm.NamedPaths.Select(p => (p.Id, p.Version, p.OrderedModels)));
    }

    [Fact]
    public async Task ReorderPathAsync_UnknownPathId_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory)), BuildGraph());
        var catalog = BuildCatalog();
        await vm.LoadPathsAsync(catalog);

        var reordered = await vm.ReorderPathAsync(catalog, Guid.NewGuid(), AllNodes);

        Assert.False(reordered);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.NamedPaths);
    }
}
