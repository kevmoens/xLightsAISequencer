using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 022 ("Undo/redo transactions and lock semantics for approved effects"). Required
/// acceptance: "Undo restores exact snapshot; locked effects reject programmatic edits." Implementation
/// guidance: "Locks enforced below ViewModel; undo/redo persisted as transactions and respects project
/// revision; test restart and stale writes." Required failure/edge case: "Out-of-scope, locked or
/// stale revision must be rejected without partial mutation." These tests exercise the real
/// <see cref="EditTransactionService"/>, <see cref="EffectLockPolicy"/>,
/// <see cref="JsonEditTransactionHistoryStore"/> against the gate demo's own exact layout shape
/// (Roofline, Arch 1, Arch 2 — matching every prior PHASE-03 step's own fixtures), not mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step022AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step022-" + Guid.NewGuid().ToString("N"));

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

    private static EditScope WholeDocumentScope(SequenceDocument document, bool respectLocks = true) =>
        new(
            new TimeWindow(0, long.MaxValue),
            document.Effects.Select(e => e.Target).ToHashSet(),
            RespectLocks: respectLocks);

    private static EditTransactionService NewService() => new(new EffectLockPolicy(), new EffectRecipeRegistry());

    /// <summary>
    /// Core gate-demo undo/redo: drag Arch 1 (move), resize Roofline, then undo both and redo both —
    /// the exact phase gate's own named sequence (Section 1: "drag Arch 1 effect, resize Roofline,
    /// undo/redo both operations"). Asserts the document is byte-for-byte/structurally identical to
    /// its own prior snapshot at every step, not just that no exception was thrown.
    /// </summary>
    [Fact]
    public async Task Step022_Acceptance_Passes_UndoRestoresExactSnapshot_ThenRedoReappliesExactSnapshot()
    {
        var layout = GateDemoLayout();
        var original = GateDemoPlan(layout);
        var service = NewService();
        var arch1 = original.Effects.Single(e => e.Target.Value == "Arch 1");
        var roofline = original.Effects.Single(e => e.Target.Value == "Roofline");

        // Drag Arch 1.
        var moveCommand = new MoveEffect(new DocumentRevision(original.Revision), WholeDocumentScope(original), arch1.Id, arch1.StartTicks + 1_000_000);
        var afterMove = await service.ApplyAsync(original, moveCommand, CancellationToken.None);

        // Resize Roofline.
        var newEnd = roofline.StartTicks + ((roofline.EndTicks - roofline.StartTicks) / 2);
        var resizeCommand = new ResizeEffect(new DocumentRevision(afterMove.Document.Revision), WholeDocumentScope(afterMove.Document), roofline.Id, newEnd);
        var afterResize = await service.ApplyAsync(afterMove.Document, resizeCommand, CancellationToken.None);

        Assert.Equal(original.Revision + 2, afterResize.Document.Revision);

        // Undo the resize: exact snapshot of afterMove.Document.
        var undoneResize = await service.UndoAsync(original.Id, CancellationToken.None);
        AssertSameDocument(afterMove.Document, undoneResize);

        // Undo the move: exact snapshot of the original document.
        var undoneMove = await service.UndoAsync(original.Id, CancellationToken.None);
        AssertSameDocument(original, undoneMove);

        // Redo the move: exact snapshot of afterMove.Document again.
        var redoneMove = await service.RedoAsync(original.Id, CancellationToken.None);
        AssertSameDocument(afterMove.Document, redoneMove);

        // Redo the resize: exact snapshot of afterResize.Document again.
        var redoneResize = await service.RedoAsync(original.Id, CancellationToken.None);
        AssertSameDocument(afterResize.Document, redoneResize);
    }

    /// <summary>"Locked effects reject programmatic edits" — proven directly against the gate demo's own "lock Arch 2, attempt forbidden modification" sequence.</summary>
    [Fact]
    public async Task Step022_Acceptance_Passes_LockedEffectRejectsProgrammaticEdit_ButUnlockedPeersStillEditable()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var service = NewService();
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");

        // Lock Arch 2.
        var lockCommand = new SetEffectLocked(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), arch2.Id, true);
        var afterLock = await service.ApplyAsync(plan, lockCommand, CancellationToken.None);
        Assert.True(afterLock.Document.Effects.Single(e => e.Id == arch2.Id).Locked);

        // Attempt forbidden modification.
        var forbiddenMove = new MoveEffect(new DocumentRevision(afterLock.Document.Revision), WholeDocumentScope(afterLock.Document), arch2.Id, arch2.StartTicks + 1_000_000);
        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(afterLock.Document, forbiddenMove, CancellationToken.None));
        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
        Assert.Equal(arch2.StartTicks, afterLock.Document.Effects.Single(e => e.Id == arch2.Id).StartTicks);

        // A different, unlocked effect is still editable — scoped to just that model, since a scope
        // reaching the still-locked Arch 2 would (correctly, per the existing EffectLockPolicy from
        // Steps 019/020) block the whole scope, not just an edit that targets Arch 2 itself.
        var roofline = afterLock.Document.Effects.Single(e => e.Target.Value == "Roofline");
        var rooflineOnlyScope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { roofline.Target });
        var moveRoofline = new MoveEffect(new DocumentRevision(afterLock.Document.Revision), rooflineOnlyScope, roofline.Id, roofline.StartTicks + 1_000_000);
        var afterMoveRoofline = await service.ApplyAsync(afterLock.Document, moveRoofline, CancellationToken.None);
        Assert.Equal(roofline.StartTicks + 1_000_000, afterMoveRoofline.Document.Effects.Single(e => e.Id == roofline.Id).StartTicks);
    }

    /// <summary>The lock toggle itself must not be gated by the lock it is setting — an already-locked effect must still be unlockable in one call.</summary>
    [Fact]
    public async Task LockedEffect_CanStillBeUnlockedByItsOwnLockToggleCommand()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var service = NewService();
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");

        var lockCommand = new SetEffectLocked(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), arch2.Id, true);
        var afterLock = await service.ApplyAsync(plan, lockCommand, CancellationToken.None);
        Assert.True(afterLock.Document.Effects.Single(e => e.Id == arch2.Id).Locked);

        var unlockCommand = new SetEffectLocked(new DocumentRevision(afterLock.Document.Revision), WholeDocumentScope(afterLock.Document), arch2.Id, false);
        var afterUnlock = await service.ApplyAsync(afterLock.Document, unlockCommand, CancellationToken.None);
        Assert.False(afterUnlock.Document.Effects.Single(e => e.Id == arch2.Id).Locked);

        // Now freely editable again.
        var move = new MoveEffect(new DocumentRevision(afterUnlock.Document.Revision), WholeDocumentScope(afterUnlock.Document), arch2.Id, arch2.StartTicks + 2_000_000);
        var afterMove = await service.ApplyAsync(afterUnlock.Document, move, CancellationToken.None);
        Assert.Equal(arch2.StartTicks + 2_000_000, afterMove.Document.Effects.Single(e => e.Id == arch2.Id).StartTicks);
    }

    [Fact]
    public async Task SetEffectLocked_StaleRevision_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var service = NewService();

        var command = new SetEffectLocked(new DocumentRevision(plan.Revision + 1), WholeDocumentScope(plan), arch2.Id, true);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.StaleRevision, ex.Reason);
        Assert.False(plan.Effects.Single(e => e.Id == arch2.Id).Locked);
    }

    [Fact]
    public async Task SetEffectLocked_OutOfScopeModel_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var service = NewService();

        var narrowScope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { new("Roofline") });
        var command = new SetEffectLocked(new DocumentRevision(plan.Revision), narrowScope, arch2.Id, true);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
    }

    [Fact]
    public async Task UndoWithNoAppliedEdits_IsRejected_NothingToUndo_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var service = NewService();

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.UndoAsync(plan.Id, CancellationToken.None));

        Assert.Equal(EditRejectionReason.NothingToUndo, ex.Reason);
    }

    [Fact]
    public async Task RedoWithNothingUndone_IsRejected_NothingToRedo_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = NewService();

        var move = new MoveEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), arch1.Id, arch1.StartTicks + 1_000_000);
        await service.ApplyAsync(plan, move, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.RedoAsync(plan.Id, CancellationToken.None));

        Assert.Equal(EditRejectionReason.NothingToRedo, ex.Reason);
    }

    /// <summary>
    /// The standard undo/redo correctness trap: undo an edit, then apply a brand-new, different edit
    /// instead of redoing — the pending redo entry (computed against the now-diverged document state)
    /// must be discarded, never silently reapplied later as a stale patch.
    /// </summary>
    [Fact]
    public async Task NewEditAfterUndo_ClearsRedoHistory_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var service = NewService();

        var moveArch1 = new MoveEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), arch1.Id, arch1.StartTicks + 1_000_000);
        var afterMoveArch1 = await service.ApplyAsync(plan, moveArch1, CancellationToken.None);

        var undone = await service.UndoAsync(plan.Id, CancellationToken.None);
        AssertSameDocument(plan, undone);

        // A new, different edit instead of redoing.
        var moveRoofline = new MoveEffect(new DocumentRevision(undone.Revision), WholeDocumentScope(undone), roofline.Id, roofline.StartTicks + 1_000_000);
        await service.ApplyAsync(undone, moveRoofline, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.RedoAsync(plan.Id, CancellationToken.None));
        Assert.Equal(EditRejectionReason.NothingToRedo, ex.Reason);
    }

    /// <summary>
    /// Undo/redo respects project revision end to end: a document loaded from the real
    /// <see cref="JsonSequenceDocumentStore"/> round trip still undoes to the exact prior snapshot
    /// (stable IDs/structural equality survive serialization, same guarantee Step 019/020 already
    /// proved for a single ApplyAsync round trip, extended here across an undo).
    /// </summary>
    [Fact]
    public async Task UndoAfterSaveReopenRoundTrip_StillRestoresExactSnapshot()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var service = NewService();
        var store = new JsonSequenceDocumentStore(_root);

        var savedOriginalPath = await store.SaveAsync(plan, CancellationToken.None);
        var reloadedOriginal = await store.LoadAsync(savedOriginalPath, CancellationToken.None);

        var move = new MoveEffect(new DocumentRevision(reloadedOriginal.Revision), WholeDocumentScope(reloadedOriginal), roofline.Id, roofline.StartTicks + 1_000_000);
        var afterMove = await service.ApplyAsync(reloadedOriginal, move, CancellationToken.None);

        var undone = await service.UndoAsync(reloadedOriginal.Id, CancellationToken.None);
        AssertSameDocument(reloadedOriginal, undone);
    }

    /// <summary>
    /// "Undo/redo history survives restart" (Section 4), proven for real via
    /// <see cref="JsonEditTransactionHistoryStore"/>: a brand-new <see cref="EditTransactionService"/>
    /// instance (standing in for "the app restarted" — a fresh process has no in-memory stack either)
    /// configured with the same on-disk history directory can still undo an edit applied by a prior,
    /// now-discarded service instance.
    /// </summary>
    [Fact]
    public async Task Step022_Acceptance_Passes_UndoHistorySurvivesRestart_ViaRealJsonHistoryStore()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var historyStore = new JsonEditTransactionHistoryStore(_root);

        // "Before restart": one service instance applies an edit.
        var serviceBeforeRestart = new EditTransactionService(new EffectLockPolicy(), new EffectRecipeRegistry(), historyStore);
        var move = new MoveEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), roofline.Id, roofline.StartTicks + 1_000_000);
        var afterMove = await serviceBeforeRestart.ApplyAsync(plan, move, CancellationToken.None);

        // "After restart": a brand-new service instance, pointed at the same real on-disk directory,
        // with no in-memory knowledge of the edit above.
        var serviceAfterRestart = new EditTransactionService(new EffectLockPolicy(), new EffectRecipeRegistry(), historyStore);
        var restoredAfterUndo = await serviceAfterRestart.UndoAsync(plan.Id, CancellationToken.None);

        AssertSameDocument(plan, restoredAfterUndo);

        // The redo side survived restart too.
        var redone = await serviceAfterRestart.RedoAsync(plan.Id, CancellationToken.None);
        AssertSameDocument(afterMove.Document, redone);
    }

    /// <summary>Without a configured history store, undo/redo history is honestly in-memory-only: a new service instance has nothing to undo for a document an earlier instance edited.</summary>
    [Fact]
    public async Task WithoutHistoryStore_UndoHistoryDoesNotSurviveANewServiceInstance_DisclosedGap()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");

        var serviceOne = NewService();
        var move = new MoveEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), roofline.Id, roofline.StartTicks + 1_000_000);
        await serviceOne.ApplyAsync(plan, move, CancellationToken.None);

        var serviceTwo = NewService();
        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => serviceTwo.UndoAsync(plan.Id, CancellationToken.None));
        Assert.Equal(EditRejectionReason.NothingToUndo, ex.Reason);
    }

    private static void AssertSameDocument(SequenceDocument expected, SequenceDocument actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.Effects.Count, actual.Effects.Count);
        foreach (var expectedEffect in expected.Effects)
        {
            var actualEffect = actual.Effects.Single(e => e.Id == expectedEffect.Id);
            Assert.Equal(expectedEffect.Target, actualEffect.Target);
            Assert.Equal(expectedEffect.Layer, actualEffect.Layer);
            Assert.Equal(expectedEffect.StartTicks, actualEffect.StartTicks);
            Assert.Equal(expectedEffect.EndTicks, actualEffect.EndTicks);
            Assert.Equal(expectedEffect.RecipeId, actualEffect.RecipeId);
            Assert.Equal(expectedEffect.RecipeVersion, actualEffect.RecipeVersion);
            Assert.Equal(expectedEffect.Locked, actualEffect.Locked);
            Assert.Equal(expectedEffect.Parameters, actualEffect.Parameters);
        }
    }
}
