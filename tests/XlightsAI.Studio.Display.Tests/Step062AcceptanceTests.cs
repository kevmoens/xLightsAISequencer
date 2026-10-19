using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display.Tests;

/// <summary>
/// PHASE-09 Step 062 -- "Define named custom paths and semantic spatial motions." Required acceptance:
/// "Left-right, outside-in and vertical previews match ordered props." Failure/edge case: "Invalid
/// input, cancellation or stale persisted state must not corrupt the project." These tests exercise the
/// real <see cref="ShowLayoutImporter"/>/<see cref="DisplayGraphBuilder"/>/<see cref="SpatialPathSuggester"/>/
/// <see cref="SpatialPathEditor"/>/<see cref="SpatialPathCatalog"/>/<see cref="JsonSpatialPathStore"/>
/// (not mocks) against the same Step 059 golden fixture (five arches at WorldPosX 0,10,20,30,40; a
/// three-segment roofline at WorldPosX 5,20,35, WorldPosY 8).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step062AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static readonly ModelId[] Arches =
    [
        new("Arch-01"), new("Arch-02"), new("Arch-03"), new("Arch-04"), new("Arch-05"),
    ];

    [Fact]
    public async Task Step062_Acceptance_Passes_LeftRightOutsideInAndVerticalPreviewsMatchOrderedProps()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var suggester = new SpatialPathSuggester();

        // Left-right: ascending WorldPosX (0,10,20,30,40) -> Arch-01..Arch-05, unchanged order.
        var leftToRight = suggester.Suggest(graph, "Arch Left-Right", Arches, DirectionPolicy.LeftToRight, isClosed: false);
        Assert.Equal(Arches, leftToRight.OrderedModels);

        // Outside-in: leftmost, rightmost, second-leftmost, second-rightmost, middle.
        var outsideIn = suggester.Suggest(graph, "Arch Outside-In", Arches, DirectionPolicy.OutsideIn, isClosed: false);
        Assert.Equal(
            [new ModelId("Arch-01"), new ModelId("Arch-05"), new ModelId("Arch-02"), new ModelId("Arch-04"), new ModelId("Arch-03")],
            outsideIn.OrderedModels);

        // Vertical (top-to-bottom): Arch-03 (WorldPosY 0) sits above Roof-02 (WorldPosY 8, same WorldPosX 20).
        var verticalPair = new[] { new ModelId("Roof-02"), new ModelId("Arch-03") };
        var topToBottom = suggester.Suggest(graph, "Column Top-Bottom", verticalPair, DirectionPolicy.TopToBottom, isClosed: false);
        Assert.Equal([new ModelId("Arch-03"), new ModelId("Roof-02")], topToBottom.OrderedModels);

        // Every suggestion starts at Version 0 with a fresh, distinct identity -- not yet persisted.
        Assert.Equal(0, leftToRight.Version);
        Assert.Equal(0, outsideIn.Version);
        Assert.Equal(0, topToBottom.Version);
        Assert.NotEqual(leftToRight.Id, outsideIn.Id);
    }

    [Fact]
    public async Task CreateAsync_ReorderAsync_ReverseAsync_PersistVersionedHistoryAcrossRestart()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step062-{Guid.NewGuid():N}");
        try
        {
            var catalog1 = new SpatialPathCatalog(new JsonSpatialPathStore(cacheDirectory), new SpatialPathSuggester(), new SpatialPathEditor());

            var created = await catalog1.CreateAsync(graph, "Arch Route", Arches, DirectionPolicy.LeftToRight, isClosed: false, allowReplaceExisting: false, CancellationToken.None);
            Assert.Equal(1, created.Version);
            var archRoute = Assert.Single(created.Paths);
            Assert.Equal(0, archRoute.Version);
            Assert.Equal(Arches, archRoute.OrderedModels);
            Assert.Empty(created.History);

            var reversed = await catalog1.ReverseAsync(graph, archRoute.Id, CancellationToken.None);
            Assert.Equal(2, reversed.Version);
            var reversedRoute = Assert.Single(reversed.Paths);
            Assert.Equal(1, reversedRoute.Version);
            Assert.Equal(reversedRoute.Id, archRoute.Id);
            Assert.Equal(Arches.Reverse(), reversedRoute.OrderedModels);
            // The pre-reverse version is now in history, versioned, not lost.
            Assert.Single(reversed.History, h => h.Id == archRoute.Id && h.Version == 0);

            var manualOrder = new[] { Arches[2], Arches[0], Arches[4], Arches[1], Arches[3] };
            var reordered = await catalog1.ReorderAsync(graph, archRoute.Id, manualOrder, CancellationToken.None);
            Assert.Equal(3, reordered.Version);
            var reorderedRoute = Assert.Single(reordered.Paths);
            Assert.Equal(2, reorderedRoute.Version);
            Assert.Equal(manualOrder, reorderedRoute.OrderedModels);
            Assert.Equal(2, reordered.History.Count);

            // "Restart": brand-new store/catalog instance pointed at the same on-disk directory.
            var catalog2 = new SpatialPathCatalog(new JsonSpatialPathStore(cacheDirectory), new SpatialPathSuggester(), new SpatialPathEditor());
            var reloaded = await catalog2.GetPathsAsync(graph.SourceLayoutHash, CancellationToken.None);
            Assert.Equal(3, reloaded.Version);
            var reloadedRoute = Assert.Single(reloaded.Paths);
            Assert.Equal(2, reloadedRoute.Version);
            Assert.Equal(manualOrder, reloadedRoute.OrderedModels);
            Assert.Equal(2, reloaded.History.Count);
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
    public async Task CreateAsync_DuplicateName_RejectedUnlessAllowReplaceExisting_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step062-dup-{Guid.NewGuid():N}");
        try
        {
            var catalog = new SpatialPathCatalog(new JsonSpatialPathStore(cacheDirectory), new SpatialPathSuggester(), new SpatialPathEditor());
            var first = await catalog.CreateAsync(graph, "Arch Route", Arches, DirectionPolicy.LeftToRight, isClosed: false, allowReplaceExisting: false, CancellationToken.None);
            var firstPathId = Assert.Single(first.Paths).Id;

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.CreateAsync(graph, "Arch Route", Arches, DirectionPolicy.RightToLeft, isClosed: false, allowReplaceExisting: false, CancellationToken.None));

            // Rejected attempt never touched the persisted set.
            var unchanged = await catalog.GetPathsAsync(graph.SourceLayoutHash, CancellationToken.None);
            Assert.Equal(1, unchanged.Version);
            Assert.Equal(Arches, Assert.Single(unchanged.Paths).OrderedModels);

            var replaced = await catalog.CreateAsync(graph, "Arch Route", Arches, DirectionPolicy.RightToLeft, isClosed: false, allowReplaceExisting: true, CancellationToken.None);
            var replacedPath = Assert.Single(replaced.Paths);
            Assert.Equal(firstPathId, replacedPath.Id); // same identity, new version -- a replace, not a fresh path.
            Assert.Equal(1, replacedPath.Version);
            Assert.Equal(Arches.Reverse(), replacedPath.OrderedModels);
            Assert.Single(replaced.History, h => h.Id == firstPathId && h.Version == 0);
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
    public async Task Suggest_InvalidInput_RejectsBeforeAnyPathIsBuilt_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);
        var suggester = new SpatialPathSuggester();

        await Task.CompletedTask; // keep this test's own async shape consistent with its siblings

        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            suggester.Suggest(graph, "Empty", [], DirectionPolicy.LeftToRight, isClosed: false)); // no nodes

        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            suggester.Suggest(graph, "Foreign", [new ModelId("Does-Not-Exist")], DirectionPolicy.LeftToRight, isClosed: false)); // unknown node

        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            suggester.Suggest(graph, "Dup", [new ModelId("Arch-01"), new ModelId("Arch-01")], DirectionPolicy.LeftToRight, isClosed: false)); // duplicate

        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            suggester.Suggest(graph, "BadPolicy", Arches, "Diagonal", isClosed: false)); // unsupported policy

        var noGeometryGraph = builder.Build(
            new DisplayGraph("hash", [new DisplayModel(new ModelId("NoGeo"), "NoGeo", "Arches", [], [])], [], []),
            DefaultPolicy);
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            suggester.Suggest(noGeometryGraph, "NoGeometry", [new ModelId("NoGeo")], DirectionPolicy.LeftToRight, isClosed: false)); // unresolvable geometry
    }

    [Fact]
    public void SpatialPathEditor_Reorder_NonPermutation_Throws_Invalid_NegativePath()
    {
        var editor = new SpatialPathEditor();
        var path = new SpatialPath(Guid.NewGuid(), "P", Arches, IsClosed: false, Version: 0);

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => editor.Reorder(path, [Arches[0]])); // incomplete
        Assert.Throws<SpatialDisplayGraphInvalidException>(() => editor.Reorder(path, [.. Arches, Arches[0]])); // duplicate
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            editor.Reorder(path, [Arches[0], Arches[1], Arches[2], Arches[3], new ModelId("Ghost")])); // foreign id
    }

    [Fact]
    public async Task ReorderAsync_ReverseAsync_UnknownPathId_Throws_Invalid_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step062-unknown-{Guid.NewGuid():N}");
        try
        {
            var catalog = new SpatialPathCatalog(new JsonSpatialPathStore(cacheDirectory), new SpatialPathSuggester(), new SpatialPathEditor());
            var unknownId = Guid.NewGuid();

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => catalog.ReorderAsync(graph, unknownId, Arches, CancellationToken.None));
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(
                () => catalog.ReverseAsync(graph, unknownId, CancellationToken.None));

            Assert.False(File.Exists(new JsonSpatialPathStore(cacheDirectory).GetPath(graph.SourceLayoutHash)));
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
    public async Task CreateAsync_Cancelled_DoesNotPersist_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step062-cancel-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonSpatialPathStore(cacheDirectory);
            var catalog = new SpatialPathCatalog(store, new SpatialPathSuggester(), new SpatialPathEditor());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                catalog.CreateAsync(graph, "Arch Route", Arches, DirectionPolicy.LeftToRight, isClosed: false, allowReplaceExisting: false, cts.Token));

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
    public async Task Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step062-store-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonSpatialPathStore(cacheDirectory);
            await Assert.ThrowsAsync<SpatialDisplayGraphCorruptException>(
                () => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
