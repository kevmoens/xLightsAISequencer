using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits.Tests;

/// <summary>
/// PHASE-11 Step 072 ("Create persisted selection context: interval, props, instruments, phrases,
/// locks"). Required acceptance: "Serialization test preserves exact selection and scope." Required
/// failure/edge case: "Out-of-scope, locked or stale revision must be rejected without partial
/// mutation." These tests exercise the real <see cref="SelectionContextCaptureService"/>,
/// <see cref="EffectLockPolicy"/> and <see cref="JsonSelectionContextStore"/> against the same gate-demo
/// layout shape (Roofline, Arch 1, Arch 2) every prior PHASE-02/03 step's own tests already use, not
/// mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step072AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step072-" + Guid.NewGuid().ToString("N"));

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

    /// <summary>
    /// The phase gate's own named worked example: "Select 42-48 seconds on five arches..." — here
    /// narrowed to the gate-demo layout's own two arches, over the "Arch 1" effect's own window, naming
    /// its own effect id explicitly (PHASE-11 Section 3: "selected... effects" is part of a scoped
    /// selection, not only time+models).
    /// </summary>
    [Fact]
    public async Task Step072_Acceptance_SerializationPreservesExactSelectionAndScope()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");

        var scope = new EditScope(
            new TimeWindow(arch1.StartTicks, arch1.EndTicks),
            new HashSet<ModelId> { arch1.Target, arch2.Target },
            new HashSet<Guid> { arch1.Id });

        var instrumentTrackIds = new[] { "T-Piano", "T-KickSnare" };
        var phraseIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var spatialPathIds = new[] { Guid.NewGuid() };
        var noteIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());
        var captured = captureService.Capture(
            plan, new DocumentRevision(plan.Revision), scope, instrumentTrackIds, phraseIds, spatialPathIds, noteIds);

        Assert.Equal(plan.Revision, captured.BaseDocumentRevision.Value);
        Assert.Equal(scope.Time, captured.Scope.Time);
        Assert.Equal(scope.Models, captured.Scope.Models);
        Assert.Equal(scope.RespectLocks, captured.Scope.RespectLocks);

        var store = new JsonSelectionContextStore(_root);
        var savedPath = await store.SaveAsync(captured, CancellationToken.None);

        // Brand-new store instance — a real "restart" round trip, the same convention every other
        // PHASE-02/03/10 persistence step's own acceptance test already uses.
        var reloaded = await new JsonSelectionContextStore(_root).LoadAsync(savedPath, CancellationToken.None);

        Assert.Equal(captured.Id, reloaded.Id);
        Assert.Equal(captured.BaseDocumentRevision, reloaded.BaseDocumentRevision);
        Assert.Equal(captured.Scope.Time, reloaded.Scope.Time);
        Assert.Equal(captured.Scope.Models, reloaded.Scope.Models);
        Assert.Equal(captured.Scope.EffectIds, reloaded.Scope.EffectIds);
        Assert.Equal(captured.Scope.RespectLocks, reloaded.Scope.RespectLocks);
        Assert.Equal(instrumentTrackIds, reloaded.InstrumentTrackIds);
        Assert.Equal(phraseIds, reloaded.PhraseIds);
        Assert.Equal(spatialPathIds, reloaded.SpatialPathIds);
        Assert.Equal(noteIds, reloaded.NoteIds);
        Assert.Equal(captured.CapturedAtUtc, reloaded.CapturedAtUtc);
    }

    [Fact]
    public void StaleRevision_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
        {
            captureService.Capture(plan, new DocumentRevision(plan.Revision + 1), scope, [], [], [], []);
        });

        Assert.Equal(EditRejectionReason.StaleRevision, ex.Reason);
        // No selection-context file exists anywhere — the rejection fired before any snapshot, let alone
        // any persisted file, was ever constructed.
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void LockedEffect_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };
        var scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch2.Target });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
        {
            captureService.Capture(planWithLock, new DocumentRevision(planWithLock.Revision), scope, [], [], [], []);
        });

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
        Assert.Contains(arch2.Id.ToString(), ex.Message);
    }

    [Fact]
    public void NoPropsSelected_IsRejected_OutOfScope_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId>());
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
        {
            captureService.Capture(plan, new DocumentRevision(plan.Revision), scope, [], [], [], []);
        });

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
    }

    [Fact]
    public void EffectIdOutsideScopeModelSet_IsRejected_OutOfScope_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        // Scope's own model set is "Roofline" only, but the named effect id belongs to "Arch 1" — an
        // internally inconsistent scope.
        var scope = new EditScope(
            new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { roofline.Target }, new HashSet<Guid> { arch1.Id });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
        {
            captureService.Capture(plan, new DocumentRevision(plan.Revision), scope, [], [], [], []);
        });

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
    }

    [Fact]
    public void UnknownEffectId_IsRejected_OutOfScope_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var scope = new EditScope(
            new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target }, new HashSet<Guid> { Guid.NewGuid() });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
        {
            captureService.Capture(plan, new DocumentRevision(plan.Revision), scope, [], [], [], []);
        });

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
    }

    [Fact]
    public void InvertedInterval_IsRejected_InvalidDuration_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var scope = new EditScope(new TimeWindow(100, 100), new HashSet<ModelId> { arch1.Target });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());

        var ex = Assert.Throws<EditCommandRejectedException>(() =>
        {
            captureService.Capture(plan, new DocumentRevision(plan.Revision), scope, [], [], [], []);
        });

        Assert.Equal(EditRejectionReason.InvalidDuration, ex.Reason);
    }

    [Fact]
    public void Capture_NullArguments_Throw_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { new("Roofline") });
        var captureService = new SelectionContextCaptureService(new EffectLockPolicy());

        Assert.Throws<ArgumentNullException>(() =>
        {
            captureService.Capture(null!, new DocumentRevision(plan.Revision), scope, [], [], [], []);
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            captureService.Capture(plan, new DocumentRevision(plan.Revision), null!, [], [], [], []);
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            captureService.Capture(plan, new DocumentRevision(plan.Revision), scope, null!, [], [], []);
        });
    }

    [Fact]
    public async Task Load_MissingFile_ThrowsFileNotFoundException_NegativePath()
    {
        var store = new JsonSelectionContextStore(_root);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => store.LoadAsync(Path.Combine(_root, "selection-context-does-not-exist.json"), CancellationToken.None));
    }

    [Fact]
    public async Task Load_CorruptFile_ThrowsSelectionContextCorruptException_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "selection-context-corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json ", CancellationToken.None);
        var store = new JsonSelectionContextStore(_root);

        await Assert.ThrowsAsync<SelectionContextCorruptException>(
            () => store.LoadAsync(path, CancellationToken.None));
    }
}
