using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display.Tests;

/// <summary>
/// PHASE-09 Step 061 -- "Create graphical layout/path editor with position correction." Required
/// acceptance: "Drag and reorder saves across restart." Failure/edge case: "Invalid model/format/version
/// must fail safely without rewriting source files." These tests exercise the real
/// <see cref="ShowLayoutImporter"/>, <see cref="DisplayGraphBuilder"/>, <see cref="DisplayLayoutEditor"/>
/// and <see cref="JsonDisplayLayoutCorrectionsStore"/> (not mocks) -- a "restart" is modeled by
/// constructing brand-new store/editor instances against the same on-disk cache directory.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step061AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    [Fact]
    public async Task Step061_Acceptance_Passes_DragAndReorderSavesAcrossRestart()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step061-{Guid.NewGuid():N}");
        try
        {
            // Session 1: drag Arch-01 to a new position, then reorder the whole list editor.
            var store1 = new JsonDisplayLayoutCorrectionsStore(cacheDirectory);
            var editor1 = new DisplayLayoutEditor(store1);

            var draggedId = new ModelId("Arch-01");
            var afterDrag = await editor1.CorrectPositionAsync(graph, draggedId, newX: 0.9, newY: 0.1, CancellationToken.None);
            Assert.Equal(1, afterDrag.Version);
            Assert.Equal(0.9, Assert.Single(afterDrag.PositionOverrides).X);

            var reversedOrder = graph.Nodes.Select(n => n.Id).Reverse().ToList();
            var afterReorder = await editor1.ReorderAsync(graph, reversedOrder, CancellationToken.None);
            Assert.Equal(2, afterReorder.Version);
            Assert.Equal(reversedOrder, afterReorder.DisplayOrder);
            // The drag from the previous action is still carried forward, not clobbered by the reorder.
            Assert.Single(afterReorder.PositionOverrides, o => o.NodeId == draggedId && o.X == 0.9 && o.Y == 0.1);

            // "Restart": brand-new store/editor instances pointed at the same on-disk directory, and a
            // freshly rebuilt (new VersionId) graph from the same source layout -- nothing in-memory
            // from session 1 is reused.
            var rebuiltGraph = builder.Build(imported, DefaultPolicy);
            Assert.NotEqual(graph.VersionId, rebuiltGraph.VersionId);

            var store2 = new JsonDisplayLayoutCorrectionsStore(cacheDirectory);
            var editor2 = new DisplayLayoutEditor(store2);

            var reloaded = await editor2.GetCorrectionsAsync(rebuiltGraph.SourceLayoutHash, CancellationToken.None);
            Assert.Equal(2, reloaded.Version);
            Assert.Equal(reversedOrder, reloaded.DisplayOrder);
            Assert.Single(reloaded.PositionOverrides, o => o.NodeId == draggedId && o.X == 0.9 && o.Y == 0.1);

            var projected = DisplayLayoutProjection.Apply(rebuiltGraph, reloaded);
            Assert.Equal(reversedOrder, projected.Nodes.Select(n => n.Id));
            var draggedNode = projected.Nodes.Single(n => n.Id == draggedId);
            Assert.Equal(0.9, draggedNode.X);
            Assert.Equal(0.1, draggedNode.Y);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CorrectPositionAsync_UnknownNodeId_FailsSafely_WithoutRewritingSourceFile_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var sourcePath = Path.Combine(directory, ShowDirectoryFileNames.RgbEffects);
        var originalBytes = await File.ReadAllBytesAsync(sourcePath);

        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step061-invalid-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonDisplayLayoutCorrectionsStore(cacheDirectory);
            var editor = new DisplayLayoutEditor(store);

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => editor.CorrectPositionAsync(graph, new ModelId("Does-Not-Exist"), 0.5, 0.5, CancellationToken.None));

            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(sourcePath));
            Assert.False(File.Exists(store.GetPath(graph.SourceLayoutHash)));
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CorrectPositionAsync_OutOfRangeCoordinate_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)])],
            [],
            []);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step061-range-{Guid.NewGuid():N}");
        try
        {
            var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(cacheDirectory));

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => editor.CorrectPositionAsync(graph, new ModelId("A"), newX: 1.5, newY: 0.5, CancellationToken.None));
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => editor.CorrectPositionAsync(graph, new ModelId("A"), newX: double.NaN, newY: 0.5, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReorderAsync_IncompleteOrDuplicateOrForeignOrder_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [
                new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(new ModelId("B"), "B", "Arches", [], [new Point2(1, 0)]),
            ],
            [],
            []);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step061-reorder-{Guid.NewGuid():N}");
        try
        {
            var editor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(cacheDirectory));

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => editor.ReorderAsync(graph, [new ModelId("A")], CancellationToken.None)); // incomplete
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => editor.ReorderAsync(graph, [new ModelId("A"), new ModelId("A")], CancellationToken.None)); // duplicate
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => editor.ReorderAsync(graph, [new ModelId("A"), new ModelId("Ghost")], CancellationToken.None)); // foreign id
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void DisplayLayoutProjection_Apply_MismatchedSourceLayoutHash_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "real-hash",
            [new DisplayModel(new ModelId("A"), "A", "Arches", [], [new Point2(0, 0)])],
            [],
            []);
        var graph = builder.Build(imported, DefaultPolicy);
        var staleCorrections = DisplayLayoutCorrections.Empty("different-hash");

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => DisplayLayoutProjection.Apply(graph, staleCorrections));
    }

    [Fact]
    public async Task Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step061-store-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonDisplayLayoutCorrectionsStore(cacheDirectory);
            await Assert.ThrowsAsync<SpatialDisplayGraphCorruptException>(
                () => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
