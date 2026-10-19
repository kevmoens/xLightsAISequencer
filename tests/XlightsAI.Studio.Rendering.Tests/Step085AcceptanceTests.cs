using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rendering.Tests;

/// <summary>
/// PHASE-13 Step 085 ("Build fast lightweight internal preview for supported primitive gestures").
/// Required acceptance, verbatim: "Manual known fixtures match approximate rendered path." Required
/// failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not corrupt
/// the project." Every fixture here is built entirely programmatically (no mocking; real
/// <see cref="LocalGesturePreviewRenderer"/>, real <see cref="SpatialDisplayGraph"/>/<see cref="SequenceDocument"/>
/// instances) -- the same convention <c>Step084AcceptanceTests</c> already established for data-only fixtures.
/// </summary>
public sealed class Step085AcceptanceTests
{
    private static readonly ModelId NodeA = new("Node-A");
    private static readonly ModelId NodeB = new("Node-B");
    private static readonly ModelId NodeC = new("Node-C");
    private static readonly Guid ArchPathId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static SpatialDisplayGraph BuildArchLayout()
    {
        var nodes = new List<DisplayNode>
        {
            new(NodeA, "Arch-A", 0.0, 0.5, "Arches", new HashSet<string>()),
            new(NodeB, "Arch-B", 0.5, 0.0, "Arches", new HashSet<string>()),
            new(NodeC, "Arch-C", 1.0, 0.5, "Arches", new HashSet<string>()),
        };
        var path = new SpatialPath(ArchPathId, "Arch Step Route", new[] { NodeA, NodeB, NodeC }, IsClosed: false, Version: 1);
        return new SpatialDisplayGraph("layout-hash-step085", nodes, Array.Empty<SpatialEdge>(), new[] { path }, Guid.NewGuid());
    }

    private static EffectInstance Hold(ModelId target, long start, long end) => new(
        Guid.NewGuid(), target, Layer: 0, start, end,
        RecipeId: EffectRecipeRegistry.SimpleOnRecipeId, RecipeVersion: EffectRecipeRegistry.SimpleOnRecipeVersion,
        Parameters: new Dictionary<string, string> { [LocalGesturePreviewRenderer.PreviewGestureParameterKey] = LocalGesturePreviewRenderer.HoldGestureValue });

    private static EffectInstance Pop(ModelId target, long start, long end) => new(
        Guid.NewGuid(), target, Layer: 0, start, end,
        RecipeId: EffectRecipeRegistry.SimpleOnRecipeId, RecipeVersion: EffectRecipeRegistry.SimpleOnRecipeVersion,
        Parameters: new Dictionary<string, string> { [LocalGesturePreviewRenderer.PreviewGestureParameterKey] = LocalGesturePreviewRenderer.PopGestureValue });

    private static EffectInstance Step(ModelId target, long start, long end, Guid pathId) => new(
        Guid.NewGuid(), target, Layer: 0, start, end,
        RecipeId: EffectRecipeRegistry.SimpleOnRecipeId, RecipeVersion: EffectRecipeRegistry.SimpleOnRecipeVersion,
        Parameters: new Dictionary<string, string>
        {
            [LocalGesturePreviewRenderer.PreviewGestureParameterKey] = LocalGesturePreviewRenderer.StepGestureValue,
            [LocalGesturePreviewRenderer.PathIdParameterKey] = pathId.ToString(),
        });

    private static EffectInstance UnknownGesture(ModelId target, long start, long end) => new(
        Guid.NewGuid(), target, Layer: 0, start, end,
        RecipeId: EffectRecipeRegistry.SimpleOnRecipeId, RecipeVersion: EffectRecipeRegistry.SimpleOnRecipeVersion,
        Parameters: new Dictionary<string, string>());

    private static async Task<List<PreviewFrame>> CollectAsync(
        LocalGesturePreviewRenderer renderer, SequenceDocument doc, SpatialDisplayGraph layout, TimeWindow window, CancellationToken ct = default)
    {
        var frames = new List<PreviewFrame>();
        await foreach (var frame in renderer.PreviewAsync(doc, layout, window, ct))
        {
            frames.Add(frame);
        }
        return frames;
    }

