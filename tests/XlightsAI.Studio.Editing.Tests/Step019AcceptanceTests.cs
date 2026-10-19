using System.Xml.Linq;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 019 ("Drag effect starts with optional snap"). Required acceptance: "Start time
/// persists after save/reopen and XSQ export." Required failure/edge case: "Out-of-scope, locked or
/// stale revision must be rejected without partial mutation." These tests exercise the real
/// <see cref="EditTransactionService"/>, <see cref="EffectLockPolicy"/>, <see cref="JsonSequenceDocumentStore"/>
/// and <see cref="XsqExporter"/> against the gate demo's own exact layout shape (Roofline, Arch 1,
/// Arch 2 — matching <c>Step012AcceptanceTests</c>/<c>Step015AcceptanceTests</c>), not mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step019AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step019-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>The gate demo's own layout shape (PHASE-02 Section 1: Roofline, Arch 1, Arch 2).</summary>
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

    /// <summary>
    /// Core gate-demo move: drag "Arch 1" to a new, snapped start time. Required acceptance:
    /// the new start time (a) is reflected in the returned document/transaction, (b) survives a real
    /// save/reopen round trip through <see cref="JsonSequenceDocumentStore"/>, and (c) survives a real
    /// XSQ export — while every other effect's exported start/end stays byte-for-byte identical
    /// (the phase gate's own "verify other effects stay... semantically identical" requirement).
    /// </summary>
    [Fact]
    public async Task Step019_Acceptance_Passes_MovedStartTimePersistsAfterSaveReopenAndXsqExport()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = new EditTransactionService(new EffectLockPolicy());

        // Optional snap: drag lands on a slightly-off tick, caller snaps to a 10,000,000-tick (1s) grid
        // before constructing the command (Section 3: snapping is a UI-gesture concern, not implicit).
        var requestedTicks = arch1.StartTicks + 24_300_000;
        var snappedTicks = EditTimeSnapping.SnapTicks(requestedTicks, snapIntervalTicks: 10_000_000);
        Assert.Equal(arch1.StartTicks + 20_000_000, snappedTicks); // nearest whole second, confirms snap actually rounded.

        var command = new MoveEffect(
            new DocumentRevision(plan.Revision),
            WholeDocumentScope(plan),
            arch1.Id,
            snappedTicks);

        var result = await service.ApplyAsync(plan, command, CancellationToken.None);

        var movedEffect = result.Document.Effects.Single(e => e.Id == arch1.Id);
        var originalDuration = arch1.EndTicks - arch1.StartTicks;
        Assert.Equal(snappedTicks, movedEffect.StartTicks);
        Assert.Equal(snappedTicks + originalDuration, movedEffect.EndTicks); // duration preserved exactly.
        Assert.Equal(plan.Revision + 1, result.Document.Revision);
        Assert.Equal(plan.Revision, result.Transaction.BeforeRevision);
        Assert.Equal(result.Document.Revision, result.Transaction.AfterRevision);
        Assert.Contains(arch1.StartTicks.ToString(), result.Transaction.SerializedBeforePatch);
        Assert.Contains(snappedTicks.ToString(), result.Transaction.SerializedAfterPatch);

        // Every other effect is untouched in-memory already (immutable records); assert explicitly.
        foreach (var other in result.Document.Effects.Where(e => e.Id != arch1.Id))
        {
            var original = plan.Effects.Single(e => e.Id == other.Id);
            Assert.Equal(original.StartTicks, other.StartTicks);
            Assert.Equal(original.EndTicks, other.EndTicks);
        }

        // (a) Save/reopen round trip.
        var store = new JsonSequenceDocumentStore(_root);
        var savedPath = await store.SaveAsync(result.Document, CancellationToken.None);
        var reloaded = await store.LoadAsync(savedPath, CancellationToken.None);
        Assert.Equal(snappedTicks, reloaded.Effects.Single(e => e.Id == arch1.Id).StartTicks);

        // (b) Real XSQ export: the moved effect's exported startTime/endTime reflect the move; every
        // other effect's exported startTime/endTime is unchanged from an export of the original plan.
        var movedExportPath = Path.Combine(_root, "moved.xsq");
        var originalExportPath = Path.Combine(_root, "original.xsq");
        await new XsqExporter().ExportAsync(layout, reloaded, movedExportPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);
        await new XsqExporter().ExportAsync(layout, plan, originalExportPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        var movedXml = XDocument.Load(movedExportPath);
        var originalXml = XDocument.Load(originalExportPath);

        var movedArch1Effect = FindEffect(movedXml, "Arch 1");
        var originalArch1Effect = FindEffect(originalXml, "Arch 1");
        Assert.NotEqual(originalArch1Effect.Attribute("startTime")!.Value, movedArch1Effect.Attribute("startTime")!.Value);

        foreach (var targetName in new[] { "Roofline", "Arch 2" })
        {
            var movedOther = FindEffect(movedXml, targetName);
            var originalOther = FindEffect(originalXml, targetName);
            Assert.Equal(originalOther.Attribute("startTime")!.Value, movedOther.Attribute("startTime")!.Value);
            Assert.Equal(originalOther.Attribute("endTime")!.Value, movedOther.Attribute("endTime")!.Value);
        }
    }

    private static XElement FindEffect(XDocument document, string targetName) =>
        document.Root!
            .Element("ElementEffects")!
            .Elements("Element")
            .Single(e => e.Attribute("name")!.Value == targetName)
            .Descendants("Effect")
            .Single();

    [Fact]
    public async Task StaleRevision_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = new EditTransactionService(new EffectLockPolicy());

        var staleCommand = new MoveEffect(
            new DocumentRevision(plan.Revision + 1), // wrong on purpose.
            WholeDocumentScope(plan),
            arch1.Id,
            arch1.StartTicks + 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, staleCommand, CancellationToken.None));

        Assert.Equal(EditRejectionReason.StaleRevision, ex.Reason);
        // The original plan instance is provably untouched: it is an immutable record, and the
        // exception fired before any replacement document/transaction was even constructed.
        Assert.Equal(arch1.StartTicks, plan.Effects.Single(e => e.Id == arch1.Id).StartTicks);
    }

    [Fact]
    public async Task LockedEffect_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };
        var service = new EditTransactionService(new EffectLockPolicy());

        var command = new MoveEffect(
            new DocumentRevision(planWithLock.Revision),
            WholeDocumentScope(planWithLock),
            arch2.Id,
            arch2.StartTicks + 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(planWithLock, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
        Assert.Contains(arch2.Id.ToString(), ex.Message);
    }

    [Fact]
    public async Task EffectOutsideScopeModelSet_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = new EditTransactionService(new EffectLockPolicy());

        // Scope only grants "Roofline" — Arch 1 is explicitly out of scope.
        var narrowScope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { new("Roofline") });
        var command = new MoveEffect(new DocumentRevision(plan.Revision), narrowScope, arch1.Id, arch1.StartTicks + 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
    }

    [Fact]
    public async Task MoveOutsideScopeTimeWindow_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = new EditTransactionService(new EffectLockPolicy());

        // Scope's time window ends well before the requested new position.
        var narrowScope = new EditScope(new TimeWindow(0, arch1.EndTicks), new HashSet<ModelId> { arch1.Target });
        var command = new MoveEffect(new DocumentRevision(plan.Revision), narrowScope, arch1.Id, 290_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
    }

    [Fact]
    public async Task UnknownEffectId_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var service = new EditTransactionService(new EffectLockPolicy());

        var command = new MoveEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), Guid.NewGuid(), 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.EffectNotFound, ex.Reason);
    }

    /// <summary>Implementation guidance: "clamp negative times" — a requested negative start time clamps to 0 rather than being rejected, and duration is still preserved exactly.</summary>
    [Fact]
    public async Task NegativeRequestedStartTime_ClampsToZero_PreservesDuration()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var duration = roofline.EndTicks - roofline.StartTicks;
        var service = new EditTransactionService(new EffectLockPolicy());

        var command = new MoveEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), roofline.Id, -5_000_000);

        var result = await service.ApplyAsync(plan, command, CancellationToken.None);

        var moved = result.Document.Effects.Single(e => e.Id == roofline.Id);
        Assert.Equal(0, moved.StartTicks);
        Assert.Equal(duration, moved.EndTicks - moved.StartTicks);
    }

    [Theory]
    [InlineData(0L, null)]
    [InlineData(1_234_567L, null)]
    [InlineData(1_234_567L, 0L)]
    [InlineData(1_234_567L, -1L)]
    public void SnapTicks_WithNoOrInvalidInterval_ReturnsUnchanged(long ticks, long? interval)
    {
        Assert.Equal(ticks, EditTimeSnapping.SnapTicks(ticks, interval));
    }

    [Fact]
    public void SnapTicks_RoundsToNearestInterval()
    {
        Assert.Equal(10_000_000, EditTimeSnapping.SnapTicks(14_000_000, 10_000_000));
        Assert.Equal(20_000_000, EditTimeSnapping.SnapTicks(16_000_000, 10_000_000));
    }
}
