using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-09 Step 061 -- SCREEN 05 DISPLAY PATHS. Fast, deterministic tests for
/// <see cref="DisplayPathEditorViewModel"/>'s own bindable rendering/correction contract against a real
/// <see cref="DisplayLayoutEditor"/>/<see cref="JsonDisplayLayoutCorrectionsStore"/> (not mocks) under a
/// temporary directory.
/// </summary>
public sealed class Step061DisplayPathEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step061-vm-{Guid.NewGuid():N}");

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
            "hash-061",
            [
                new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(new ModelId("B"), "B", "Arches", [], [new Point2(1, 0)]),
                new DisplayModel(new ModelId("C"), "C", "Arches", [], [new Point2(2, 0)]),
            ],
            [],
            []);
        return new DisplayGraphBuilder().Build(imported, DefaultPolicy);
    }

    [Fact]
    public async Task OpenAsync_NoCorrectionsYet_RendersRowsInGraphOrder()
    {
        var graph = BuildGraph();
        var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));

        var vm = new DisplayPathEditorViewModel();
        Assert.True(vm.IsEmpty);

        var opened = await vm.OpenAsync(editor, graph);

        Assert.True(opened);
        Assert.True(vm.IsLoaded);
        Assert.Equal(0, vm.Version);
        Assert.Equal(["A", "B", "C"], vm.Rows.Select(r => r.NodeId.Value));
    }

    [Fact]
    public async Task CorrectSelectedNodePositionAsync_ThenReorderAsync_PersistsAcrossRestart()
    {
        var graph = BuildGraph();
        var editor1 = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));

        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(editor1, graph);
        vm.SelectNode(new ModelId("B"));

        var dragSucceeded = await vm.CorrectSelectedNodePositionAsync(editor1, newX: 0.75, newY: 0.25);
        Assert.True(dragSucceeded);
        Assert.Null(vm.ActionErrorMessage);
        Assert.Equal(1, vm.Version);
        var draggedRow = vm.Rows.Single(r => r.NodeId.Value == "B");
        Assert.Equal(0.75, draggedRow.X);
        Assert.Equal(0.25, draggedRow.Y);

        var newOrder = new[] { new ModelId("C"), new ModelId("A"), new ModelId("B") };
        var reorderSucceeded = await vm.ReorderAsync(editor1, newOrder);
        Assert.True(reorderSucceeded);
        Assert.Equal(2, vm.Version);
        Assert.Equal(["C", "A", "B"], vm.Rows.Select(r => r.NodeId.Value));
        // The earlier drag correction is still applied after the reorder.
        Assert.Equal(0.75, vm.Rows.Single(r => r.NodeId.Value == "B").X);

        // "Restart": brand-new view model + editor/store instances over the same on-disk directory and
        // a freshly rebuilt graph.
        var rebuiltGraph = BuildGraph();
        var editor2 = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));
        var reopened = new DisplayPathEditorViewModel();

        var opened = await reopened.OpenAsync(editor2, rebuiltGraph);

        Assert.True(opened);
        Assert.Equal(2, reopened.Version);
        Assert.Equal(["C", "A", "B"], reopened.Rows.Select(r => r.NodeId.Value));
        Assert.Equal(0.75, reopened.Rows.Single(r => r.NodeId.Value == "B").X);
    }

    [Fact]
    public async Task CorrectSelectedNodePositionAsync_NothingSelected_SetsActionErrorAndLeavesRowsUnchanged_NegativePath()
    {
        var graph = BuildGraph();
        var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(editor, graph);
        var rowsBefore = vm.Rows;

        var succeeded = await vm.CorrectSelectedNodePositionAsync(editor, 0.5, 0.5);

        Assert.False(succeeded);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(rowsBefore.Select(r => (r.NodeId, r.X, r.Y)), vm.Rows.Select(r => (r.NodeId, r.X, r.Y)));
        Assert.Equal(0, vm.Version);
    }

    [Fact]
    public async Task CorrectSelectedNodePositionAsync_OutOfRange_RejectsWithoutPartialMutation_NegativePath()
    {
        var graph = BuildGraph();
        var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(editor, graph);
        vm.SelectNode(new ModelId("A"));

        var succeeded = await vm.CorrectSelectedNodePositionAsync(editor, newX: 2.0, newY: 0.5);

        Assert.False(succeeded);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(0, vm.Version);
        Assert.Equal(0.0, vm.Rows.Single(r => r.NodeId.Value == "A").X);
    }

    [Fact]
    public async Task ReorderAsync_InvalidOrder_RejectsWithoutPartialMutation_NegativePath()
    {
        var graph = BuildGraph();
        var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(_cacheDirectory));
        var vm = new DisplayPathEditorViewModel();
        await vm.OpenAsync(editor, graph);

        var succeeded = await vm.ReorderAsync(editor, [new ModelId("A"), new ModelId("B")]); // missing C

        Assert.False(succeeded);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(0, vm.Version);
        Assert.Equal(["A", "B", "C"], vm.Rows.Select(r => r.NodeId.Value));
    }

    [Fact]
    public void ShellViewModel_DisplayPathEditorScreen_NavigatesAndStartsEmpty()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();

        shell.NavigateToDisplayPathEditorCommand.Execute(null);

        Assert.True(shell.IsShowingDisplayPathEditor);
        Assert.True(shell.DisplayPathEditor.IsEmpty);
    }
}
