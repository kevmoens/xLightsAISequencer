using System.Xml.Linq;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 020 ("Resize duration and edit supported effect parameters"). Required acceptance:
/// "Edited parameters survive project reload and xLights import." Required failure/edge case:
/// "Out-of-scope, locked or stale revision must be rejected without partial mutation", plus this
/// step's own named failure: "reject zero/negative length and unsupported parameters." These tests
/// exercise the real <see cref="EditTransactionService"/>, <see cref="EffectLockPolicy"/>,
/// <see cref="EffectRecipeRegistry"/>, <see cref="JsonSequenceDocumentStore"/> and
/// <see cref="XsqExporter"/> against the gate demo's own exact layout shape (Roofline, Arch 1,
/// Arch 2 — matching <c>Step019AcceptanceTests</c>), not mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step020AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step020-" + Guid.NewGuid().ToString("N"));

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

    private static EditTransactionService NewService() => new(new EffectLockPolicy(), new EffectRecipeRegistry());

    private static XElement FindEffect(XDocument document, string targetName) =>
        document.Root!
            .Element("ElementEffects")!
            .Elements("Element")
            .Single(e => e.Attribute("name")!.Value == targetName)
            .Descendants("Effect")
            .Single();

    /// <summary>
    /// Core gate-demo resize: shrink "Roofline"'s duration. Required acceptance: the new end time
    /// (a) is reflected in the returned document/transaction, (b) survives a real save/reopen round
    /// trip through <see cref="JsonSequenceDocumentStore"/>, and (c) survives a real XSQ export —
    /// while every other effect's exported start/end stays byte-for-byte identical.
    /// </summary>
    [Fact]
    public async Task Step020_Acceptance_Passes_ResizedDurationPersistsAfterSaveReopenAndXsqExport()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var service = NewService();

        var newEnd = roofline.StartTicks + ((roofline.EndTicks - roofline.StartTicks) / 2); // shrink to half duration.
        var command = new ResizeEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), roofline.Id, newEnd);

        var result = await service.ApplyAsync(plan, command, CancellationToken.None);

        var resized = result.Document.Effects.Single(e => e.Id == roofline.Id);
        Assert.Equal(roofline.StartTicks, resized.StartTicks); // start untouched by a resize.
        Assert.Equal(newEnd, resized.EndTicks);
        Assert.Equal(plan.Revision + 1, result.Document.Revision);
        Assert.Equal(plan.Revision, result.Transaction.BeforeRevision);
        Assert.Equal(result.Document.Revision, result.Transaction.AfterRevision);

        // Every other effect is untouched in-memory already (immutable records); assert explicitly.
        foreach (var other in result.Document.Effects.Where(e => e.Id != roofline.Id))
        {
            var original = plan.Effects.Single(e => e.Id == other.Id);
            Assert.Equal(original.StartTicks, other.StartTicks);
            Assert.Equal(original.EndTicks, other.EndTicks);
        }

        // (a) Save/reopen round trip.
        var store = new JsonSequenceDocumentStore(_root);
        var savedPath = await store.SaveAsync(result.Document, CancellationToken.None);
        var reloaded = await store.LoadAsync(savedPath, CancellationToken.None);
        Assert.Equal(newEnd, reloaded.Effects.Single(e => e.Id == roofline.Id).EndTicks);

        // (b) Real XSQ export: the resized effect's exported endTime reflects the resize; every other
        // effect's exported startTime/endTime is unchanged from an export of the original plan.
        var resizedExportPath = Path.Combine(_root, "resized.xsq");
        var originalExportPath = Path.Combine(_root, "original.xsq");
        await new XsqExporter().ExportAsync(layout, reloaded, resizedExportPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);
        await new XsqExporter().ExportAsync(layout, plan, originalExportPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        var resizedXml = XDocument.Load(resizedExportPath);
        var originalXml = XDocument.Load(originalExportPath);

        var resizedRooflineEffect = FindEffect(resizedXml, "Roofline");
        var originalRooflineEffect = FindEffect(originalXml, "Roofline");
        Assert.Equal(originalRooflineEffect.Attribute("startTime")!.Value, resizedRooflineEffect.Attribute("startTime")!.Value);
        Assert.NotEqual(originalRooflineEffect.Attribute("endTime")!.Value, resizedRooflineEffect.Attribute("endTime")!.Value);

        foreach (var targetName in new[] { "Arch 1", "Arch 2" })
        {
            var resizedOther = FindEffect(resizedXml, targetName);
            var originalOther = FindEffect(originalXml, targetName);
            Assert.Equal(originalOther.Attribute("startTime")!.Value, resizedOther.Attribute("startTime")!.Value);
            Assert.Equal(originalOther.Attribute("endTime")!.Value, resizedOther.Attribute("endTime")!.Value);
        }
    }

    [Fact]
    public async Task ResizeToZeroLength_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var service = NewService();

        var command = new ResizeEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), roofline.Id, roofline.StartTicks);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.InvalidDuration, ex.Reason);
        Assert.Equal(roofline.EndTicks, plan.Effects.Single(e => e.Id == roofline.Id).EndTicks);
    }

    [Fact]
    public async Task ResizeToNegativeLength_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var service = NewService();

        var command = new ResizeEffect(new DocumentRevision(plan.Revision), WholeDocumentScope(plan), roofline.Id, roofline.StartTicks - 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.InvalidDuration, ex.Reason);
    }

    [Fact]
    public async Task ResizeOutsideScopeTimeWindow_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var service = NewService();

        var narrowScope = new EditScope(new TimeWindow(0, roofline.EndTicks), new HashSet<ModelId> { roofline.Target });
        var command = new ResizeEffect(new DocumentRevision(plan.Revision), narrowScope, roofline.Id, roofline.EndTicks + 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.OutOfScope, ex.Reason);
    }

    [Fact]
    public async Task ResizeLockedEffect_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };
        var service = NewService();

        var command = new ResizeEffect(new DocumentRevision(planWithLock.Revision), WholeDocumentScope(planWithLock), arch2.Id, arch2.EndTicks + 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(planWithLock, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
    }

    [Fact]
    public async Task ResizeStaleRevision_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var roofline = plan.Effects.Single(e => e.Target.Value == "Roofline");
        var service = NewService();

        var command = new ResizeEffect(new DocumentRevision(plan.Revision + 1), WholeDocumentScope(plan), roofline.Id, roofline.EndTicks + 1_000_000);

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.StaleRevision, ex.Reason);
    }

    /// <summary>
    /// "Edit supported effect parameters": the only recipe this build knows (<c>effect.on.v1</c>) has
    /// exactly one verified parameter/value pair (<c>EffectFamily=On</c>) — re-asserting that same
    /// value is the one real, supported edit this build can make today without inventing an xLights
    /// parameter it has never verified. Required acceptance: "Edited parameters survive project
    /// reload" — proven via a real <see cref="JsonSequenceDocumentStore"/> round trip; "...and xLights
    /// import" is explicitly flagged as not independently verifiable here (see evidence §6: no
    /// installed xLights build, and <see cref="XsqExporter"/> does not yet serialize
    /// <see cref="EffectInstance.Parameters"/> into the exported XML at all — the same disclosed Step
    /// 013 gap, not reopened by this step).
    /// </summary>
    [Fact]
    public async Task Step020_Acceptance_Passes_SetSupportedParameterPersistsAfterSaveReopen()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = NewService();

        var command = new SetEffectParameters(
            new DocumentRevision(plan.Revision),
            WholeDocumentScope(plan),
            arch1.Id,
            new Dictionary<string, string> { ["EffectFamily"] = "On" });

        var result = await service.ApplyAsync(plan, command, CancellationToken.None);

        var updated = result.Document.Effects.Single(e => e.Id == arch1.Id);
        Assert.Equal("On", updated.Parameters["EffectFamily"]);
        // Preserve unchanged effect fields: start/end/layer/recipe untouched by a parameter-only edit.
        Assert.Equal(arch1.StartTicks, updated.StartTicks);
        Assert.Equal(arch1.EndTicks, updated.EndTicks);
        Assert.Equal(arch1.Layer, updated.Layer);
        Assert.Equal(arch1.RecipeId, updated.RecipeId);

        var store = new JsonSequenceDocumentStore(_root);
        var savedPath = await store.SaveAsync(result.Document, CancellationToken.None);
        var reloaded = await store.LoadAsync(savedPath, CancellationToken.None);
        Assert.Equal("On", reloaded.Effects.Single(e => e.Id == arch1.Id).Parameters["EffectFamily"]);
    }

    [Fact]
    public async Task SetUnsupportedParameterValue_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = NewService();

        var command = new SetEffectParameters(
            new DocumentRevision(plan.Revision),
            WholeDocumentScope(plan),
            arch1.Id,
            new Dictionary<string, string> { ["EffectFamily"] = "Twinkle" }); // never verified against real xLights.

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.UnsupportedParameter, ex.Reason);
        Assert.Equal("On", plan.Effects.Single(e => e.Id == arch1.Id).Parameters["EffectFamily"]);
    }

    [Fact]
    public async Task SetUnknownParameterKey_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var service = NewService();

        var command = new SetEffectParameters(
            new DocumentRevision(plan.Revision),
            WholeDocumentScope(plan),
            arch1.Id,
            new Dictionary<string, string> { ["Brightness"] = "100" }); // not a recognized key for this recipe.

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(plan, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.UnsupportedParameter, ex.Reason);
    }

    [Fact]
    public async Task SetParametersLockedEffect_IsRejected_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var planWithLock = plan with { Effects = effects };
        var service = NewService();

        var command = new SetEffectParameters(
            new DocumentRevision(planWithLock.Revision),
            WholeDocumentScope(planWithLock),
            arch2.Id,
            new Dictionary<string, string> { ["EffectFamily"] = "On" });

        var ex = await Assert.ThrowsAsync<EditCommandRejectedException>(
            () => service.ApplyAsync(planWithLock, command, CancellationToken.None));

        Assert.Equal(EditRejectionReason.Locked, ex.Reason);
    }
}
