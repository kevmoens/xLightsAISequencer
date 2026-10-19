using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing.Tests;

/// <summary>
/// PHASE-03 Step 018 ("Render effect blocks and inspector selection"). Required acceptance:
/// "Selection maps exactly to effect ID and time span." Required failure/edge case: "Out-of-scope,
/// locked or stale revision must be rejected without partial mutation" — for this step's own narrow
/// render/hit-test scope, proven as "hit-testing an empty pixel region never returns a false-positive
/// effect id, and an overlapping layer is never silently hidden behind another." These tests exercise
/// the real <see cref="EffectBlockLayoutBuilder"/>/<see cref="TimelineViewport"/> against the gate
/// demo's own exact layout shape (Roofline, Arch 1, Arch 2 — matching
/// <c>Step019AcceptanceTests</c>/<c>Step020AcceptanceTests</c>), not mocks.
/// </summary>
public sealed class Step018AcceptanceTests
{
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

    private static TimelineViewport SmallViewport(TimeWindow bounds) =>
        TimelineViewport.Create(bounds, TimelineViewport.MinTicksPerPixel);

    [Fact]
    public void Step018_Acceptance_Passes_BuildLayerTracks_MapsExactlyToEffectIdAndTimeSpan()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 1_000_000));

        var tracks = EffectBlockLayoutBuilder.BuildLayerTracks(plan.Effects, arch1.Target, viewport);

        var track = Assert.Single(tracks);
        Assert.Equal(arch1.Layer, track.Layer);
        var block = Assert.Single(track.Blocks);
        Assert.Equal(arch1.Id, block.EffectId);
        Assert.Equal(arch1.Target, block.Target);
        Assert.Equal(arch1.StartTicks, block.StartTicks);
        Assert.Equal(arch1.EndTicks, block.EndTicks);
        Assert.Equal(arch1.Locked, block.Locked);
        // Exact mapping through the real shared viewport transform — not a hardcoded pixel value.
        Assert.Equal(viewport.XFromTime(arch1.StartTicks), block.X);
        Assert.Equal(viewport.XFromTime(arch1.EndTicks) - block.X, block.Width);
    }

    [Fact]
    public void Step018_Acceptance_Passes_HitTest_ReturnsExactEffectId_AtAnyPixelWithinItsSpan()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 1_000_000));

        var startPixel = viewport.XFromTime(arch1.StartTicks);
        var midPixel = viewport.XFromTime((arch1.StartTicks + arch1.EndTicks) / 2);
        var lastInsidePixel = viewport.XFromTime(arch1.EndTicks) - 1;

        Assert.Equal(arch1.Id, EffectBlockLayoutBuilder.HitTest(plan.Effects, arch1.Target, viewport, startPixel));
        Assert.Equal(arch1.Id, EffectBlockLayoutBuilder.HitTest(plan.Effects, arch1.Target, viewport, midPixel));
        Assert.Equal(arch1.Id, EffectBlockLayoutBuilder.HitTest(plan.Effects, arch1.Target, viewport, lastInsidePixel));
    }

    [Fact]
    public void HitTest_OutsideEverySpan_ReturnsNull_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 10_000_000));

        // One pixel before the effect even starts, and far past the whole document's content.
        var beforeStart = viewport.XFromTime(arch1.StartTicks) - 1;
        var wayPastEnd = viewport.XFromTime(plan.Effects.Max(e => e.EndTicks) + 5_000_000);

        Assert.Null(EffectBlockLayoutBuilder.HitTest(plan.Effects, arch1.Target, viewport, beforeStart));
        Assert.Null(EffectBlockLayoutBuilder.HitTest(plan.Effects, arch1.Target, viewport, wayPastEnd));
    }

    [Fact]
    public void ModelWithNoEffects_ProducesEmptyTracks_SilenceEdgeCase()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var viewport = SmallViewport(new TimeWindow(0, 10_000_000));

        var tracks = EffectBlockLayoutBuilder.BuildLayerTracks(plan.Effects, new ModelId("Nonexistent-Model"), viewport);

        Assert.Empty(tracks);
        Assert.Null(EffectBlockLayoutBuilder.HitTest(plan.Effects, new ModelId("Nonexistent-Model"), viewport, 0));
    }

    /// <summary>
    /// "Support overlapping layers explicitly" (Step 018 implementation guidance): two effects on the
    /// same model with overlapping time spans, on two different <see cref="EffectInstance.Layer"/>
    /// values, must render on two distinct tracks — never merged into one block, never silently
    /// hidden. <see cref="EffectBlockLayoutBuilder.HitTest"/> at the overlapping pixel returns the
    /// higher (topmost) layer's effect, an explicit choice, not an arbitrary one.
    /// </summary>
    [Fact]
    public void OverlappingLayers_RenderOnDistinctTracks_HitTestPrefersTopmostLayer()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch1 = plan.Effects.Single(e => e.Target.Value == "Arch 1");

        // A second effect on the same model, same time span, one layer above arch1's own layer —
        // never producible by ThreeEffectPlanFactory today, but a real, legitimate document shape
        // (two simultaneous effects on one model is exactly what "overlapping layers" means).
        var overlapping = arch1 with { Id = Guid.NewGuid(), Layer = arch1.Layer + 1 };
        var effects = plan.Effects.Append(overlapping).ToList();
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 1_000_000));

        var tracks = EffectBlockLayoutBuilder.BuildLayerTracks(effects, arch1.Target, viewport);

        Assert.Equal(2, tracks.Count);
        Assert.Equal(arch1.Layer, tracks[0].Layer);
        Assert.Equal(overlapping.Layer, tracks[1].Layer);
        Assert.Equal(arch1.Id, Assert.Single(tracks[0].Blocks).EffectId);
        Assert.Equal(overlapping.Id, Assert.Single(tracks[1].Blocks).EffectId);

        var midPixel = viewport.XFromTime((arch1.StartTicks + arch1.EndTicks) / 2);
        Assert.Equal(overlapping.Id, EffectBlockLayoutBuilder.HitTest(effects, arch1.Target, viewport, midPixel));
    }

    [Fact]
    public void LockedEffect_StillRendersAndHitTests_VisibleNotHidden()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var arch2 = plan.Effects.Single(e => e.Target.Value == "Arch 2");
        var lockedArch2 = arch2 with { Locked = true };
        var effects = plan.Effects.Select(e => e.Id == arch2.Id ? lockedArch2 : e).ToList();
        var viewport = SmallViewport(new TimeWindow(0, plan.Effects.Max(e => e.EndTicks) + 1_000_000));

        var tracks = EffectBlockLayoutBuilder.BuildLayerTracks(effects, arch2.Target, viewport);

        var block = Assert.Single(Assert.Single(tracks).Blocks);
        Assert.True(block.Locked);
        var midPixel = viewport.XFromTime((arch2.StartTicks + arch2.EndTicks) / 2);
        Assert.Equal(arch2.Id, EffectBlockLayoutBuilder.HitTest(effects, arch2.Target, viewport, midPixel));
    }
}
