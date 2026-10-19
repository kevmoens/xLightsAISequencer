using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 021 ("Multi-row time-range selection with inclusive/exclusive boundary semantics").
/// Required acceptance: "Selected model IDs and time range match UI; edge-case tests." Required
/// failure/edge case: "Out-of-scope, locked or stale revision must be rejected without partial
/// mutation." These tests exercise the real <see cref="TimelineRangeSelectionBuilder"/>/
/// <see cref="TimelineViewport"/>/<see cref="TimelineRowFlattener"/>, and — for the shared
/// out-of-scope/locked/stale rejection — the real <see cref="EditTransactionService"/>, against the
/// gate demo's own exact layout shape (Roofline, Arch 1, Arch 2 — matching
/// <c>Step019AcceptanceTests</c>/<c>Step020AcceptanceTests</c>/<c>Step018AcceptanceTests</c>), not
/// mocks.
/// </summary>
public sealed class Step021AcceptanceTests
{
    private static DisplayGraph GateDemoLayout() => new(
        "TEST-HASH-NOT-A-REAL-FILE",
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", [], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", [], []),
        ],
        [],
        []);

    private static SequenceDocument GateDemoPlan(DisplayGraph layout) =>
        new ThreeEffectPlanFactory().CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], 300_000_000, new EffectRecipeRegistry());

    private static IReadOnlyList<TimelineRowSlot> VisibleRows(DisplayGraph layout) =>
        TimelineRowFlattener.Flatten(TimelineRowTreeBuilder.Build(layout).Roots, new HashSet<TimelineRowId>());

    private static TimelineViewport SmallViewport(TimeWindow bounds) =>
        TimelineViewport.Create(bounds, TimelineViewport.MinTicksPerPixel);

    [Fact]
    public void Step021_Acceptance_Passes_SelectedModelIdsAndTimeRangeMatchExactDragRectangle()
    {
        var layout = GateDemoLayout();
        var rows = VisibleRows(layout); // Roofline, Arch 1, Arch 2 — all ungrouped model rows.
        var viewport = SmallViewport(new TimeWindow(0, 10_000_000));

        var startPixel = 10d;
        var endPixel = 100d;
        var scope = TimelineRangeSelectionBuilder.Build(rows, rowIndexA: 0, rowIndexB: 1, pixelA: startPixel, pixelB: endPixel, viewport);

        Assert.Equal(new HashSet<ModelId> { new("Roofline"), new("Arch 1") }, scope.Models);
        Assert.Equal(viewport.TimeFromX(startPixel), scope.Time.StartTicks);
        Assert.Equal(viewport.TimeFromX(endPixel), scope.Time.EndTicks);
        Assert.Null(scope.EffectIds);
    }

    [Fact]
    public void ReversedDrag_TopToBottomAndBottomToTop_ProduceIdenticalScope()
    {
        var layout = GateDemoLayout();
        var rows = VisibleRows(layout);
        var viewport = SmallViewport(new TimeWindow(0, 10_000_000));

        var forward = TimelineRangeSelectionBuilder.Build(rows, 0, 2, 50d, 300d, viewport);
        var reversed = TimelineRangeSelectionBuilder.Build(rows, 2, 0, 300d, 50d, viewport);

        Assert.Equal(forward.Models, reversed.Models);
        Assert.Equal(forward.Time, reversed.Time);
    }

    /// <summary>
    /// This step's own title: "inclusive/exclusive boundary semantics" — a half-open
    /// <c>[Start, End)</c> scope (PHASE-01 Section 2's canonical rule) reached through the real
    /// <see cref="EffectLockPolicy"/> overlap test. An effect starting exactly at the selection's
    /// <c>EndTicks</c> is outside the window (exclusive end) and is therefore NOT "reached" by the
    /// scope — a locked effect sitting exactly on that boundary is not protected by this scope, while
    /// one starting at the scope's own inclusive <c>StartTicks</c> is.
    /// </summary>
    [Fact]
    public void HalfOpenBoundary_EndExclusive_EffectExactlyAtEndTicksIsNotReachedByScope()
    {
        var layout = GateDemoLayout();
        var rows = VisibleRows(layout);
        var viewport = SmallViewport(new TimeWindow(0, 10_000_000));

        var scope = TimelineRangeSelectionBuilder.Build(rows, 0, 0, 0d, 100d, viewport);
        Assert.True(scope.Time.StartTicks < scope.Time.EndTicks);

        var insideEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Roofline"), 0, scope.Time.StartTicks, scope.Time.StartTicks + 1, "r", "v", new Dictionary<string, string>(), Locked: true);
        var onBoundaryEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Roofline"), 0, scope.Time.EndTicks, scope.Time.EndTicks + 1_000, "r", "v", new Dictionary<string, string>(), Locked: true);
        var document = new SequenceDocument(Guid.NewGuid(), 0, [insideEffect, onBoundaryEffect]);
        var policy = new EffectLockPolicy();

        // The inside effect (starts exactly at the scope's own inclusive StartTicks) IS reached —
        // AssertEditable throws because it is locked and in range.
        var insideOnlyDocument = document with { Effects = [insideEffect] };
        Assert.Throws<EditCommandRejectedException>(() => policy.AssertEditable(insideOnlyDocument, scope));

        // The boundary effect (starts exactly at the scope's own exclusive EndTicks) is NOT reached —
        // AssertEditable does not throw even though it, too, is locked.
        var boundaryOnlyDocument = document with { Effects = [onBoundaryEffect] };
        policy.AssertEditable(boundaryOnlyDocument, scope); // Does not throw.
    }

    [Fact]
    public void SelectionAcrossNonadjacentRows_IncludesOnlyRowsActuallyInRange_NeverInterpolated()
    {
        // Front-Yard (group) / Arch-01 (model) / Spot-Group (group, collapsed) / MegaTree (model).
        var arch1 = new DisplayModel(new ModelId("Arch-01"), "Arch-01", "Arch", ["Front-Yard"], []);
        var arch2 = new DisplayModel(new ModelId("Arch-02"), "Arch-02", "Arch", ["Spot-Group"], []);
        var megaTree = new DisplayModel(new ModelId("MegaTree"), "MegaTree", "Tree", [], []);
        var spotGroup = new DisplayModelGroup("Spot-Group", [new DisplayGroupMember("Arch-02", DisplayGroupMemberKind.Model)]);
        var frontYard = new DisplayModelGroup("Front-Yard",
        [
            new DisplayGroupMember("Arch-01", DisplayGroupMemberKind.Model),
            new DisplayGroupMember("Spot-Group", DisplayGroupMemberKind.Group),
        ]);
        var layout = new DisplayGraph("hash", [arch1, arch2, megaTree], [spotGroup, frontYard], []);

        // Collapse Spot-Group: visible rows become [Front-Yard(group), Arch-01, Spot-Group(group), MegaTree].
        var tree = TimelineRowTreeBuilder.Build(layout);
        var rows = TimelineRowFlattener.Flatten(tree.Roots, new HashSet<TimelineRowId> { TimelineRowId.ForGroup("Spot-Group") });
        Assert.Equal(4, rows.Count); // Arch-02 is NOT in this list at all — it is hidden under the collapsed group.

        var viewport = SmallViewport(new TimeWindow(0, 10_000_000));

        // Drag across every visible row (indices 0..3): must include Arch-01 and MegaTree (the two
        // real model rows actually visible), the two group rows contribute nothing, and — critically —
        // Arch-02 (hidden inside the collapsed group) must NOT be silently swept in just because the
        // drag rectangle visually passed over its collapsed ancestor.
        var scope = TimelineRangeSelectionBuilder.Build(rows, 0, 3, 0d, 50d, viewport);

        Assert.Equal(new HashSet<ModelId> { new("Arch-01"), new("MegaTree") }, scope.Models);
        Assert.DoesNotContain(new ModelId("Arch-02"), scope.Models);
    }

    [Fact]
    public void RowIndicesOutOfBounds_ClampSafely_NegativePath()
    {
        var layout = GateDemoLayout();
        var rows = VisibleRows(layout);
        var viewport = SmallViewport(new TimeWindow(0, 10_000_000));

        // Both far outside the real [0, rows.Count) range — must clamp, never throw.
        var scope = TimelineRangeSelectionBuilder.Build(rows, -50, 999, 0d, 50d, viewport);

        Assert.Equal(new HashSet<ModelId> { new("Roofline"), new("Arch 1"), new("Arch 2") }, scope.Models);
    }

    [Fact]
    public void EmptyRowList_ProducesEmptyScope_SilenceEdgeCase()
    {
        var viewport = SmallViewport(new TimeWindow(0, 10_000_000));

        var scope = TimelineRangeSelectionBuilder.Build([], 0, 0, 0d, 100d, viewport);

        Assert.Empty(scope.Models);
    }

    /// <summary>
    /// The real tie-in: a scope built from a drag that deliberately excludes "Arch 2"'s row must
    /// reject any edit attempt against an "Arch 2" effect through it — proving this step's own scope
    /// construction, not just the already-PASS command layer, is the thing enforcing "out-of-scope...
    /// must be rejected without partial mutation."
    /// </summary>
    [Fact]
    public async Task DragExcludingARow_EffectOnThatRow_IsRejectedOutOfScope_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var rows = VisibleRows(layout); // [Roofline, Arch 1, Arch 2]
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 1_000_000));

        // Drag only across rows 0..1 (Roofline, Arch 1) — Arch 2's row is deliberately excluded.
        var scope = TimelineRangeSelectionBuilder.Build(rows, 0, 1, 0d, viewport.XFromTime(plan.Effects.Max(e => e.EndTicks)), viewport);
        Assert.DoesNotContain(new ModelId("Arch 2"), scope.Models);

        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var command = new MoveEffect(new DocumentRevision(plan.Revision), scope, arch2.Id, arch2.StartTicks + 1_000_000);
        var service = new EditTransactionService(new EffectLockPolicy());

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
        Assert.Equal(arch2.StartTicks, plan.Effects.Single(e => e.Id == arch2.Id).StartTicks);
    }

    /// <summary>A scope built across every row, with a stale expected revision, is still rejected — the scope itself is correct, but the document moved on.</summary>
    [Fact]
    public async Task ScopeCoversRow_ButRevisionIsStale_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var rows = VisibleRows(layout);
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 1_000_000));
        var scope = TimelineRangeSelectionBuilder.Build(rows, 0, 2, 0d, viewport.XFromTime(plan.Effects.Max(e => e.EndTicks)), viewport);

        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var command = new MoveEffect(new DocumentRevision(plan.Revision + 1), scope, roofline.Id, roofline.StartTicks + 1_000_000);
        var service = new EditTransactionService(new EffectLockPolicy());

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.StaleRevision, ex.Reason);
    }

    /// <summary>A scope built with a row range that includes a locked effect's row still has the lock enforced — selection scope never bypasses locks.</summary>
    [Fact]
    public async Task ScopeCoversLockedEffectRow_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedPlan = plan with { Effects = plan.Effects.Select(e => e.Id == arch2.Id ? e with { Locked = true } : e).ToList() };
        var rows = VisibleRows(layout);
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 1_000_000));
        var scope = TimelineRangeSelectionBuilder.Build(rows, 0, 2, 0d, viewport.XFromTime(plan.Effects.Max(e => e.EndTicks)), viewport);

        var command = new MoveEffect(new DocumentRevision(lockedPlan.Revision), scope, arch2.Id, arch2.StartTicks + 1_000_000);
        var service = new EditTransactionService(new EffectLockPolicy());

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(lockedPlan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
    }
}
