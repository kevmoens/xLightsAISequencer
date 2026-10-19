using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display.Tests;

/// <summary>
/// PHASE-09 Step 059 -- "Normalize coordinates, model groups and relationships into display graph."
/// Required acceptance: "Fixtures map consistent physical positions and stable IDs." Failure/edge case:
/// "Invalid model/format/version must fail safely without rewriting source files." These tests exercise
/// the real <see cref="ShowLayoutImporter"/> and the real <see cref="DisplayGraphBuilder"/>/
/// <see cref="JsonSpatialDisplayGraphStore"/> (not mocks) and assert the persisted/returned domain state
/// itself, not merely that a call happened.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step059AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    [Fact]
    public async Task Step059_Acceptance_Passes_FixturesMapConsistentPositionsAndStableIds()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");

        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        var first = builder.Build(imported, DefaultPolicy);
        var second = builder.Build(imported, DefaultPolicy);

        // Stable IDs and consistent physical positions: rebuilding from the same imported snapshot
        // twice produces identical nodes/edges (only VersionId, this run's own provenance stamp, differs).
        Assert.NotEqual(first.VersionId, second.VersionId);
        Assert.Equal(
            first.Nodes.Select(n => (n.Id, n.XlightsName, n.X, n.Y, n.ModelKind)),
            second.Nodes.Select(n => (n.Id, n.XlightsName, n.X, n.Y, n.ModelKind)));
        Assert.Equal(first.Edges, second.Edges);

        // Five arches map to five distinct, consistently-ordered X positions (normalized left-to-right).
        var archIds = new[] { "Arch-01", "Arch-02", "Arch-03", "Arch-04", "Arch-05" };
        var archNodes = archIds.Select(name => Assert.Single(first.Nodes, n => n.XlightsName == name)).ToList();
        Assert.Equal(archNodes.Select(n => n.X).Order(), archNodes.Select(n => n.X));
        Assert.Equal(0.0, archNodes[0].X);
        Assert.Equal(1.0, archNodes[^1].X);
        Assert.All(archNodes, n => Assert.Equal("Arches", n.ModelKind));
        Assert.All(archNodes, n => Assert.Empty(n.Tags));

        // Every node id is stable/addressable by the model's own ModelId.
        Assert.Equal(new ModelId("Arch-01"), archNodes[0].Id);

        // Group nodes are projected with a centroid derived only from their resolvable Model members.
        var archesGroup = Assert.Single(first.Nodes, n => n.XlightsName == "Arches" && n.ModelKind == "Group");
        Assert.Equal(archNodes.Average(n => n.X), archesGroup.X, precision: 10);
        Assert.Equal(archNodes.Average(n => n.Y), archesGroup.Y, precision: 10);

        var roofNodes = new[] { "Roof-01", "Roof-02", "Roof-03" }
            .Select(name => Assert.Single(first.Nodes, n => n.XlightsName == name)).ToList();
        var roofGroup = Assert.Single(first.Nodes, n => n.XlightsName == "Roofline" && n.ModelKind == "Group");
        Assert.Equal(roofNodes.Average(n => n.X), roofGroup.X, precision: 10);

        // All-Props nests both groups and references an unresolved name and a submodel shape; only the
        // two resolvable Group members ever produce an edge.
        var allProps = Assert.Single(first.Nodes, n => n.XlightsName == "All-Props" && n.ModelKind == "Group");
        Assert.Contains(first.Edges, e => e.From == archesGroup.Id && e.To == allProps.Id && e.Relation == "NestedGroup");
        Assert.Contains(first.Edges, e => e.From == roofGroup.Id && e.To == allProps.Id && e.Relation == "NestedGroup");
        Assert.DoesNotContain(first.Edges, e => e.To == allProps.Id && e.Relation == "GroupMember");

        // Direct group-membership edges: five Arches + three Roofline = eight, plus the two nested-group
        // edges above = ten total. Nothing is produced for "Ghost-Prop" (unresolved) or the submodel ref.
        Assert.Equal(10, first.Edges.Count);
        Assert.Equal(5, first.Edges.Count(e => e.To == archesGroup.Id && e.Relation == "GroupMember"));
        Assert.Equal(3, first.Edges.Count(e => e.To == roofGroup.Id && e.Relation == "GroupMember"));
        Assert.All(first.Edges, e => Assert.False(e.UserDefined));

        Assert.Empty(first.Paths);
        Assert.Equal(imported.SourceLayoutHash, first.SourceLayoutHash);
    }

    /// <summary>
    /// Failure/edge case: an unusable <see cref="GeometryPolicy"/> is rejected before any node/edge is
    /// produced, and the real on-disk source layout file is never touched.
    /// </summary>
    [Fact]
    public async Task Step059_Negative_InvalidGeometryPolicy_FailsSafely_WithoutRewritingSourceFile()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var sourcePath = Path.Combine(directory, ShowDirectoryFileNames.RgbEffects);
        var originalBytes = await File.ReadAllBytesAsync(sourcePath);

        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var invalidPolicy = new GeometryPolicy(MaxNeighborDistance: -1, RespectGroups: true);

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => builder.Build(imported, invalidPolicy));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(sourcePath));
    }

    [Fact]
    public void DuplicateModelName_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [
                new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", [], [new Point2(0, 0)]),
                new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", [], [new Point2(1, 1)]),
            ],
            [],
            []);

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => builder.Build(imported, DefaultPolicy));
    }

    [Fact]
    public void GroupNameCollidesWithModelId_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [new DisplayModel(new ModelId("Arches"), "Arches", "Arches", ["Arches"], [new Point2(0, 0)])],
            [new DisplayModelGroup("Arches", [new DisplayGroupMember("Arches", DisplayGroupMemberKind.Model)])],
            []);

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => builder.Build(imported, DefaultPolicy));
    }

    /// <summary>
    /// Step 059's own "controlled alias resolution for changing xLights model names": a model renamed
    /// in a fresh import still resolves to its prior, stable node id when an explicit alias is supplied.
    /// </summary>
    [Fact]
    public void AliasResolution_PinsRenamedModelToPriorStableId()
    {
        var builder = new DisplayGraphBuilder();
        var original = new DisplayGraph(
            "hash-1",
            [new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", [], [new Point2(0, 0)])],
            [],
            []);
        var renamed = new DisplayGraph(
            "hash-2",
            [new DisplayModel(new ModelId("Arch-01-Renamed"), "Arch-01-Renamed", "Arches", [], [new Point2(0, 0)])],
            [],
            []);

        var before = builder.Build(original, DefaultPolicy);
        var originalNodeId = Assert.Single(before.Nodes).Id;

        var withoutAlias = builder.Build(renamed, DefaultPolicy);
        Assert.NotEqual(originalNodeId, Assert.Single(withoutAlias.Nodes).Id);

        var alias = new NodeAlias(originalNodeId, "Arch-01-Renamed");
        var withAlias = builder.Build(renamed, DefaultPolicy, [alias]);
        var renamedNode = Assert.Single(withAlias.Nodes);
        Assert.Equal(originalNodeId, renamedNode.Id);
        Assert.Equal("Arch-01-Renamed", renamedNode.XlightsName);
    }

    [Fact]
    public void AmbiguousAlias_SameNameDifferentNodeId_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", [], [])],
            [],
            []);
        var aliases = new[]
        {
            new NodeAlias(new ModelId("Stable-A"), "Arch-01"),
            new NodeAlias(new ModelId("Stable-B"), "Arch-01"),
        };

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => builder.Build(imported, DefaultPolicy, aliases));
    }

    [Fact]
    public void AliasCollapsesTwoModelsToSameNodeId_Throws_Invalid_NegativePath()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [
                new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", [], []),
                new DisplayModel(new ModelId("Arch-02"), "Arch-02", "Arches", [], []),
            ],
            [],
            []);
        var aliases = new[]
        {
            new NodeAlias(new ModelId("Stable-A"), "Arch-01"),
            new NodeAlias(new ModelId("Stable-A"), "Arch-02"),
        };

        Assert.Throws<SpatialDisplayGraphInvalidException>(() => builder.Build(imported, DefaultPolicy, aliases));
    }

    [Fact]
    public void RespectGroupsFalse_SkipsGroupNodesAndEdges()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", ["Arches"], [new Point2(0, 0)])],
            [new DisplayModelGroup("Arches", [new DisplayGroupMember("Arch-01", DisplayGroupMemberKind.Model)])],
            []);

        var graph = builder.Build(imported, new GeometryPolicy(MaxNeighborDistance: 0.3, RespectGroups: false));

        Assert.Single(graph.Nodes);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void NoGeometry_FallsBackToCenterWithTag()
    {
        var builder = new DisplayGraphBuilder();
        var imported = new DisplayGraph(
            "hash",
            [new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arches", ["Arches"], [])],
            [new DisplayModelGroup("Arches", [new DisplayGroupMember("Arch-01", DisplayGroupMemberKind.Model)])],
            []);

        var graph = builder.Build(imported, DefaultPolicy);

        var modelNode = Assert.Single(graph.Nodes, n => n.ModelKind == "Arches");
        Assert.Equal(0.5, modelNode.X);
        Assert.Equal(0.5, modelNode.Y);
        Assert.Contains("NoGeometry", modelNode.Tags);

        var groupNode = Assert.Single(graph.Nodes, n => n.ModelKind == "Group");
        Assert.Equal(0.5, groupNode.X);
        Assert.Contains("NoGeometry", groupNode.Tags);

        Assert.Equal(0, Assert.Single(graph.Edges).Distance);
    }

    [Fact]
    public async Task Store_SaveAsync_Then_LoadAsync_RoundTrips()
    {
        var builder = new DisplayGraphBuilder();
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var graph = builder.Build(imported, DefaultPolicy);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step059-store-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonSpatialDisplayGraphStore(cacheDirectory);

            var path = await store.SaveAsync(graph, CancellationToken.None);
            Assert.Equal(store.GetPath(graph.SourceLayoutHash), path);

            var reloaded = await store.LoadAsync(path, CancellationToken.None);

            Assert.Equal(graph.SourceLayoutHash, reloaded.SourceLayoutHash);
            Assert.Equal(graph.VersionId, reloaded.VersionId);
            Assert.Equal(graph.Nodes.Count, reloaded.Nodes.Count);
            Assert.Equal(graph.Edges.Count, reloaded.Edges.Count);
            Assert.Equal(graph.Nodes.Select(n => n.Id), reloaded.Nodes.Select(n => n.Id));
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
    public async Task Store_LoadAsync_MissingFile_ThrowsFileNotFound_NegativePath()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step059-store-missing-{Guid.NewGuid():N}");
        var store = new JsonSpatialDisplayGraphStore(cacheDirectory);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => store.LoadAsync(Path.Combine(cacheDirectory, "does-not-exist.json"), CancellationToken.None));
    }

    [Fact]
    public async Task Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step059-store-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonSpatialDisplayGraphStore(cacheDirectory);
            await Assert.ThrowsAsync<SpatialDisplayGraphCorruptException>(
                () => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
