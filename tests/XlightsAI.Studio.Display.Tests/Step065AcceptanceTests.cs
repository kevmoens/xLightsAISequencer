using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display.Tests;

/// <summary>
/// PHASE-09 Step 065 -- "Add route validation, conflicting-write checks and incompatible prop fallback."
/// Required acceptance: "Invalid path cannot silently produce mis-targeted XSQ effects." Implementation
/// guidance: "Validate missing/locked prop, effect support, path intersections and overlaps; report
/// incompatibilities before generating XSQ." Failure/edge case: "Invalid input, cancellation or stale
/// persisted state must not corrupt the project." These tests exercise the real
/// <see cref="ShowLayoutImporter"/>/<see cref="DisplayGraphBuilder"/>/<see cref="RouteValidator"/>/
/// <see cref="LockedNodeCatalog"/>/<see cref="RouteValidationCatalog"/>/<see cref="JsonLockedNodeStore"/>/
/// <see cref="JsonRouteValidationReportStore"/> (not mocks) against the same Step 059 golden fixture (five
/// arches at WorldPosX 0,10,20,30,40).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step065AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static readonly ModelId[] Arches =
    [
        new("Arch-01"), new("Arch-02"), new("Arch-03"), new("Arch-04"), new("Arch-05"),
    ];

    private static async Task<SpatialDisplayGraph> BuildArchGraphAsync()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var imported = await importer.ImportReadOnlyAsync(FixtureDirectory("sample-show-spatial-display"), CancellationToken.None);
        return builder.Build(imported, DefaultPolicy);
    }

    /// <summary>A small, hand-built graph whose node positions deliberately form a crossing "bowtie" route: A(0,0) B(1,1) C(1,0) D(0,1).</summary>
    private static SpatialDisplayGraph BuildCrossingGraph()
    {
        var nodes = new[]
        {
            new DisplayNode(new ModelId("A"), "A", 0.0, 0.0, "Arch", new HashSet<string>()),
            new DisplayNode(new ModelId("B"), "B", 1.0, 1.0, "Arch", new HashSet<string>()),
            new DisplayNode(new ModelId("C"), "C", 1.0, 0.0, "Arch", new HashSet<string>()),
            new DisplayNode(new ModelId("D"), "D", 0.0, 1.0, "Arch", new HashSet<string>()),
        };
        return new SpatialDisplayGraph("crossing-hash", nodes, [], [], Guid.NewGuid());
    }

    [Fact]
    public async Task Step065_Acceptance_InvalidPathNeverSilentlyProducesMisTargetedXsqEffects()
    {
        var graph = await BuildArchGraphAsync();
        var validator = new RouteValidator();
        var empty = LockedNodeSet.Empty(graph.SourceLayoutHash);

        // Clean path: no issues at all, never blocked.
        var cleanPath = new SpatialPath(Guid.NewGuid(), "Clean Route", Arches, IsClosed: false, Version: 0);
        var cleanReport = validator.Validate(cleanPath, graph, empty, recipeId: null, EffectRecipeSupport.Unrestricted);
        Assert.Empty(cleanReport.Issues);
        Assert.False(cleanReport.HasBlockingIssues);

        // Missing node: a path referencing a node absent from the display graph must be reported as a
        // blocking error, never silently dropped or targeted.
        var missingPath = new SpatialPath(Guid.NewGuid(), "Missing Route", [Arches[0], new ModelId("Ghost")], IsClosed: false, Version: 0);
        var missingReport = validator.Validate(missingPath, graph, empty, recipeId: null, EffectRecipeSupport.Unrestricted);
        Assert.Contains(missingReport.Issues, i => i.Kind == RouteIssueKind.MissingNode && i.Severity == RouteIssueSeverity.Error && i.NodeId == new ModelId("Ghost"));
        Assert.True(missingReport.HasBlockingIssues);

        // Locked node: a path referencing an explicitly locked node must be reported as a blocking error.
        var locked = empty with { LockedNodeIds = [Arches[2]], Version = 1 };
        var lockedPath = new SpatialPath(Guid.NewGuid(), "Locked Route", Arches, IsClosed: false, Version: 0);
        var lockedReport = validator.Validate(lockedPath, graph, locked, recipeId: null, EffectRecipeSupport.Unrestricted);
        Assert.Contains(lockedReport.Issues, i => i.Kind == RouteIssueKind.LockedNode && i.Severity == RouteIssueSeverity.Error && i.NodeId == Arches[2]);
        Assert.True(lockedReport.HasBlockingIssues);

        // Incompatible recipe: a declared-unsupported recipe for the node's own model kind is a blocking error.
        var recipeSupport = new EffectRecipeSupport(new Dictionary<string, IReadOnlySet<string>>
        {
            ["Arches"] = new HashSet<string> { "Twinkle" },
        });
        var recipeReport = validator.Validate(cleanPath, graph, empty, recipeId: "Spin", recipeSupport);
        Assert.True(recipeReport.Issues.Count(i => i.Kind == RouteIssueKind.IncompatibleRecipe && i.Severity == RouteIssueSeverity.Error) == Arches.Length);
        Assert.True(recipeReport.HasBlockingIssues);

        // The exact same recipe declared supported produces no incompatibility issues at all.
        var compatibleReport = validator.Validate(cleanPath, graph, empty, recipeId: "Twinkle", recipeSupport);
        Assert.DoesNotContain(compatibleReport.Issues, i => i.Kind == RouteIssueKind.IncompatibleRecipe);
        Assert.False(compatibleReport.HasBlockingIssues);
    }

    [Fact]
    public void PathIntersectionAndOverlap_AreReportedAsWarnings_NeverBlocking()
    {
        var graph = BuildCrossingGraph();
        var validator = new RouteValidator();
        var empty = LockedNodeSet.Empty(graph.SourceLayoutHash);

        // A -> B -> C -> D: segment A-B and segment C-D cross in the middle of the bowtie.
        var crossingPath = new SpatialPath(Guid.NewGuid(), "Bowtie",
            [new ModelId("A"), new ModelId("B"), new ModelId("C"), new ModelId("D")], IsClosed: false, Version: 0);
        var report = validator.Validate(crossingPath, graph, empty, recipeId: null, EffectRecipeSupport.Unrestricted);

        Assert.Contains(report.Issues, i => i.Kind == RouteIssueKind.PathIntersection && i.Severity == RouteIssueSeverity.Warning);
        // A geometric crossing is still a valid, usable route -- never blocking.
        Assert.False(report.HasBlockingIssues);

        // Two different nodes sharing the exact same position: an explicit coincident "overlap" node E at A's own position.
        var overlapNodes = new List<DisplayNode>(graph.Nodes) { new(new ModelId("E"), "E", 0.0, 0.0, "Arch", new HashSet<string>()) };
        var overlapGraph = graph with { Nodes = overlapNodes };
        var overlapPath = new SpatialPath(Guid.NewGuid(), "Overlap", [new ModelId("A"), new ModelId("E")], IsClosed: false, Version: 0);
        var overlapReport = validator.Validate(overlapPath, overlapGraph, LockedNodeSet.Empty(overlapGraph.SourceLayoutHash), recipeId: null, EffectRecipeSupport.Unrestricted);

        Assert.Contains(overlapReport.Issues, i => i.Kind == RouteIssueKind.PathOverlap && i.Severity == RouteIssueSeverity.Warning);
        Assert.False(overlapReport.HasBlockingIssues);

        // A path that simply revisits the same node id (an intentional out-and-back) is never itself flagged as an overlap.
        var revisitGraphNodes = graph.Nodes; // reuse plain A/B/C/D, no coincident duplicates.
        var revisitPath = new SpatialPath(Guid.NewGuid(), "OutAndBack", [new ModelId("A"), new ModelId("B"), new ModelId("A")], IsClosed: false, Version: 0);
        var revisitReport = validator.Validate(revisitPath, graph, empty, recipeId: null, EffectRecipeSupport.Unrestricted);
        Assert.DoesNotContain(revisitReport.Issues, i => i.Kind == RouteIssueKind.PathOverlap);
    }

    [Fact]
    public async Task Validate_InvalidInput_RejectsBeforeAnyIssueIsProduced_NegativePath()
    {
        var graph = await BuildArchGraphAsync();
        var validator = new RouteValidator();
        var empty = LockedNodeSet.Empty(graph.SourceLayoutHash);

        // Empty path.
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            validator.Validate(new SpatialPath(Guid.NewGuid(), "Empty", [], IsClosed: false, Version: 0), graph, empty, null, EffectRecipeSupport.Unrestricted));

        // Locked-node set authored against a different layout than the display graph.
        var mismatched = LockedNodeSet.Empty("some-other-layout-hash");
        var validPath = new SpatialPath(Guid.NewGuid(), "Valid", Arches, IsClosed: false, Version: 0);
        Assert.Throws<SpatialDisplayGraphInvalidException>(() =>
            validator.Validate(validPath, graph, mismatched, null, EffectRecipeSupport.Unrestricted));
    }

    [Fact]
    public async Task LockedNodeCatalog_LockAndUnlock_PersistsAndRejectsInvalidInput()
    {
        var graph = await BuildArchGraphAsync();
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step065-locks-{Guid.NewGuid():N}");
        try
        {
            var catalog = new LockedNodeCatalog(new JsonLockedNodeStore(cacheDirectory));

            var empty = await catalog.GetLockedNodesAsync(graph.SourceLayoutHash, CancellationToken.None);
            Assert.Empty(empty.LockedNodeIds);
            Assert.Equal(0, empty.Version);

            var afterLock = await catalog.LockAsync(graph, Arches[1], CancellationToken.None);
            Assert.Contains(Arches[1], afterLock.LockedNodeIds);
            Assert.Equal(1, afterLock.Version);

            // Unknown node id rejected before any write.
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.LockAsync(graph, new ModelId("Does-Not-Exist"), CancellationToken.None));

            // Already-locked node rejected before any write.
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.LockAsync(graph, Arches[1], CancellationToken.None));

            var afterUnlock = await catalog.UnlockAsync(graph, Arches[1], CancellationToken.None);
            Assert.DoesNotContain(Arches[1], afterUnlock.LockedNodeIds);
            Assert.Equal(2, afterUnlock.Version);

            // Unlocking a node that is not locked rejected before any write.
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.UnlockAsync(graph, Arches[1], CancellationToken.None));

            // Reload across a brand-new catalog/store instance over the same directory -- persisted across "restart".
            var reloaded = await new LockedNodeCatalog(new JsonLockedNodeStore(cacheDirectory))
                .GetLockedNodesAsync(graph.SourceLayoutHash, CancellationToken.None);
            Assert.Equal(afterUnlock.Version, reloaded.Version);
            Assert.Equal(afterUnlock.LockedNodeIds, reloaded.LockedNodeIds);
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
    public async Task RouteValidationCatalog_ValidateAsync_PersistsAndReloadsAcrossRestart()
    {
        var graph = await BuildArchGraphAsync();
        var path = new SpatialPath(Guid.NewGuid(), "Arch Route", Arches, IsClosed: false, Version: 0);
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step065-reports-{Guid.NewGuid():N}");
        try
        {
            var catalog1 = new RouteValidationCatalog(new JsonRouteValidationReportStore(cacheDirectory), new RouteValidator());
            var report = await catalog1.ValidateAsync(
                graph, path, LockedNodeSet.Empty(graph.SourceLayoutHash), null, EffectRecipeSupport.Unrestricted, CancellationToken.None);

            Assert.Equal(graph.SourceLayoutHash, report.SourceLayoutHash);
            Assert.Equal(path.Id, report.PathId);
            Assert.False(report.HasBlockingIssues);

            var store2 = new JsonRouteValidationReportStore(cacheDirectory);
            var cachePath = store2.GetPath(graph.SourceLayoutHash, path.Id, path.Version, recipeId: null);
            Assert.True(File.Exists(cachePath));
            var reloaded = await store2.LoadAsync(cachePath, CancellationToken.None);
            Assert.Equal(report.Issues, reloaded.Issues);
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
    public async Task RouteValidationCatalog_ValidateAsync_ConflictingWrite_RejectsStalePathVersion_NegativePath()
    {
        var graph = await BuildArchGraphAsync();
        var pathId = Guid.NewGuid();
        var stalePath = new SpatialPath(pathId, "Arch Route", Arches, IsClosed: false, Version: 0);
        var livePath = stalePath with { Version = 1, OrderedModels = [.. Arches.Reverse()] };
        var graphWithLivePath = graph with { Paths = [livePath] };

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step065-conflict-{Guid.NewGuid():N}");
        try
        {
            var catalog = new RouteValidationCatalog(new JsonRouteValidationReportStore(cacheDirectory), new RouteValidator());

            // Validating the stale (version 0) in-memory copy while the live graph already carries version 1
            // for the same path id must be rejected outright -- the conflicting-write check -- before any
            // geometry check runs or any cache file is written.
            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.ValidateAsync(graphWithLivePath, stalePath, LockedNodeSet.Empty(graph.SourceLayoutHash), null, EffectRecipeSupport.Unrestricted, CancellationToken.None));
            Assert.False(Directory.Exists(cacheDirectory));

            // Validating the exact live version succeeds normally.
            var report = await catalog.ValidateAsync(graphWithLivePath, livePath, LockedNodeSet.Empty(graph.SourceLayoutHash), null, EffectRecipeSupport.Unrestricted, CancellationToken.None);
            Assert.False(report.HasBlockingIssues);

            // A path never attached to the graph's own live Paths (e.g. not yet saved) has nothing to
            // conflict with and is never rejected on this basis alone.
            var neverSaved = new SpatialPath(Guid.NewGuid(), "Brand New", Arches, IsClosed: false, Version: 0);
            var neverSavedReport = await catalog.ValidateAsync(graphWithLivePath, neverSaved, LockedNodeSet.Empty(graph.SourceLayoutHash), null, EffectRecipeSupport.Unrestricted, CancellationToken.None);
            Assert.False(neverSavedReport.HasBlockingIssues);
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
    public async Task RouteValidationCatalog_ValidateAsync_InvalidInput_NeverWritesCacheFile_NegativePath()
    {
        var graph = await BuildArchGraphAsync();
        var emptyPath = new SpatialPath(Guid.NewGuid(), "Empty", [], IsClosed: false, Version: 0);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step065-invalid-{Guid.NewGuid():N}");
        try
        {
            var catalog = new RouteValidationCatalog(new JsonRouteValidationReportStore(cacheDirectory), new RouteValidator());

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.ValidateAsync(graph, emptyPath, LockedNodeSet.Empty(graph.SourceLayoutHash), null, EffectRecipeSupport.Unrestricted, CancellationToken.None));

            Assert.False(Directory.Exists(cacheDirectory));
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
    public async Task RouteValidationCatalog_ValidateAsync_Cancelled_DoesNotPersist_NegativePath()
    {
        var graph = await BuildArchGraphAsync();
        var path = new SpatialPath(Guid.NewGuid(), "Arch Route", Arches, IsClosed: false, Version: 0);

        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step065-cancel-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonRouteValidationReportStore(cacheDirectory);
            var catalog = new RouteValidationCatalog(store, new RouteValidator());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                catalog.ValidateAsync(graph, path, LockedNodeSet.Empty(graph.SourceLayoutHash), null, EffectRecipeSupport.Unrestricted, cts.Token));

            Assert.False(File.Exists(store.GetPath(graph.SourceLayoutHash, path.Id, path.Version, recipeId: null)));
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
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step065-store-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cacheDirectory);
        var reportPath = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(reportPath, "{ not valid json");
        var lockPath = Path.Combine(cacheDirectory, "corrupt-locks.json");
        await File.WriteAllTextAsync(lockPath, "{ not valid json");

        try
        {
            var reportStore = new JsonRouteValidationReportStore(cacheDirectory);
            await Assert.ThrowsAsync<SpatialDisplayGraphCorruptException>(
                () => reportStore.LoadAsync(reportPath, CancellationToken.None));

            var lockStore = new JsonLockedNodeStore(cacheDirectory);
            await Assert.ThrowsAsync<SpatialDisplayGraphCorruptException>(
                () => lockStore.LoadAsync(lockPath, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
