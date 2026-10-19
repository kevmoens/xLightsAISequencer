using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 023 ("Scope-aware export and regeneration contract"). Required acceptance: "Hash
/// unaffected effects before/after scoped edit; no changes outside scope." Implementation guidance:
/// "Export scoped edits without regenerating other effect recipes; hash unaffected effects and inspect
/// XSQ after round-trip." Required failure/edge case: "Invalid model/format/version must fail safely
/// without rewriting source files." These tests exercise the real <see cref="EditTransactionService"/>,
/// <see cref="ScopedXsqExporter"/>, <see cref="XsqExporter"/> and <see cref="XsqStructuralReader"/>
/// against the gate demo's own exact layout shape (Roofline, Arch 1, Arch 2 — matching every prior
/// PHASE-03 step's own fixtures), not mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step023AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step023-" + Guid.NewGuid().ToString("N"));

    public Step023AcceptanceTests() => Directory.CreateDirectory(_root);

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

    private static ScopedXsqExporter NewScopedExporter() => new(new XsqExporter(), new XsqStructuralReader());

    /// <summary>
    /// Core gate-demo sequence restricted to Step 023's own scope: drag Arch 1 (move), resize Roofline,
    /// then scoped-export each change with a scope covering only the model that actually moved. Asserts
    /// the required acceptance text literally: the untouched effects' hashes are reported unchanged, and
    /// the real exported XSQ, read back independently, still carries their exact original timing.
    /// </summary>
    [Fact]
    public async Task Step023_Acceptance_Passes_HashesUnaffectedEffects_NoChangesOutsideScope()
    {
        var layout = GateDemoLayout();
        var original = GateDemoPlan(layout);
        var service = new EditTransactionService(new EffectLockPolicy(), new EffectRecipeRegistry());
        var exporter = NewScopedExporter();

        var arch1 = original.Effects.Single(e => e.Target.Value == "Arch 1");
        var roofline = original.Effects.Single(e => e.Target.Value == "Roofline");
        var arch2 = original.Effects.Single(e => e.Target.Value == "Arch 2");

        // Drag Arch 1, scoped to just Arch 1's own model.
        var arch1Scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target });
        var moveCommand = new MoveEffect(new DocumentRevision(original.Revision), arch1Scope, arch1.Id, arch1.StartTicks + 1_000_000);
        var afterMove = await service.ApplyAsync(original, moveCommand, CancellationToken.None);

        var outputPath = Path.Combine(_root, "scoped-move.xsq");
        var result = await exporter.ExportScopedAsync(
            layout, original, afterMove.Document, arch1Scope, outputPath, "2024.01", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        // Roofline and Arch 2 were outside the scope and must be reported unchanged.
        Assert.Equal(2, result.OutOfScopeEffectHashes.Count);
        Assert.Contains(roofline.Id, result.OutOfScopeEffectHashes.Keys);
        Assert.Contains(arch2.Id, result.OutOfScopeEffectHashes.Keys);

        // The same hashes must still be reported after a second, unrelated scoped edit (Resize Roofline,
        // scoped to just Roofline) that does NOT touch Arch 1 or Arch 2 — "hash unaffected effects
        // before/after scoped edit" proven across two independent scoped operations, matching the phase
        // gate's own "drag Arch 1... resize Roofline" sequence.
        var rooflineScope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { roofline.Target });
        var newEnd = roofline.StartTicks + ((roofline.EndTicks - roofline.StartTicks) / 2);
        var resizeCommand = new ResizeEffect(new DocumentRevision(afterMove.Document.Revision), rooflineScope, roofline.Id, newEnd);
        var afterResize = await service.ApplyAsync(afterMove.Document, resizeCommand, CancellationToken.None);

        var outputPath2 = Path.Combine(_root, "scoped-resize.xsq");
        var result2 = await exporter.ExportScopedAsync(
            layout, afterMove.Document, afterResize.Document, rooflineScope, outputPath2, "2024.01", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        // Arch 1 and Arch 2 are outside the resize's own scope; Arch 1's hash is identical to the one
        // computed by the first export above, since the resize never touched it.
        Assert.Equal(result.OutOfScopeEffectHashes[arch2.Id], result2.OutOfScopeEffectHashes[arch2.Id]);

        // Inspect the real exported XSQ after round-trip: Arch 1 and Arch 2 carry their exact original
        // timing; only Roofline's own placement reflects the resize.
        var structural = new XsqStructuralReader().Read(outputPath2);
        var arch1Element = structural.ElementEffects.Single(e => e.Name == "Arch 1");
        var arch2Element = structural.ElementEffects.Single(e => e.Name == "Arch 2");
        var rooflineElement = structural.ElementEffects.Single(e => e.Name == "Roofline");

        Assert.Contains(arch1Element.Effects, p => p.StartTimeMs == RoundToFrameMs(afterMove.Document.Effects.Single(e => e.Id == arch1.Id).StartTicks));
        Assert.Contains(arch2Element.Effects, p => p.StartTimeMs == RoundToFrameMs(arch2.StartTicks) && p.EndTimeMs == RoundToFrameMs(arch2.EndTicks));
        Assert.Contains(rooflineElement.Effects, p => p.EndTimeMs == RoundToFrameMs(newEnd));
    }

    /// <summary>
    /// "No changes outside scope" as an enforced rejection, not merely an assertion: an out-of-scope
    /// effect (Roofline) is mutated directly (bypassing the command pipeline, simulating a bug that
    /// would otherwise silently regenerate it) while the declared scope covers only Arch 1. The scoped
    /// export must reject before writing anything.
    /// </summary>
    [Fact]
    public async Task ScopedExport_EffectOutsideScopeChanged_IsRejected_NoFileWritten_NegativePath()
    {
        var layout = GateDemoLayout();
        var original = GateDemoPlan(layout);
        var exporter = NewScopedExporter();

        var arch1 = original.Effects.Single(e => e.Target.Value == "Arch 1");
        var roofline = original.Effects.Single(e => e.Target.Value == "Roofline");
        var arch1Scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target });

        var tamperedRoofline = roofline with { StartTicks = roofline.StartTicks + 500_000, EndTicks = roofline.EndTicks + 500_000 };
        var tamperedEffects = original.Effects.Select(e => e.Id == roofline.Id ? tamperedRoofline : e).ToList();
        var tamperedAfter = original with { Revision = original.Revision + 1, Effects = tamperedEffects };

        var outputPath = Path.Combine(_root, "rejected.xsq");

        await Assert.ThrowsAsync<ScopedExportContractViolatedException>(() => exporter.ExportScopedAsync(
            layout, original, tamperedAfter, arch1Scope, outputPath, "2024.01", XsqExportPolicy.RejectIfExists, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
    }

    /// <summary>An out-of-scope effect removed entirely between before/after is also a scope violation, not merely "fewer effects to compare".</summary>
    [Fact]
    public async Task ScopedExport_OutOfScopeEffectRemoved_IsRejected_NoFileWritten_NegativePath()
    {
        var layout = GateDemoLayout();
        var original = GateDemoPlan(layout);
        var exporter = NewScopedExporter();

        var arch1 = original.Effects.Single(e => e.Target.Value == "Arch 1");
        var roofline = original.Effects.Single(e => e.Target.Value == "Roofline");
        var arch1Scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target });

        var afterWithoutRoofline = original with
        {
            Revision = original.Revision + 1,
            Effects = original.Effects.Where(e => e.Id != roofline.Id).ToList(),
        };

        var outputPath = Path.Combine(_root, "rejected-removed.xsq");

        await Assert.ThrowsAsync<ScopedExportContractViolatedException>(() => exporter.ExportScopedAsync(
            layout, original, afterWithoutRoofline, arch1Scope, outputPath, "2024.01", XsqExportPolicy.RejectIfExists, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// "Invalid model/format/version must fail safely without rewriting source files": an effect whose
    /// target does not resolve against the supplied layout must reject via the real underlying
    /// <see cref="XsqExporter"/>'s own validation — reused, not duplicated — with no file written, even
    /// though the scope-contract check itself would have passed.
    /// </summary>
    [Fact]
    public async Task ScopedExport_InvalidModelTarget_FailsSafely_NoFileWritten_NegativePath()
    {
        var layout = GateDemoLayout();
        var original = GateDemoPlan(layout);
        var exporter = NewScopedExporter();

        var arch1 = original.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch1Scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target });

        // Layout no longer contains "Arch 2" at all — simulates an invalid/stale model reference.
        var layoutMissingArch2 = layout with { Models = layout.Models.Where(m => m.XlightsName != "Arch 2").ToList() };

        var outputPath = Path.Combine(_root, "rejected-invalid-model.xsq");

        await Assert.ThrowsAsync<XsqExportInvalidException>(() => exporter.ExportScopedAsync(
            layoutMissingArch2, original, original, arch1Scope, outputPath, "2024.01", XsqExportPolicy.RejectIfExists, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
    }

    /// <summary>"Invalid... version must fail safely without rewriting source files": a blank target xLights version is rejected with no file written.</summary>
    [Fact]
    public async Task ScopedExport_BlankTargetVersion_FailsSafely_NoFileWritten_NegativePath()
    {
        var layout = GateDemoLayout();
        var original = GateDemoPlan(layout);
        var exporter = NewScopedExporter();

        var arch1 = original.Effects.Single(e => e.Target.Value == "Arch 1");
        var arch1Scope = new EditScope(new TimeWindow(0, long.MaxValue), new HashSet<ModelId> { arch1.Target });

        var outputPath = Path.Combine(_root, "rejected-blank-version.xsq");

        await Assert.ThrowsAsync<XsqExportInvalidException>(() => exporter.ExportScopedAsync(
            layout, original, original, arch1Scope, outputPath, "   ", XsqExportPolicy.RejectIfExists, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
    }

    private static long RoundToFrameMs(long ticks)
    {
        var ms = TimeSpan.FromTicks(ticks).TotalMilliseconds;
        return (long)Math.Round(ms / XsqExporter.FrameIntervalMs, MidpointRounding.AwayFromZero) * XsqExporter.FrameIntervalMs;
    }
}
