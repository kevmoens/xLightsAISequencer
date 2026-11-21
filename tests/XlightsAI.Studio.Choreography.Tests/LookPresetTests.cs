namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.3 ("reusable-presets"): <see cref="LookPresetConverter"/>'s own pure
/// capture/re-expand round trip -- effect types, relative layer order, blend modes and relative
/// timing/value-curve shape survive unchanged; <see cref="LookLayer.Props"/> and <see cref="Look.PaletteRef"/>
/// (the two target-/color-specific fields) are dropped on capture and freely re-supplied on apply.
/// </summary>
public sealed class LookPresetTests
{
    private static Look BuildSourceLook() => new(
        Id: "source-look", Name: "Source Look", Intent: "test intent", EnergyCeiling: 0.8,
        Motion: "chase", PaletteRef: "warm-gold", Subject: "Tree",
        Layers:
        [
            new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, new Dictionary<string, string> { ["X"] = "1" }, "Normal", "Bars", 0.9),
            new LookLayer(2, "Tree", "Twinkle", LookLayer.RoleSecondary, null, "2 is Mask", "Beats", 0.4,
                Curves: new Dictionary<string, LookCurveBinding> { ["Twinkle_Speed"] = new("Music", "Beats") }),
        ]);

    [Fact]
    public void FromLook_CapturesEveryLayerStructureFieldAndDropsProps()
    {
        var look = BuildSourceLook();

        var preset = LookPresetConverter.FromLook("my-preset", look, "captured for test");

        Assert.Equal("my-preset", preset.Name);
        Assert.Equal(look.Motion, preset.Motion);
        Assert.Equal(look.Id, preset.SourceLookId);
        Assert.Equal("captured for test", preset.SourceNote);
        Assert.Equal(0, preset.Version);
        Assert.Equal(2, preset.Layers.Count);

        var layer0 = preset.Layers[0];
        Assert.Equal(LookLayer.RoleSubject, layer0.Role);
        Assert.Equal("On", layer0.Effect);
        Assert.Equal(1, layer0.Z);
        Assert.Equal("Normal", layer0.Blend);
        Assert.Equal(0.9, layer0.Duty);
        Assert.Equal("Bars", layer0.Timescale);
        Assert.Equal("1", layer0.ParamsOrEmpty["X"]);

        var layer1 = preset.Layers[1];
        Assert.Equal(LookLayer.RoleSecondary, layer1.Role);
        Assert.Equal("Twinkle", layer1.Effect);
        Assert.Equal(2, layer1.Z);
        Assert.Equal("2 is Mask", layer1.Blend);
        Assert.Equal(0.4, layer1.Duty);
        Assert.Equal("Beats", layer1.Timescale);
        Assert.Equal("Music", layer1.CurvesOrEmpty["Twinkle_Speed"].Type);
        Assert.Equal("Beats", layer1.CurvesOrEmpty["Twinkle_Speed"].Track);
    }

    [Fact]
    public void FromLook_EmptyLayers_Throws()
    {
        var look = BuildSourceLook() with { Layers = [] };
        Assert.Throws<ArgumentException>(() => LookPresetConverter.FromLook("empty", look));
    }

    [Fact]
    public void Apply_ProducesALookWithEveryLayerRetargetedAndRepalettedButStructureUnchanged()
    {
        var sourceLook = BuildSourceLook();
        var preset = LookPresetConverter.FromLook("my-preset", sourceLook);

        var applied = LookPresetConverter.Apply(
            preset, newLookId: "applied-look", newLookName: "Applied Look", intent: "new intent",
            energyCeiling: 0.6, paletteRef: "ice-blue", subject: "MiniTrees", targetProps: "group:MiniTrees");

        Assert.Equal("applied-look", applied.Id);
        Assert.Equal("ice-blue", applied.PaletteRef);
        Assert.Equal("MiniTrees", applied.Subject);
        Assert.Equal(sourceLook.Motion, applied.Motion);
        Assert.Equal(2, applied.Layers.Count);

        foreach (var layer in applied.Layers)
        {
            Assert.Equal("group:MiniTrees", layer.Props);
        }

        var appliedByZ = applied.Layers.OrderBy(l => l.Z).ToList();
        var sourceByZ = sourceLook.Layers.OrderBy(l => l.Z).ToList();
        Assert.Equal(sourceByZ.Select(l => l.Z), appliedByZ.Select(l => l.Z));
        Assert.Equal(sourceByZ.Select(l => l.Effect), appliedByZ.Select(l => l.Effect));
        Assert.Equal(sourceByZ.Select(l => l.Blend), appliedByZ.Select(l => l.Blend));
        Assert.Equal(sourceByZ.Select(l => l.Duty), appliedByZ.Select(l => l.Duty));
        Assert.Equal(sourceByZ.Select(l => l.Timescale), appliedByZ.Select(l => l.Timescale));
        Assert.Equal(sourceByZ.Select(l => l.Role), appliedByZ.Select(l => l.Role));
    }

    /// <summary>
    /// Build-plan Phase 30 step 30.3: "any HitMotionCompounds from 30.2 worth keeping" -- proves a preset
    /// is general enough to capture that exact confirmed shape (a base hit layer plus a second layer using
    /// 30.2's own confirmed <see cref="HitMotionCompound.MovementEffect"/>/<see cref="HitMotionCompound.MovementValueCurveParameterKey"/>/
    /// <see cref="HitMotionCompound.MovementCurveType"/> constants) without inventing anything new -- the
    /// same three confirmed constants 30.2 already shipped, reused here via the general Look/LookLayer
    /// curve-binding path (<see cref="LookCurveBinding"/>) rather than 30.2's own cue-compound-specific
    /// <see cref="Xlights.EffectValueCurve"/> construction.
    /// </summary>
    [Fact]
    public void FromLook_HitMotionCompoundShapedLook_RoundTripsTheConfirmedMovementLayerUnchanged()
    {
        var look = new Look(
            "hit-motion-look", "Hit Motion Look", "accent with movement", 0.9, "pop", "warm-gold", "Tree",
            [
                new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 1.0),
                new LookLayer(2, "Tree", HitMotionCompound.MovementEffect, LookLayer.RoleSecondary, null, "Normal", null, 1.0,
                    Curves: new Dictionary<string, LookCurveBinding>
                    {
                        [HitMotionCompound.MovementValueCurveParameterKey] = new(HitMotionCompound.MovementCurveType),
                    }),
            ]);

        var preset = LookPresetConverter.FromLook("hit-motion-preset", look, "promoted from a 30.2 HitMotionCompound");
        var applied = LookPresetConverter.Apply(preset, "applied", "Applied", "intent", 0.9, "ice-blue", "MiniTrees", "group:MiniTrees");

        var movementLayer = Assert.Single(applied.Layers, l => l.Effect == HitMotionCompound.MovementEffect);
        Assert.Equal(HitMotionCompound.MovementCurveType, movementLayer.CurvesOrEmpty[HitMotionCompound.MovementValueCurveParameterKey].Type);
        Assert.Equal("group:MiniTrees", movementLayer.Props);
    }
}
