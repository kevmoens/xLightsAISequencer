using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-10 Step 068 -- "Create configurable visual density and focus budget." Required acceptance: "Clutter
/// fixture is reduced while preserving focal musical event." Fast, deterministic tests for
/// <see cref="VisualBudgetEditorViewModel"/>'s own bindable contract (<see cref="VisualBudgetEditorViewModel.OpenPresetAsync"/>/
/// <see cref="VisualBudgetEditorViewModel.SaveAsync"/>/<see cref="VisualBudgetEditorViewModel.Preview"/>)
/// against a real <see cref="VisualBudgetPresetCatalog"/>/<see cref="JsonVisualBudgetPresetStore"/>/<see cref="VisualBudgetResolver"/>
/// (not mocks) under a temporary directory. No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists (the
/// project's own pre-existing, consistent convention -- see <c>implementation-evidence/PHASE-10/STEP-067.md</c>
/// §3/§7); this step's own required acceptance text is proven at the bindable view-model layer the real
/// budget UI would bind to, the same precedent Step 059-067's own evidence already established.
/// </summary>
public sealed class Step068VisualBudgetEditorViewModelTests : IDisposable
{
    private const long TicksPerSecond = 10_000_000;

    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step068-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    private VisualBudgetPresetCatalog BuildCatalog() => new(new JsonVisualBudgetPresetStore(_cacheDirectory));

    private static MusicalRoleAssignment Assignment(string trackId, MusicalRole role, string tier, int priority) =>
        new(Guid.NewGuid(), trackId, role, Guid.NewGuid(), tier, priority, Weight: 0.5, Version: 0);

    [Fact]
    public async Task Step068_Acceptance_PreviewReducesClutterWhilePreservingFocalEvent()
    {
        // Required acceptance, verbatim: "Clutter fixture is reduced while preserving focal musical event."
        var vm = new VisualBudgetEditorViewModel();
        var catalog = BuildCatalog();
        var evaluationWindow = new TimeWindow(0, 10 * TicksPerSecond);

        var opened = await vm.OpenPresetAsync(catalog, "Verse");
        Assert.True(opened);
        Assert.Equal(ScreenLoadState.Loaded, vm.LoadState);
        Assert.Null(vm.Version); // never saved yet -- DefaultBudget is in effect.
        Assert.Empty(vm.Rows);

        var tightBudget = new VisualBudget(MaxConcurrentFocalProps: 1, MaxMeanBrightness: 1.0, MaxEventDensityPerSecond: 0.3, DarknessFractionTarget: 0.1);
        var saved = await vm.SaveAsync(catalog, tightBudget);
        Assert.True(saved);
        Assert.Equal(0, vm.Version);

        var lead = new VisualBudgetCandidate(Assignment("T-Piano", MusicalRole.Lead, PrecedenceTier.Manual, 5), evaluationWindow, Brightness: 0.5, IsFocal: true);
        var clutter = Enumerable.Range(0, 10)
            .Select(i => new VisualBudgetCandidate(
                Assignment($"T-Clutter{i}", MusicalRole.Atmosphere, PrecedenceTier.Automated, i),
                new TimeWindow(i * TicksPerSecond, i * TicksPerSecond + TicksPerSecond / 10),
                Brightness: 0.2, IsFocal: false))
            .ToList();
        var candidates = new List<VisualBudgetCandidate> { lead };
        candidates.AddRange(clutter);

        var previewed = vm.Preview(new VisualBudgetResolver(new RolePrecedenceResolver()), candidates, evaluationWindow);
        Assert.True(previewed);
        Assert.Equal(11, vm.Rows.Count);

        var leadRow = vm.Rows.Single(r => r.SourceTrackId == "T-Piano");
        Assert.Equal(VisualBudgetDecision.Accepted, leadRow.Decision);
        Assert.Equal(evaluationWindow, leadRow.AdmittedWindow);

        var admittedClutterCount = clutter.Count(c => vm.Rows.Single(r => r.SourceTrackId == c.Assignment.SourceTrackId).Decision != VisualBudgetDecision.Dropped);
        Assert.True(admittedClutterCount < clutter.Count, "Clutter fixture must be reduced by the preview, not fully admitted.");
        Assert.NotNull(vm.ObservedMeanBrightness);
        Assert.NotNull(vm.ObservedEventDensityPerSecond);
    }