    [Fact]
    public async Task Step085_Acceptance_Passes()
    {
        var layout = BuildArchLayout();
        const long frame = LocalGesturePreviewRenderer.FrameIntervalTicks;

        // Hold on Node-A for the whole window; Step walks the arch path Node-A -> Node-B -> Node-C.
        var holdEffect = Hold(NodeA, 0, frame * 3);
        var stepEffect = Step(NodeB, 0, frame * 3, ArchPathId);
        var doc = new SequenceDocument(Guid.NewGuid(), 1, new[] { holdEffect, stepEffect });

        var frames = await CollectAsync(new LocalGesturePreviewRenderer(), doc, layout, new TimeWindow(0, frame * 3));

        Assert.Equal(3, frames.Count);
        Assert.All(frames, f => Assert.Empty(f.UnsupportedEffectIds));

        // Frame 0: Hold lights Node-A; Step (progress 0) also resolves to Node-A.
        Assert.Contains(frames[0].Illuminated, p => p.Model == NodeA && p.R == 255 && p.G == 255 && p.B == 255);

        // Frame 1 (mid-window): Step has advanced to Node-B.
        Assert.Contains(frames[1].Illuminated, p => p.Model == NodeB);

        // Frame 2 (last sampled instant, progress ~0.67): Step is on Node-C, the path's final node.
        Assert.Contains(frames[2].Illuminated, p => p.Model == NodeC);

        // Hold keeps Node-A lit at every sampled instant regardless of the Step gesture's own progress.
        Assert.All(frames, f => Assert.Contains(f.Illuminated, p => p.Model == NodeA));
    }

    [Fact]
    public async Task Step085_Acceptance_PopFlashesThenGoesDark()
    {
        var layout = BuildArchLayout();
        const long frame = LocalGesturePreviewRenderer.FrameIntervalTicks;
        var popEffect = Pop(NodeC, 0, frame * 4); // PopFlashFraction=0.25 -> lit only for t < frame

        var doc = new SequenceDocument(Guid.NewGuid(), 1, new[] { popEffect });
        var frames = await CollectAsync(new LocalGesturePreviewRenderer(), doc, layout, new TimeWindow(0, frame * 4));

        Assert.Contains(frames[0].Illuminated, p => p.Model == NodeC);
        Assert.DoesNotContain(frames[1].Illuminated, p => p.Model == NodeC);
        Assert.DoesNotContain(frames[2].Illuminated, p => p.Model == NodeC);
        Assert.Empty(frames[1].UnsupportedEffectIds); // dark, but still a *resolved* (supported) gesture
    }

    [Fact]
    public async Task Step085_Acceptance_UnsupportedGestureIsFlaggedApproximate()
    {
        var layout = BuildArchLayout();
        const long frame = LocalGesturePreviewRenderer.FrameIntervalTicks;
        var unknown = UnknownGesture(NodeA, 0, frame);

        var doc = new SequenceDocument(Guid.NewGuid(), 1, new[] { unknown });
        var frames = await CollectAsync(new LocalGesturePreviewRenderer(), doc, layout, new TimeWindow(0, frame));

        Assert.Single(frames);
        Assert.Empty(frames[0].Illuminated);
        Assert.Equal(new[] { unknown.Id }, frames[0].UnsupportedEffectIds);
    }

    [Fact]
    public async Task Step085_NegativePath_InvalidWindow_ThrowsWithoutYieldingAnyFrame()
    {
        var layout = BuildArchLayout();
        var doc = new SequenceDocument(Guid.NewGuid(), 1, Array.Empty<EffectInstance>());
        var renderer = new LocalGesturePreviewRenderer();

        var framesSeen = 0;
        var invalidWindow = new TimeWindow(1_000, 1_000); // empty window: End == Start

        await Assert.ThrowsAsync<RenderPreviewInvalidException>(async () =>
        {
            await foreach (var _ in renderer.PreviewAsync(doc, layout, invalidWindow, CancellationToken.None))
            {
                framesSeen++;
            }
        });

        Assert.Equal(0, framesSeen);
    }

    [Fact]
    public async Task Step085_NegativePath_Cancellation_StopsWithoutCorruptingOutput()
    {
        var layout = BuildArchLayout();
        const long frame = LocalGesturePreviewRenderer.FrameIntervalTicks;
        var doc = new SequenceDocument(Guid.NewGuid(), 1, new[] { Hold(NodeA, 0, frame * 100) });
        var renderer = new LocalGesturePreviewRenderer();

        using var cts = new CancellationTokenSource();
        var framesSeen = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in renderer.PreviewAsync(doc, layout, new TimeWindow(0, frame * 100), cts.Token))
            {
                framesSeen++;
                if (framesSeen == 3)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(3, framesSeen);
    }
}
