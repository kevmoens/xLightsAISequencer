using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-10 Step 069 -- "Build sectional energy and color/brightness story." Required acceptance:
/// "Verse/chorus intensity curve follows plan." Fast, deterministic tests for
/// <see cref="VisualStoryEditorViewModel"/>'s own bindable contract (<see cref="VisualStoryEditorViewModel.OpenSongAsync"/>/
/// <see cref="VisualStoryEditorViewModel.Plan"/>/<see cref="VisualStoryEditorViewModel.SaveAsync"/>) against a
/// real <see cref="VisualStoryCatalog"/>/<see cref="JsonVisualStoryStore"/>/<see cref="SectionVisualStoryPlanner"/>
/// (not mocks) under a temporary directory. No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists (the
/// project's own pre-existing, consistent convention -- see <c>implementation-evidence/PHASE-10/STEP-067.md</c>
/// §3/§7); this step's own required acceptance text is proven at the bindable view-model layer the real
/// story UI would bind to, the same precedent Step 059-068's own evidence already established.
/// </summary>
public sealed class Step069VisualStoryEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step069-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private VisualStoryCatalog BuildCatalog() => new(new JsonVisualStoryStore(_cacheDirectory));

    [Fact]
    public async Task Step069_Acceptance_PlanAndSaveFollowVerseChorusIntensityCurve()
    {
        // Required acceptance, verbatim: "Verse/chorus intensity curve follows plan."
        var vm = new VisualStoryEditorViewModel();
        var catalog = BuildCatalog();
        var songId = Guid.NewGuid();

        var opened = await vm.OpenSongAsync(catalog, songId);
        Assert.True(opened);
        Assert.Equal(ScreenLoadState.Loaded, vm.LoadState);
        Assert.Null(vm.Version); // never saved yet.
        Assert.Empty(vm.Rows);

        var verseId = Guid.NewGuid();
        var chorusId = Guid.NewGuid();
        var planner = new SectionVisualStoryPlanner();
        var planned = vm.Plan(planner, [
            new SectionEnergyInput(verseId, SectionKind.Verse, 0.4),
            new SectionEnergyInput(chorusId, SectionKind.Chorus, 0.9),
        ]);
        Assert.True(planned);
        Assert.True(vm.HasUnsavedPlan);

        var verseRow = vm.Rows.Single(r => r.SectionId == verseId);
        var chorusRow = vm.Rows.Single(r => r.SectionId == chorusId);
        Assert.True(chorusRow.Intensity > verseRow.Intensity);
        Assert.True(chorusRow.Intensity < 1.0);

        var saved = await vm.SaveAsync(catalog);
        Assert.True(saved);
        Assert.Equal(0, vm.Version);
        Assert.False(vm.HasUnsavedPlan);

        // Rows still reflect the now-persisted plan after saving.
        Assert.True(vm.Rows.Single(r => r.SectionId == chorusId).Intensity > vm.Rows.Single(r => r.SectionId == verseId).Intensity);
    }

    [Fact]
    public async Task VisualStory_PersistsAndCanChangePerSong_SavedStateAcrossRestart()
    {
        var vm = new VisualStoryEditorViewModel();
        var catalog = BuildCatalog();
        var songA = Guid.NewGuid();
        var songB = Guid.NewGuid();
        var planner = new SectionVisualStoryPlanner();

        await vm.OpenSongAsync(catalog, songA);
        vm.Plan(planner, [new SectionEnergyInput(Guid.NewGuid(), SectionKind.Verse, 0.4)]);
        await vm.SaveAsync(catalog);
        Assert.Equal(0, vm.Version);

        await vm.OpenSongAsync(catalog, songB);
        Assert.Null(vm.Version); // Song B's own independent, never-saved story -- not a leftover from Song A.
        Assert.Empty(vm.Rows);
        vm.Plan(planner, [new SectionEnergyInput(Guid.NewGuid(), SectionKind.Chorus, 0.9)]);
        await vm.SaveAsync(catalog);
        Assert.Equal(0, vm.Version);

        // Restart: a brand-new view-model/catalog instance over the same on-disk directory.
        var restartVm = new VisualStoryEditorViewModel();
        var restartCatalog = BuildCatalog();
        await restartVm.OpenSongAsync(restartCatalog, songA);
        Assert.Single(restartVm.Rows);
        Assert.Equal(SectionKind.Verse, restartVm.Rows[0].Kind);

        await restartVm.OpenSongAsync(restartCatalog, songB);
        Assert.Single(restartVm.Rows);
        Assert.Equal(SectionKind.Chorus, restartVm.Rows[0].Kind);
    }

    [Fact]
    public async Task Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath()
    {
        var vm = new VisualStoryEditorViewModel();
        var planner = new SectionVisualStoryPlanner();

        var planned = vm.Plan(planner, [new SectionEnergyInput(Guid.NewGuid(), SectionKind.Verse, 0.4)]);

        Assert.False(planned);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task Plan_InvalidSections_RejectsWithoutPartialRows_NegativePath()
    {
        var vm = new VisualStoryEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSongAsync(catalog, Guid.NewGuid());
        var planner = new SectionVisualStoryPlanner();

        var planned = vm.Plan(planner, []);

        Assert.False(planned);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Empty(vm.Rows);
        Assert.False(vm.HasUnsavedPlan);
    }

    [Fact]
    public async Task SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath()
    {
        var vm = new VisualStoryEditorViewModel();
        var catalog = BuildCatalog();

        var saved = await vm.SaveAsync(catalog);

        Assert.False(saved);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Null(vm.Version);
    }

    [Fact]
    public async Task SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath()
    {
        var vm = new VisualStoryEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenSongAsync(catalog, Guid.NewGuid());

        var saved = await vm.SaveAsync(catalog);

        Assert.False(saved);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Null(vm.Version);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task OpenSongAsync_CorruptPersistedFile_SetsErrorAndIsError_NegativePath()
    {
        Directory.CreateDirectory(_cacheDirectory);
        var songId = Guid.NewGuid();
        var store = new JsonVisualStoryStore(_cacheDirectory);
        await File.WriteAllTextAsync(store.GetPath(songId), "{ not valid json");

        var vm = new VisualStoryEditorViewModel();
        var catalog = BuildCatalog();

        var opened = await vm.OpenSongAsync(catalog, songId);

        Assert.False(opened);
        Assert.True(vm.IsError);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.SongId);
    }
}
