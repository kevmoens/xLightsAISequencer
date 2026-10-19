using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-07 Step 051 — "Handle... frame quantization gracefully"; Section 12's own instruction: "verify
/// xLights frame snapping doesn't reverse event order." Exercises the real <see cref="XsqExporter"/>
/// (the one real, already-accepted xLights frame-grid rounding implementation in this repository --
/// PHASE-02 Steps 013/015's own <c>FrameIntervalMs</c>/<c>RoundToFrame</c>), not a mock, and real
/// file-system I/O against a real round-trip <see cref="XsqStructuralReader"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step051FrameQuantizationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step051-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Step051_Acceptance_Passes_OverlappingEffectsOnSameLayer_FrameSnapNeverReversesOrder_NoInvalidEffects()
    {
        // Nothing upstream of XsqExporter enforces non-overlap between two EffectInstances on the same
        // target+layer (unlike MelodyTrackValidator's own strict NoteEvent invariant) -- a genuinely
        // raw-overlapping pair (effect 2's own raw StartTicks falls before effect 1's own raw EndTicks)
        // must still frame-quantize to a valid, non-colliding, correctly-ordered pair: pushed forward,
        // never reordered, never silently written overlapping ("no invalid effects," this step's own
        // required acceptance, verbatim).
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var target = new ModelId(layout.Models[0].XlightsName);

        var effect1 = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, target, 0L, 300_000L);         // 0-30ms raw
        var effect2 = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, target, 100_000L, 400_000L);   // 10-40ms raw -- genuinely overlaps effect1

        var plan = new SequenceDocument(Guid.NewGuid(), 1, [effect1, effect2]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "overlap.xsq");
        await exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None);

        var structural = new XsqStructuralReader().Read(outputPath);
        var element = structural.ElementEffects.Single(e => e.Name == target.Value);
        Assert.Equal(2, element.Effects.Count);

        var first = element.Effects[0];
        var second = element.Effects[1];

        // Order is never reversed: the second effect's own written start is never before the first's own
        // written end.
        Assert.True(second.StartTimeMs >= first.EndTimeMs,
            $"Frame-quantized effects must never overlap/reverse: first=[{first.StartTimeMs},{first.EndTimeMs}), second=[{second.StartTimeMs},{second.EndTimeMs}).");

        // Pushed forward to meet the first effect's own rounded end, preserving its own rounded duration
        // (50ms - 0ms = 50ms naive width) rather than being dropped, truncated to nothing, or left
        // colliding.
        Assert.Equal(0.0, first.StartTimeMs);
        Assert.Equal(25.0, first.EndTimeMs);
        Assert.Equal(25.0, second.StartTimeMs);
        Assert.Equal(75.0, second.EndTimeMs);
    }

    [Fact]
    public async Task NonOverlappingWellSeparatedEffects_FrameQuantizeIndependently_NeverShifted()
    {
        // Regression guard: two effects far enough apart that frame rounding alone could never cause a
        // collision must be written with their own independent rounded timing, completely unshifted --
        // the new collision guard must never trigger for the ordinary, non-overlapping case every prior
        // Phase 02/07 step already relies on.
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var target = new ModelId(layout.Models[0].XlightsName);

        var effect1 = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, target, 0L, 300_000L);        // 0-30ms
        var effect2 = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, target, 1_000_000L, 1_300_000L); // 100-130ms, well separated

        var plan = new SequenceDocument(Guid.NewGuid(), 1, [effect1, effect2]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "separated.xsq");
        await exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None);

        var structural = new XsqStructuralReader().Read(outputPath);
        var element = structural.ElementEffects.Single(e => e.Name == target.Value);

        Assert.Equal(0.0, element.Effects[0].StartTimeMs);
        Assert.Equal(25.0, element.Effects[0].EndTimeMs);
        Assert.Equal(100.0, element.Effects[1].StartTimeMs);
        Assert.Equal(125.0, element.Effects[1].EndTimeMs);
    }

    [Fact]
    public async Task EffectWhoseOwnDurationCollapsesAtFrameGrid_StillFailsSafely_NoFileWritten_NegativePath()
    {
        // Step 013/015's own original, unmodified failure path (unaffected by this step's own additive
        // collision guard, which only ever pushes a START forward, never masks a single effect's own
        // genuinely non-positive rounded duration): a 10ms-wide effect rounds to a single 0ms point at
        // the 25ms frame grid and must still be rejected outright, not silently widened or dropped.
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var target = new ModelId(layout.Models[0].XlightsName);

        var collapsingEffect = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, target, 0L, 100_000L); // 0-10ms -> rounds to [0,0]ms
        var plan = new SequenceDocument(Guid.NewGuid(), 1, [collapsingEffect]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "collapsing.xsq");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None));

        Assert.Contains("non-positive duration", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>Mirrors Step013AcceptanceTests's own identical fixture (single-model slice is all this step needs).</summary>
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", ["Front-Arches"], []),
        ],
        Groups: [],
        Warnings: []);
}
