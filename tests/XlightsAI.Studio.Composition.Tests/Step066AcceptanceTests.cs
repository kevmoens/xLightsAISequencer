using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Composition.Tests;

/// <summary>
/// PHASE-10 Step 066 -- "Define semantic musical roles and event precedence rules." Required acceptance:
/// "Synthetic role mapping yields deterministic priorities." Implementation guidance: "Represent
/// lead/accompaniment/accent and instrument track linkage per phrase rather than hardcode one stem per
/// display model." Failure/edge case: "Invalid input, cancellation or stale persisted state must not
/// corrupt the project." These tests exercise the real <see cref="RolePrecedenceResolver"/>/
/// <see cref="MusicalRoleAssignmentCatalog"/>/<see cref="JsonMusicalRoleAssignmentStore"/> (not mocks),
/// and -- for <see cref="MusicalRoleAssignment.SpatialPathId"/> validation -- a real
/// <see cref="SpatialPathSet"/> built from the real Step 059/Phase 09 golden fixture (five arches at
/// WorldPosX 0,10,20,30,40) via the real <see cref="ShowLayoutImporter"/>/<see cref="DisplayGraphBuilder"/>/
/// <see cref="SpatialPathCatalog"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step066AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static readonly GeometryPolicy DefaultPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    private static async Task<SpatialDisplayGraph> BuildFixtureGraphAsync()
    {
        var importer = new ShowLayoutImporter();
        var builder = new DisplayGraphBuilder();
        var directory = FixtureDirectory("sample-show-spatial-display");
        var imported = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        return builder.Build(imported, DefaultPolicy);
    }

    private static async Task<SpatialPathSet> BuildFixturePathSetAsync(SpatialDisplayGraph graph, string cacheDirectory)
    {
        var catalog = new SpatialPathCatalog(new JsonSpatialPathStore(cacheDirectory), new SpatialPathSuggester(), new SpatialPathEditor());
        var archIds = new[] { "Arch-01", "Arch-02", "Arch-03", "Arch-04", "Arch-05" }.Select(n => new ModelId(n)).ToList();
        return await catalog.CreateAsync(graph, "ArchRoute", archIds, DirectionPolicy.LeftToRight, isClosed: false, allowReplaceExisting: false, CancellationToken.None);
    }

    private static string NewCacheDirectory(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"xlai-step066-{suffix}-{Guid.NewGuid():N}");

    [Fact]
    public void Step066_Acceptance_SyntheticRoleMappingYieldsDeterministicPriorities()
    {
        // "Synthetic role mapping yields deterministic priorities" -- required acceptance, verbatim.
        var phraseId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        var lockedAccent = new MusicalRoleAssignment(phraseId, "T-Stab", MusicalRole.Accent, pathId, PrecedenceTier.UserLocked, Priority: 0, Weight: 0.4, Version: 0);
        var manualLeadHorn = new MusicalRoleAssignment(phraseId, "T-Horn", MusicalRole.Lead, pathId, PrecedenceTier.Manual, Priority: 1, Weight: 0.9, Version: 0);
        var manualLeadPiano = new MusicalRoleAssignment(phraseId, "T-Piano", MusicalRole.Lead, pathId, PrecedenceTier.Manual, Priority: 1, Weight: 0.9, Version: 0);
        var automatedBassLow = new MusicalRoleAssignment(phraseId, "T-Low", MusicalRole.Bass, pathId, PrecedenceTier.Automated, Priority: 5, Weight: 0.3, Version: 0);
        var automatedBassSub = new MusicalRoleAssignment(phraseId, "T-Sub", MusicalRole.Bass, pathId, PrecedenceTier.Automated, Priority: 10, Weight: 0.3, Version: 0);

        // Expected rank, verified by hand against the resolver's own documented rule (PrecedenceTier rank,
        // then Priority descending, then MusicalRole declared rank, then SourceTrackId ordinal):
        //   1. lockedAccent    (UserLocked, outranks every other tier regardless of role/priority)
        //   2. manualLeadHorn  (Manual, Lead, priority 1, ties manualLeadPiano -> "T-Horn" < "T-Piano" ordinal)
        //   3. manualLeadPiano (Manual, Lead, priority 1)
        //   4. automatedBassSub   (Automated, Bass, priority 10 -- higher priority wins within the same tier)
        //   5. automatedBassLow   (Automated, Bass, priority 5)
        var expectedOrder = new[] { lockedAccent, manualLeadHorn, manualLeadPiano, automatedBassSub, automatedBassLow };

        var resolver = new RolePrecedenceResolver();

        // Fed in a different order each time, and even reversed -- the required acceptance text is that
        // the mapping (not the caller's input order) determines the result.
        var inputOrderA = new[] { automatedBassLow, lockedAccent, manualLeadPiano, automatedBassSub, manualLeadHorn };
        var inputOrderB = ((IReadOnlyList<MusicalRoleAssignment>)inputOrderA).Reverse().ToList();

        var resultA = resolver.ResolveOrder(inputOrderA);
        var resultB = resolver.ResolveOrder(inputOrderB);

        Assert.Equal(expectedOrder, resultA);
        Assert.Equal(expectedOrder, resultB);
        Assert.Equal(resultA, resultB); // deterministic regardless of input order
    }

    [Fact]
    public void ResolveOrder_UnknownPrecedenceTier_Throws_NegativePath()
    {
        var invalid = new MusicalRoleAssignment(Guid.NewGuid(), "T-Bad", MusicalRole.Lead, Guid.NewGuid(), "NotARealTier", 0, 0, 0);
        var resolver = new RolePrecedenceResolver();

        Assert.Throws<CompositionInvalidException>(() => resolver.ResolveOrder([invalid]));
    }

    [Fact]
    public async Task AssignAsync_RoleMappingIsAvailableAfterSave_PerPhraseTrackLinkage()
    {
        // "Represent lead/accompaniment/accent and instrument track linkage per phrase rather than
        // hardcode one stem per display model" -- the same source track ("T-Piano") plays a different
        // role in two different phrases, proven here at the real persisted catalog boundary.
        var graph = await BuildFixtureGraphAsync();
        var fixtureCacheDirectory = NewCacheDirectory("fixture");
        var cacheDirectory = NewCacheDirectory("acceptance");
        try
        {
            var pathSet = await BuildFixturePathSetAsync(graph, fixtureCacheDirectory);
            var pathId = pathSet.Paths.Single().Id;

            var catalog = new MusicalRoleAssignmentCatalog(new JsonMusicalRoleAssignmentStore(cacheDirectory));
            var phraseA = Guid.NewGuid();
            var phraseB = Guid.NewGuid();

            var afterA = await catalog.AssignAsync(pathSet, phraseA, "T-Piano", MusicalRole.Lead, pathId, PrecedenceTier.Manual, 1, 0.9, CancellationToken.None);
            var afterB = await catalog.AssignAsync(pathSet, phraseB, "T-Piano", MusicalRole.Harmony, pathId, PrecedenceTier.Manual, 0, 0.4, CancellationToken.None);

            Assert.Equal(MusicalRole.Lead, Assert.Single(afterA.Assignments).Role);
            Assert.Equal(MusicalRole.Harmony, Assert.Single(afterB.Assignments).Role);

            var reloadCatalog = new MusicalRoleAssignmentCatalog(new JsonMusicalRoleAssignmentStore(cacheDirectory));
            var reloadedA = await reloadCatalog.GetAssignmentsAsync(phraseA, CancellationToken.None);
            var reloadedB = await reloadCatalog.GetAssignmentsAsync(phraseB, CancellationToken.None);
            Assert.Equal(MusicalRole.Lead, Assert.Single(reloadedA.Assignments).Role);
            Assert.Equal(MusicalRole.Harmony, Assert.Single(reloadedB.Assignments).Role);
        }
        finally
        {
            foreach (var dir in new[] { fixtureCacheDirectory, cacheDirectory })
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
    }

    [Fact]
    public async Task AssignAsync_SameTrackTwiceInSamePhrase_ReplacesWithVersionedHistory_ConflictPolicy()
    {
        var graph = await BuildFixtureGraphAsync();
        var fixtureCacheDirectory = NewCacheDirectory("fixture-conflict");
        var cacheDirectory = NewCacheDirectory("conflict");
        try
        {
            var pathSet = await BuildFixturePathSetAsync(graph, fixtureCacheDirectory);
            var pathId = pathSet.Paths.Single().Id;
            var catalog = new MusicalRoleAssignmentCatalog(new JsonMusicalRoleAssignmentStore(cacheDirectory));
            var phraseId = Guid.NewGuid();

            var first = await catalog.AssignAsync(pathSet, phraseId, "T-Piano", MusicalRole.Rhythm, pathId, PrecedenceTier.Automated, 2, 0.5, CancellationToken.None);
            Assert.Equal(1, first.Version);
            Assert.Equal(0, Assert.Single(first.Assignments).Version);
            Assert.Empty(first.History);

            var second = await catalog.AssignAsync(pathSet, phraseId, "T-Piano", MusicalRole.Lead, pathId, PrecedenceTier.Manual, 1, 0.9, CancellationToken.None);
            Assert.Equal(2, second.Version);
            var live = Assert.Single(second.Assignments);
            Assert.Equal(MusicalRole.Lead, live.Role);
            Assert.Equal(1, live.Version);
            Assert.Single(second.History, h => h.Role == MusicalRole.Rhythm && h.Version == 0);

            var reloadCatalog = new MusicalRoleAssignmentCatalog(new JsonMusicalRoleAssignmentStore(cacheDirectory));
            var reloaded = await reloadCatalog.GetAssignmentsAsync(phraseId, CancellationToken.None);
            Assert.Equal(2, reloaded.Version);
            Assert.Equal(MusicalRole.Lead, Assert.Single(reloaded.Assignments).Role);
            Assert.Single(reloaded.History);
        }
        finally
        {
            foreach (var dir in new[] { fixtureCacheDirectory, cacheDirectory })
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
    }

    [Fact]
    public async Task ClearAsync_RemovesLiveAssignmentAndRecordsHistory()
    {
        var graph = await BuildFixtureGraphAsync();
        var fixtureCacheDirectory = NewCacheDirectory("fixture-clear");
        var cacheDirectory = NewCacheDirectory("clear");
        try
        {
            var pathSet = await BuildFixturePathSetAsync(graph, fixtureCacheDirectory);
            var pathId = pathSet.Paths.Single().Id;
            var catalog = new MusicalRoleAssignmentCatalog(new JsonMusicalRoleAssignmentStore(cacheDirectory));
            var phraseId = Guid.NewGuid();
            await catalog.AssignAsync(pathSet, phraseId, "T-Snare", MusicalRole.Accent, pathId, PrecedenceTier.AcceptedMotif, 3, 0.6, CancellationToken.None);

            var cleared = await catalog.ClearAsync(phraseId, "T-Snare", CancellationToken.None);

            Assert.Empty(cleared.Assignments);
            Assert.Single(cleared.History, h => h.SourceTrackId == "T-Snare" && h.Role == MusicalRole.Accent);
            Assert.Equal(2, cleared.Version);
        }
        finally
        {
            foreach (var dir in new[] { fixtureCacheDirectory, cacheDirectory })
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
    }

    [Fact]
    public async Task AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath()
    {
        var graph = await BuildFixtureGraphAsync();
        var fixtureCacheDirectory = NewCacheDirectory("fixture-invalid");
        var cacheDirectory = NewCacheDirectory("invalid");
        try
        {
            var pathSet = await BuildFixturePathSetAsync(graph, fixtureCacheDirectory);
            var pathId = pathSet.Paths.Single().Id;
            var store = new JsonMusicalRoleAssignmentStore(cacheDirectory);
            var catalog = new MusicalRoleAssignmentCatalog(store);
            var phraseId = Guid.NewGuid();

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.AssignAsync(pathSet, phraseId, "   ", MusicalRole.Lead, pathId, PrecedenceTier.Manual, 0, 0, CancellationToken.None)); // blank track id

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.AssignAsync(pathSet, phraseId, "T-X", (MusicalRole)999, pathId, PrecedenceTier.Manual, 0, 0, CancellationToken.None)); // undefined role

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.AssignAsync(pathSet, phraseId, "T-X", MusicalRole.Lead, pathId, "NotARealTier", 0, 0, CancellationToken.None)); // unrecognized tier

            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.AssignAsync(pathSet, phraseId, "T-X", MusicalRole.Lead, Guid.NewGuid(), PrecedenceTier.Manual, 0, 0, CancellationToken.None)); // unknown path id

            Assert.False(File.Exists(store.GetPath(phraseId)));
        }
        finally
        {
            foreach (var dir in new[] { fixtureCacheDirectory, cacheDirectory })
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
    }

    [Fact]
    public async Task ClearAsync_UnknownPair_Throws_NegativePath()
    {
        var cacheDirectory = NewCacheDirectory("clear-unknown");
        try
        {
            var catalog = new MusicalRoleAssignmentCatalog(new JsonMusicalRoleAssignmentStore(cacheDirectory));
            await Assert.ThrowsAsync<CompositionInvalidException>(() =>
                catalog.ClearAsync(Guid.NewGuid(), "T-Nope", CancellationToken.None));
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
        var fixtureCacheDirectory = NewCacheDirectory("fixture-cancel");
        var cacheDirectory = NewCacheDirectory("cancel");
        try
        {
            var pathSet = await BuildFixturePathSetAsync(graph, fixtureCacheDirectory);
            var pathId = pathSet.Paths.Single().Id;
            var store = new JsonMusicalRoleAssignmentStore(cacheDirectory);
            var catalog = new MusicalRoleAssignmentCatalog(store);
            var phraseId = Guid.NewGuid();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                catalog.AssignAsync(pathSet, phraseId, "T-Piano", MusicalRole.Lead, pathId, PrecedenceTier.Manual, 0, 0, cts.Token));

            Assert.False(File.Exists(store.GetPath(phraseId)));
        }
        finally
        {
            foreach (var dir in new[] { fixtureCacheDirectory, cacheDirectory })
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
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
            var store = new JsonMusicalRoleAssignmentStore(cacheDirectory);
            await Assert.ThrowsAsync<CompositionCorruptException>(
                () => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
