using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Display.Tests;

/// <summary>
/// PHASE-09 Step 063 -- "Persist role tags: lead, rhythm, ambient, hero and accent." Required acceptance:
/// "Role assignments are available in choreography UI." Implementation guidance: "Assign roles
/// independently of paths so same prop can play lead in one phrase and background later; provide conflict
/// policy." Failure/edge case: "Invalid input, cancellation or stale persisted state must not corrupt the
/// project." These tests exercise the real <see cref="ShowLayoutImporter"/>/<see cref="DisplayGraphBuilder"/>/
/// <see cref="RoleAssignmentCatalog"/>/<see cref="JsonRoleAssignmentStore"/> (not mocks) against the same
/// Step 059 golden fixture (five arches at WorldPosX 0,10,20,30,40; a three-segment roofline at WorldPosX
/// 5,20,35, WorldPosY 8).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step063AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static readonly ModelId Arch01 = new("Arch-01");
    private static readonly ModelId Arch02 = new("Arch-02");

    private static async Task<SpatialDisplayGraph> BuildFixtureGraphAsync()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        return builder.Build(imported, DefaultPolicy);
    }

    private static string NewCacheDirectory(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"xlai-step063-{suffix}-{Guid.NewGuid():N}");

    [Fact]
    public async Task Step063_Acceptance_Passes_RoleAssignmentsAreAvailableAfterLoad()
    {
        // "Role assignments are available in choreography UI" -- proven here at the domain/catalog layer
        // (the real, non-UI boundary the choreography UI itself would read through); the Shell-layer
        // DisplayPathEditorViewModel test proves the same claim at the bindable view-model layer.
        var graph = await BuildFixtureGraphAsync();
        var cacheDirectory = NewCacheDirectory("acceptance");
        try
        {
            var catalog = new RoleAssignmentCatalog(new JsonRoleAssignmentStore(cacheDirectory));
            var phraseA = Guid.NewGuid();
            var phraseB = Guid.NewGuid();

            // Same prop (Arch-01) plays Lead in phrase A and Ambient in phrase B -- the required
            // implementation guidance, verbatim: "same prop can play lead in one phrase and background
            // later."
            var afterLead = await catalog.AssignAsync(graph, Arch01, RoleTag.Lead, phraseA, CancellationToken.None);
            var afterAmbient = await catalog.AssignAsync(graph, Arch01, RoleTag.Ambient, phraseB, CancellationToken.None);

            Assert.Equal(2, afterAmbient.Assignments.Count);
            var leadInA = Assert.Single(afterAmbient.Assignments, a => a.ScopeId == phraseA);
            var ambientInB = Assert.Single(afterAmbient.Assignments, a => a.ScopeId == phraseB);
            Assert.Equal(RoleTag.Lead, leadInA.Role);
            Assert.Equal(RoleTag.Ambient, ambientInB.Role);
            Assert.Equal(Arch01, leadInA.NodeId);
            Assert.Equal(Arch01, ambientInB.NodeId);

            // Available/loadable via a brand-new catalog instance -- the real "available in choreography
            // UI" read path.
            var reloadCatalog = new RoleAssignmentCatalog(new JsonRoleAssignmentStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAssignmentsAsync(graph.SourceLayoutHash, CancellationToken.None);
            Assert.Equal(2, reloaded.Assignments.Count);
            Assert.Equal(RoleTag.Lead, Assert.Single(reloaded.Assignments, a => a.ScopeId == phraseA).Role);
            Assert.Equal(RoleTag.Ambient, Assert.Single(reloaded.Assignments, a => a.ScopeId == phraseB).Role);
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
    public async Task AssignAsync_SamePairTwice_ReplacesWithVersionedHistory_ConflictPolicy()
    {
        var graph = await BuildFixtureGraphAsync();
        var cacheDirectory = NewCacheDirectory("conflict");
        try
        {
            var catalog = new RoleAssignmentCatalog(new JsonRoleAssignmentStore(cacheDirectory));
            var scope = Guid.NewGuid();

            var first = await catalog.AssignAsync(graph, Arch01, RoleTag.Lead, scope, CancellationToken.None);
            Assert.Equal(1, first.Version);
            var firstAssignment = Assert.Single(first.Assignments);
            Assert.Equal(0, firstAssignment.Version);
            Assert.Equal(RoleTag.Lead, firstAssignment.Role);
            Assert.Empty(first.History);

            // Reassigning the same (node, scope) pair does not stack -- it replaces, per this step's own
            // required conflict policy.
            var second = await catalog.AssignAsync(graph, Arch01, RoleTag.Hero, scope, CancellationToken.None);
            Assert.Equal(2, second.Version);
            var liveAssignment = Assert.Single(second.Assignments);
            Assert.Equal(RoleTag.Hero, liveAssignment.Role);
            Assert.Equal(1, liveAssignment.Version);
            Assert.Single(second.History, h => h.Role == RoleTag.Lead && h.Version == 0);

            // Restart: a brand-new catalog/store instance over the same on-disk directory.
            var reloadCatalog = new RoleAssignmentCatalog(new JsonRoleAssignmentStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAssignmentsAsync(graph.SourceLayoutHash, CancellationToken.None);
            Assert.Equal(2, reloaded.Version);
            Assert.Equal(RoleTag.Hero, Assert.Single(reloaded.Assignments).Role);
            Assert.Single(reloaded.History);
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
    public async Task ClearAsync_RemovesLiveAssignmentAndRecordsHistory()
    {
        var graph = await BuildFixtureGraphAsync();
        var cacheDirectory = NewCacheDirectory("clear");
        try
        {
            var catalog = new RoleAssignmentCatalog(new JsonRoleAssignmentStore(cacheDirectory));
            var scope = Guid.NewGuid();
            await catalog.AssignAsync(graph, Arch02, RoleTag.Accent, scope, CancellationToken.None);

            var cleared = await catalog.ClearAsync(graph, Arch02, scope, CancellationToken.None);

            Assert.Empty(cleared.Assignments);
            Assert.Single(cleared.History, h => h.NodeId == Arch02 && h.Role == RoleTag.Accent);
            Assert.Equal(2, cleared.Version);
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
    public async Task AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath()
    {
        var graph = await BuildFixtureGraphAsync();
        var cacheDirectory = NewCacheDirectory("invalid");
        try
        {
            var store = new JsonRoleAssignmentStore(cacheDirectory);
            var catalog = new RoleAssignmentCatalog(store);
            var scope = Guid.NewGuid();

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.AssignAsync(graph, new ModelId("Does-Not-Exist"), RoleTag.Lead, scope, CancellationToken.None)); // unknown node

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.AssignAsync(graph, Arch01, "Soloist", scope, CancellationToken.None)); // unrecognized role

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
    public async Task ClearAsync_UnknownPair_Throws_NegativePath()
    {
        var graph = await BuildFixtureGraphAsync();
        var cacheDirectory = NewCacheDirectory("clear-unknown");
        try
        {
            var catalog = new RoleAssignmentCatalog(new JsonRoleAssignmentStore(cacheDirectory));
            var scope = Guid.NewGuid();

            await Assert.ThrowsAsync<SpatialDisplayGraphInvalidException>(() =>
                catalog.ClearAsync(graph, Arch01, scope, CancellationToken.None));
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
    public async Task AssignAsync_Cancelled_DoesNotPersist_NegativePath()
    {
        var graph = await BuildFixtureGraphAsync();
        var cacheDirectory = NewCacheDirectory("cancel");
        try
        {
            var store = new JsonRoleAssignmentStore(cacheDirectory);
            var catalog = new RoleAssignmentCatalog(store);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                catalog.AssignAsync(graph, Arch01, RoleTag.Lead, Guid.NewGuid(), cts.Token));

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
        var cacheDirectory = NewCacheDirectory("store-corrupt");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var store = new JsonRoleAssignmentStore(cacheDirectory);
            await Assert.ThrowsAsync<SpatialDisplayGraphCorruptException>(
                () => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
