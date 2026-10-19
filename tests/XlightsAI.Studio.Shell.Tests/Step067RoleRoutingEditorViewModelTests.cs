using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-10 Step 067 -- "Add configurable instrument-to-role and role-to-prop routing UI." Required
/// acceptance: "Mappings persist and can change per section." Fast, deterministic tests for
/// <see cref="RoleRoutingEditorViewModel"/>'s own bindable routing-table contract
/// (<see cref="RoleRoutingEditorViewModel.OpenSectionAsync"/>/<see cref="RoleRoutingEditorViewModel.AssignAsync"/>/
/// <see cref="RoleRoutingEditorViewModel.ClearAsync"/>) against a real
/// <see cref="MusicalRoleAssignmentCatalog"/>/<see cref="JsonMusicalRoleAssignmentStore"/> (not mocks) under
/// a temporary directory. No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists (the project's own
/// pre-existing, consistent convention -- see <c>implementation-evidence/PHASE-10/STEP-067.md</c> §3/§7);
/// this step's own required acceptance text is proven at the bindable view-model layer the real routing UI
/// would bind to, the same precedent Step 059-066's own evidence already established.
/// </summary>
public sealed class Step067RoleRoutingEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step067-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private MusicalRoleAssignmentCatalog BuildCatalog() => new(new JsonMusicalRoleAssignmentStore(_cacheDirectory));

    private static readonly Guid ArchesPathId = Guid.NewGuid();
    private static readonly Guid TreePathId = Guid.NewGuid();

    private static SpatialPathSet BuildPathSet(string layoutHash = "hash-067") => new(
        layoutHash,
        [
            new SpatialPath(ArchesPathId, "Arches", [new ModelId("Arch-01"), new ModelId("Arch-02")], IsClosed: false, Version: 0),
            new SpatialPath(TreePathId, "Tree", [new ModelId("Tree-01")], IsClosed: false, Version: 0),
        ],
        [],
        0);

    [Fact]
    public async Task Step067_Acceptance_MappingsPersistAndCanChangePerSection()
    {
        // Required acceptance, verbatim: "Mappings persist and can change per section."
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        var pathSet = BuildPathSet();
        var sectionVerse = Guid.NewGuid();
        var sectionChorus = Guid.NewGuid();
        var availableTracks = new[] { "T-Piano", "T-Bass" };

        var openedVerse = await vm.OpenSectionAsync(catalog, pathSet, sectionVerse, availableTracks);
        Assert.True(openedVerse);
        Assert.Equal(sectionVerse, vm.SectionId);
        Assert.Empty(vm.Rows);

        var assignedPiano = await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);
        Assert.True(assignedPiano);
        var assignedBass = await vm.AssignAsync(catalog, "T-Bass", MusicalRole.Bass, TreePathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);
        Assert.True(assignedBass);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(2, vm.Version);

        // Switch to a different section -- its own independently persisted (empty) routing, not a leftover
        // from the verse section.
        var openedChorus = await vm.OpenSectionAsync(catalog, pathSet, sectionChorus, availableTracks);
        Assert.True(openedChorus);
        Assert.Equal(sectionChorus, vm.SectionId);
        Assert.Empty(vm.Rows);
        Assert.Equal(0, vm.Version);

        var assignedPianoChorus = await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Harmony, TreePathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);
        Assert.True(assignedPianoChorus);
        Assert.Single(vm.Rows);
        Assert.Equal(MusicalRole.Harmony, vm.Rows.Single().Role);

        // Switch back to the verse section through a brand-new view model / catalog instance (a real
        // restart) -- the verse's own routing must be unaffected by the chorus section's own edits.
        var restartedVm = new RoleRoutingEditorViewModel();
        var restartedCatalog = BuildCatalog();
        var reopenedVerse = await restartedVm.OpenSectionAsync(restartedCatalog, pathSet, sectionVerse, availableTracks);
        Assert.True(reopenedVerse);
        Assert.Equal(2, restartedVm.Rows.Count);
        Assert.Equal(MusicalRole.Lead, restartedVm.Rows.Single(r => r.SourceTrackId == "T-Piano").Role);
        Assert.Equal(MusicalRole.Bass, restartedVm.Rows.Single(r => r.SourceTrackId == "T-Bass").Role);
    }

    [Fact]
    public async Task Warnings_UnmappedRole_ListsEveryRoleWithNoLiveRouting()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSectionAsync(catalog, BuildPathSet(), Guid.NewGuid(), ["T-Piano"]);

        await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);

        var unmapped = vm.Warnings.Where(w => w.Kind == RoleRoutingWarningKind.UnmappedRole).ToList();
        Assert.Equal(5, unmapped.Count); // every MusicalRole except Lead
        Assert.DoesNotContain(unmapped, w => w.Message.Contains("'Lead'", StringComparison.Ordinal));
        Assert.Contains(unmapped, w => w.Message.Contains("'Bass'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Warnings_UnavailableSource_FlaggedWhenTrackNoLongerInAvailableSet()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        var pathSet = BuildPathSet();
        var sectionId = Guid.NewGuid();
        await vm.OpenSectionAsync(catalog, pathSet, sectionId, ["T-Piano"]);
        await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);

        // Reopen the same section, but "T-Piano" has since disappeared from the available track set (e.g.
        // the stem was removed).
        var reopened = await vm.OpenSectionAsync(catalog, pathSet, sectionId, []);

        Assert.True(reopened);
        Assert.True(vm.Rows.Single().IsSourceUnavailable);
        Assert.Contains(vm.Warnings, w => w.Kind == RoleRoutingWarningKind.UnavailableSource && w.Message.Contains("T-Piano", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Warnings_UnavailablePath_FlaggedWhenTargetPathNoLongerExists()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        var sectionId = Guid.NewGuid();
        await vm.OpenSectionAsync(catalog, BuildPathSet(), sectionId, ["T-Piano"]);
        await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);

        // Reopen the same section against a path set where "Arches" has since been deleted.
        var shrunkPathSet = new SpatialPathSet("hash-067", [], [], 1);
        var reopened = await vm.OpenSectionAsync(catalog, shrunkPathSet, sectionId, ["T-Piano"]);

        Assert.True(reopened);
        var row = vm.Rows.Single();
        Assert.True(row.IsPathUnavailable);
        Assert.Null(row.SpatialPathName);
        Assert.Contains(vm.Warnings, w => w.Kind == RoleRoutingWarningKind.UnavailablePath && w.Message.Contains("T-Piano", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssignAsync_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath()
    {
        var vm = new RoleRoutingEditorViewModel();

        var assigned = await vm.AssignAsync(BuildCatalog(), "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);

        Assert.False(assigned);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task AssignAsync_UnknownSpatialPath_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSectionAsync(catalog, BuildPathSet(), Guid.NewGuid(), ["T-Piano"]);
        await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);
        var before = vm.Rows;

        var assigned = await vm.AssignAsync(catalog, "T-Bass", MusicalRole.Bass, Guid.NewGuid(), PrecedenceTier.Manual, priority: 1, weight: 1.0);

        Assert.False(assigned);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(before, vm.Rows);
    }

    [Fact]
    public async Task AssignAsync_UnrecognizedPrecedenceTier_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSectionAsync(catalog, BuildPathSet(), Guid.NewGuid(), ["T-Piano"]);
        await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);
        var before = vm.Rows;

        var assigned = await vm.AssignAsync(catalog, "T-Bass", MusicalRole.Bass, TreePathId, "SuperLocked", priority: 1, weight: 1.0);

        Assert.False(assigned);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(before, vm.Rows);
    }

    [Fact]
    public async Task ClearAsync_UnknownTrack_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSectionAsync(catalog, BuildPathSet(), Guid.NewGuid(), ["T-Piano"]);
        await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);
        var before = vm.Rows;

        var cleared = await vm.ClearAsync(catalog, "T-Bass");

        Assert.False(cleared);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(before, vm.Rows);
    }

    [Fact]
    public async Task ClearAsync_RemovesRowAndAddsUnmappedRoleWarningBack()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSectionAsync(catalog, BuildPathSet(), Guid.NewGuid(), ["T-Piano"]);
        await vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0);
        Assert.DoesNotContain(vm.Warnings, w => w.Kind == RoleRoutingWarningKind.UnmappedRole && w.Message.Contains("'Lead'", StringComparison.Ordinal));

        var cleared = await vm.ClearAsync(catalog, "T-Piano");

        Assert.True(cleared);
        Assert.Empty(vm.Rows);
        Assert.Contains(vm.Warnings, w => w.Kind == RoleRoutingWarningKind.UnmappedRole && w.Message.Contains("'Lead'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssignAsync_Cancelled_DoesNotPersist_NegativePath()
    {
        var vm = new RoleRoutingEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSectionAsync(catalog, BuildPathSet(), Guid.NewGuid(), ["T-Piano"]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            vm.AssignAsync(catalog, "T-Piano", MusicalRole.Lead, ArchesPathId, PrecedenceTier.Manual, priority: 1, weight: 1.0, cts.Token));

        Assert.Empty(vm.Rows);
        Assert.False(Directory.Exists(_cacheDirectory) && Directory.GetFiles(_cacheDirectory).Length > 0);
    }
}