    [Fact]
    public async Task VisualBudgetPreset_PersistsAndCanChangePerPresetName_SavedStateAcrossRestart()
    {
        var vm = new VisualBudgetEditorViewModel();
        var catalog = BuildCatalog();

        await vm.OpenPresetAsync(catalog, "Verse");
        await vm.SaveAsync(catalog, new VisualBudget(1, 0.5, 1.0, 0.1));
        Assert.Equal(0, vm.Version);

        await vm.OpenPresetAsync(catalog, "Chorus");
        Assert.Null(vm.Version); // Chorus's own independent, never-saved budget -- not a leftover from Verse.
        Assert.Equal(VisualBudgetEditorViewModel.DefaultBudget, vm.CurrentBudget);
        await vm.SaveAsync(catalog, new VisualBudget(3, 0.9, 5.0, 0.4));
        Assert.Equal(0, vm.Version);

        // Restart: a brand-new view-model/catalog instance over the same on-disk directory.
        var restartVm = new VisualBudgetEditorViewModel();
        var restartCatalog = BuildCatalog();
        await restartVm.OpenPresetAsync(restartCatalog, "Verse");
        Assert.Equal(1, restartVm.CurrentBudget.MaxConcurrentFocalProps);
        await restartVm.OpenPresetAsync(restartCatalog, "Chorus");
        Assert.Equal(3, restartVm.CurrentBudget.MaxConcurrentFocalProps);
    }

    [Fact]
    public async Task SaveAsync_NothingOpen_SetsActionErrorAndLeavesBudgetUnchanged_NegativePath()
    {
        var vm = new VisualBudgetEditorViewModel();
        var catalog = BuildCatalog();

        var saved = await vm.SaveAsync(catalog, new VisualBudget(1, 0.5, 1.0, 0.1));

        Assert.False(saved);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Equal(VisualBudgetEditorViewModel.DefaultBudget, vm.CurrentBudget);
    }

    [Fact]
    public async Task SaveAsync_InvalidBudget_RejectsWithoutPartialMutation_NegativePath()
    {
        var vm = new VisualBudgetEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenPresetAsync(catalog, "Verse");
        await vm.SaveAsync(catalog, new VisualBudget(1, 0.5, 1.0, 0.1));
        var budgetBeforeRejection = vm.CurrentBudget;

        var saved = await vm.SaveAsync(catalog, new VisualBudget(-1, 0.5, 1.0, 0.1));

        Assert.False(saved);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Equal(budgetBeforeRejection, vm.CurrentBudget);
        Assert.Equal(0, vm.Version);
    }

    [Fact]
    public async Task Preview_NothingOpen_SetsActionErrorAndLeavesRowsEmpty_NegativePath()
    {
        var vm = new VisualBudgetEditorViewModel();
        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());

        var previewed = vm.Preview(resolver, [], new TimeWindow(0, TicksPerSecond));

        Assert.False(previewed);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task Preview_InvalidCandidate_RejectsWithoutPartialRows_NegativePath()
    {
        var vm = new VisualBudgetEditorViewModel();
        var catalog = BuildCatalog();
        await vm.OpenPresetAsync(catalog, "Verse");
        var resolver = new VisualBudgetResolver(new RolePrecedenceResolver());
        var window = new TimeWindow(0, TicksPerSecond);
        var badCandidate = new VisualBudgetCandidate(
            Assignment("T-Bad", MusicalRole.Lead, PrecedenceTier.Manual, 0), window, Brightness: 2.0, IsFocal: true);

        var previewed = vm.Preview(resolver, [badCandidate], window);

        Assert.False(previewed);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task OpenPresetAsync_CatalogRejectsUnsafeName_SetsErrorAndIsError_NegativePath()
    {
        var vm = new VisualBudgetEditorViewModel();
        var catalog = BuildCatalog();

        var opened = await vm.OpenPresetAsync(catalog, "bad/name");

        Assert.False(opened);
        Assert.True(vm.IsError);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Null(vm.PresetName);
    }
}
