using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-10 Step 070 -- "Implement build, silence, transitions, release and phrase-ending recipes." Required
/// acceptance: "Musical break can intentionally darken all permitted props." Fast, deterministic tests for
/// <see cref="TransitionRecipeEditorViewModel"/>'s own bindable contract (<see cref="TransitionRecipeEditorViewModel.OpenPhraseAsync"/>/
/// <see cref="TransitionRecipeEditorViewModel.Plan"/>/<see cref="TransitionRecipeEditorViewModel.SaveAsync"/>)
/// against a real <see cref="TransitionPlanCatalog"/>/<see cref="JsonTransitionPlanStore"/>/<see cref="TransitionRecipePlanner"/>
/// (not mocks) under a temporary directory. No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists (the
/// project's own pre-existing, consistent convention -- see <c>implementation-evidence/PHASE-10/STEP-069.md</c>
/// §3/§7); this step's own required acceptance text is proven at the bindable view-model layer the real
/// recipe UI would bind to, the same precedent Step 059-069's own evidence already established.
/// </summary>
public sealed class Step070TransitionRecipeEditorViewModelTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step070-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private TransitionPlanCatalog BuildCatalog() => new(new JsonTransitionPlanStore(_cacheDirectory));

    private static XlightsAI.Studio.Contracts.TimeWindow Window(long startSeconds, long endSeconds) =>
        new(startSeconds * 10_000_000L, endSeconds * 10_000_000L);

    private static IReadOnlyList<ModelId> PermittedProps() => [new ModelId("Arches"), new ModelId("Roofline"), new ModelId("Tree")];

    [Fact]
    public async Task Step070_Acceptance_PlanAndSaveDarkenAllPermittedPropsForABlackout()
    {
        // Required acceptance, verbatim: "Musical break can intentionally darken all permitted props."
        var vm = new TransitionRecipeEditorViewModel();
        var catalog = BuildCatalog();
        var phraseId = Guid.NewGuid();

        var opened = await vm.OpenPhraseAsync(catalog, phraseId);
        Assert.True(opened);
        Assert.Equal(ScreenLoadState.Loaded, vm.LoadState);
        Assert.Null(vm.Version); // never saved yet.
        Assert.Empty(vm.Rows);

        var props = PermittedProps();
        var planner = new TransitionRecipePlanner();
        var planned = vm.Plan(planner, [new TransitionRecipeRequest(TransitionKind.Blackout, Window(10, 12), props)]);
        Assert.True(planned);
        Assert.True(vm.HasUnsavedPlan);

        var row = Assert.Single(vm.Rows);
        Assert.True(row.IsIntentionalBlackout);
        Assert.Equal(props.Count, row.TargetPropCount);
        Assert.Equal(0, row.StartIntensityPercent);
        Assert.Equal(0, row.EndIntensityPercent);

        var saved = await vm.SaveAsync(catalog);
        Assert.True(saved);
        Assert.Equal(0, vm.Version);
        Assert.False(vm.HasUnsavedPlan);

        // Rows still reflect the now-persisted plan after saving.
        Assert.True(vm.Rows.Single().IsIntentionalBlackout);
    }

    [Fact]
    public async Task TransitionPlan_PersistsAndCanChangePerPhrase_SavedStateAcrossRestart()
    {
        var vm = new TransitionRecipeEditorViewModel();
        var catalog = BuildCatalog();
        var phraseA = Guid.NewGuid();
        var phraseB = Guid.NewGuid();
        var planner = new TransitionRecipePlanner();
        var props = PermittedProps();

        await vm.OpenPhraseAsync(catalog, phraseA);
        vm.Plan(planner, [new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), props)]);
        await vm.SaveAsync(catalog);
        Assert.Equal(0, vm.Version);

        await vm.OpenPhraseAsync(catalog, phraseB);
        Assert.Null(vm.Version); // Phrase B's own independent, never-saved plan -- not a leftover from Phrase A.
        Assert.Empty(vm.Rows);
        vm.Plan(planner, [new TransitionRecipeRequest(TransitionKind.Blackout, Window(0, 2), props)]);
        await vm.SaveAsync(catalog);
        Assert.Equal(0, vm.Version);

        // Restart: a brand-new view-model/catalog instance over the same on-disk directory.
        var restartVm = new TransitionRecipeEditorViewModel();
        var restartCatalog = BuildCatalog();
        await restartVm.OpenPhraseAsync(restartCatalog, phraseA);
        Assert.Single(restartVm.Rows);
        Assert.Equal(TransitionKind.Build, restartVm.Rows[0].Kind);

        await restartVm.OpenPhraseAsync(restartCatalog, phraseB);
        Assert.Single(restartVm.Rows);
        Assert.Equal(TransitionKind.Blackout, restartVm.Rows[0].Kind);
    }

    [Fact]
    public void Plan_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath()
    {
        var vm = new TransitionRecipeEditorViewModel();
        var planner = new TransitionRecipePlanner();

        var planned = vm.Plan(planner, [new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), PermittedProps())]);

        Assert.False(planned);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task Plan_EmptyPermittedProps_RejectsWithoutPartialRows_NegativePath()
    {
        var vm = new TransitionRecipeEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenPhraseAsync(catalog, Guid.NewGuid());
        var planner = new TransitionRecipePlanner();

        var planned = vm.Plan(planner, [new TransitionRecipeRequest(TransitionKind.Build, Window(0, 4), [])]);

        Assert.False(planned);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Empty(vm.Rows);
        Assert.False(vm.HasUnsavedPlan);
    }

    [Fact]
    public async Task SaveAsync_NothingOpen_SetsActionErrorAndLeavesVersionNull_NegativePath()
    {
        var vm = new TransitionRecipeEditorViewModel();
        var catalog = BuildCatalog();

        var saved = await vm.SaveAsync(catalog);

        Assert.False(saved);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Null(vm.Version);
    }

    [Fact]
    public async Task SaveAsync_NothingPlanned_RejectsWithoutMutatingPersistedState_NegativePath()
    {
        var vm = new TransitionRecipeEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenPhraseAsync(catalog, Guid.NewGuid());

        var saved = await vm.SaveAsync(catalog);

        Assert.False(saved);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Null(vm.Version);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task OpenPhraseAsync_CorruptPersistedFile_SetsErrorAndIsError_NegativePath()
    {
        Directory.CreateDirectory(_cacheDirectory);
        var phraseId = Guid.NewGuid();
        var store = new JsonTransitionPlanStore(_cacheDirectory);
        await File.WriteAllTextAsync(store.GetPath(phraseId), "{ not valid json");

        var vm = new TransitionRecipeEditorViewModel();
        var catalog = BuildCatalog();

        var opened = await vm.OpenPhraseAsync(catalog, phraseId);

        Assert.False(opened);
        Assert.True(vm.IsError);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.PhraseId);
    }
}
