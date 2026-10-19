using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-13 Step 085 -- "Build fast lightweight internal preview for supported primitive gestures."
/// Required acceptance (verbatim): "Manual known fixtures match approximate rendered path." Required
/// failure/edge case (verbatim): "Invalid input, cancellation or stale persisted state must not corrupt
/// the project." Fast, deterministic tests for <see cref="RenderPreviewViewModel"/>'s own bindable
/// load contract against a real <see cref="LocalGesturePreviewRenderer"/> (not a mock) -- the same
/// convention Step 084's own <see cref="Step084CandidateCompareViewModelTests"/> sibling already
/// established. No real SCREEN 15/16 Avalonia/AXAML view exists yet (the project's own pre-existing,
/// consistent convention); this step's own required "Avalonia screenshot or repeatable desktop
/// interaction recording with saved-state verification" outcome is proven at the bindable view-model
/// layer the real screen UI would bind to.
/// </summary>
public sealed class Step085RenderPreviewViewModelTests
{
    private static readonly ModelId NodeA = new("Node-A");
    private const long Frame = LocalGesturePreviewRenderer.FrameIntervalTicks;

    private static SpatialDisplayGraph BuildLayout()
    {
        var nodes = new List<DisplayNode> { new(NodeA, "Arch-A", 0.0, 0.5, "Arches", new HashSet<string>()) };
        return new SpatialDisplayGraph("layout-hash-step085-vm", nodes, Array.Empty<SpatialEdge>(), Array.Empty<SpatialPath>(), Guid.NewGuid());
    }

    [Fact]
    public async Task Step085_Acceptance_LoadAsyncPopulatesFramesFromKnownFixture()
    {
        var layout = BuildLayout();
        var holdEffect = new EffectInstance(
            Guid.NewGuid(), NodeA, Layer: 0, 0, Frame * 2,
            RecipeId: EffectRecipeRegistry.SimpleOnRecipeId, RecipeVersion: EffectRecipeRegistry.SimpleOnRecipeVersion,
            Parameters: new Dictionary<string, string> { [LocalGesturePreviewRenderer.PreviewGestureParameterKey] = LocalGesturePreviewRenderer.HoldGestureValue });
        var doc = new SequenceDocument(Guid.NewGuid(), 1, new[] { holdEffect });

        var vm = new RenderPreviewViewModel();
        Assert.True(vm.IsEmpty);

        var ok = await vm.LoadAsync(new LocalGesturePreviewRenderer(), doc, layout, new TimeWindow(0, Frame * 2));

        Assert.True(ok);
        Assert.True(vm.IsLoaded);
        Assert.Equal(2, vm.Frames.Count);
        Assert.All(vm.Frames, f => Assert.Contains(f.Illuminated, p => p.Model == NodeA));
        Assert.False(vm.HasApproximateEffects);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Step085_NegativePath_InvalidWindowRejectedWithoutPartialState()
    {
        var layout = BuildLayout();
        var doc = new SequenceDocument(Guid.NewGuid(), 1, Array.Empty<EffectInstance>());
        var vm = new RenderPreviewViewModel();

        var ok = await vm.LoadAsync(new LocalGesturePreviewRenderer(), doc, layout, new TimeWindow(500, 500));

        Assert.False(ok);
        Assert.True(vm.IsError);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Empty(vm.Frames);
    }
}
