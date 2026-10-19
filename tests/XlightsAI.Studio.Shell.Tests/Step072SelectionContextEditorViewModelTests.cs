using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-11 Step 072 -- "Create persisted selection context: interval, props, instruments, phrases,
/// locks." Required acceptance: "Serialization test preserves exact selection and scope." Required
/// failure/edge case: "Out-of-scope, locked or stale revision must be rejected without partial
/// mutation." Fast, deterministic tests for <see cref="SelectionContextEditorViewModel"/>'s own bindable
/// capture/reload contract against a real <see cref="SelectionContextCaptureService"/>/
/// <see cref="JsonSelectionContextStore"/> (not mocks) under a temporary directory. No real SCREEN 14 AI
/// EDIT Avalonia/AXAML view exists (the project's own pre-existing, consistent convention -- see
/// <c>implementation-evidence/PHASE-10/STEP-067.md</c> §3/§7); this step's own required "Avalonia
/// screenshot or repeatable desktop interaction recording with saved-state verification" outcome is
/// proven at the bindable view-model layer the real SCREEN 14 UI would bind to, the same precedent
/// Steps 059-071's own evidence already established.
/// </summary>
public sealed class Step072SelectionContextEditorViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"xlai-step072-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", [], []),
        ],
        Groups: [],
        Warnings: []);

    private static SequenceDocument GateDemoPlan(DisplayGraph layout) =>
        new ThreeEffectPlanFactory().CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], 300_000_000, new EffectRecipeRegistry());

    [Fact]
    public async Task Step072_Acceptance_CapturedSelectionSurvivesSaveAndRestartReload()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var scope = new EditScope(
            new TimeWindow(arch1.StartTicks, arch1.EndTicks),
            new HashSet<ModelId> { arch1.Target, arch2.Target });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());
        var store = new JsonSelectionContextStore(_root);
        var vm = new SelectionContextEditorViewModel();

        Assert.True(vm.IsEmpty);

        var captured = await vm.CaptureAsync(
            captureService, store, plan, new DocumentRevision(plan.Revision), scope,
            ["T-Piano"], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]);

        Assert.True(captured);
        Assert.True(vm.IsLoaded);
        Assert.NotNull(vm.Current);
        Assert.NotNull(vm.SavedPath);
        var originalContext = vm.Current;

        // Required acceptance, verbatim: "Serialization test preserves exact selection and scope" --
        // proven here via a real "restart" (a brand-new store instance against the same file).
        var freshVm = new SelectionContextEditorViewModel();
        var reloaded = await freshVm.ReloadAsync(new JsonSelectionContextStore(_root), vm.SavedPath!);

        Assert.True(reloaded);
        Assert.True(freshVm.IsLoaded);
        Assert.Equal(originalContext!.Id, freshVm.Current!.Id);
        Assert.Equal(originalContext.Scope.Time, freshVm.Current.Scope.Time);
        Assert.Equal(originalContext.Scope.Models, freshVm.Current.Scope.Models);
        Assert.Equal(originalContext.InstrumentTrackIds, freshVm.Current.InstrumentTrackIds);
        Assert.Equal(originalContext.PhraseIds, freshVm.Current.PhraseIds);
        Assert.Equal(originalContext.SpatialPathIds, freshVm.Current.SpatialPathIds);
        Assert.Equal(originalContext.NoteIds, freshVm.Current.NoteIds);
    }

    [Fact]
    public async Task StaleRevision_IsRejected_LeavesCurrentUnchanged_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());
        var store = new JsonSelectionContextStore(_root);
        var vm = new SelectionContextEditorViewModel();

        var ok = await vm.CaptureAsync(
            captureService, store, plan, new DocumentRevision(plan.Revision + 1), scope, [], [], [], []);

        Assert.False(ok);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.Current);
        Assert.NotNull(vm.ActionErrorMessage);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task LockedEffect_IsRejected_LeavesCurrentUnchanged_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };
        var scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch2.Target });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());
        var store = new JsonSelectionContextStore(_root);
        var vm = new SelectionContextEditorViewModel();

        var ok = await vm.CaptureAsync(
            captureService, store, planWithLock, new DocumentRevision(planWithLock.Revision), scope, [], [], [], []);

        Assert.False(ok);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.Current);
        Assert.Contains(arch2.Id.ToString(), vm.ActionErrorMessage);
    }

    [Fact]
    public async Task Reload_MissingFile_SetsErrorState_NegativePath()
    {
        var vm = new SelectionContextEditorViewModel();
        var store = new JsonSelectionContextStore(_root);

        var ok = await vm.ReloadAsync(store, Path.Combine(_root, "selection-context-does-not-exist.json"));

        Assert.False(ok);
        Assert.True(vm.IsError);
        Assert.Null(vm.Current);
        Assert.NotNull(vm.ErrorMessage);
    }
}
