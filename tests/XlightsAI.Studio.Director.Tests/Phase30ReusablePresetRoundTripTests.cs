using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.3's own acceptance definition ("Preset reusability"), verbatim: "A saved
/// preset reproduces its recorded layer stack (effect types, relative layer order, blend modes) on a
/// different target and a different palette, with only palette-mapped colors changing -- effect types,
/// blend modes, and relative timing parameters are unchanged. A round-trip test: save a preset from a
/// generated Look, apply it to a different model/group with a different palette, and diff the two
/// placements' structure (ignoring color)."
///
/// This is the direct gate test for that definition: a real, compiled-and-exported round trip --
/// <see cref="LookPresetCatalog.SaveAsync"/> captures a generated <see cref="Look"/> (carrying both a
/// step-30.1 confirmed blend mode and a step-30.2-shaped movement/value-curve layer, proving a preset
/// generalizes to both prior steps' confirmed shapes, not just a plain effect), persists it to a real JSON
/// file, <see cref="LookPresetCatalog.ApplyAsync"/> re-expands it onto a different target and a different
/// palette, both the original and the applied Look are compiled via the real, unmodified
/// <see cref="LookCompiler.Compile"/> against the SAME section/bar grid (so only target/palette differ),
/// merged into one <see cref="ChoreographyIR"/> (<see cref="ChoreographyIR.Merge"/>, the same mechanism
/// every multi-section whole-song compile already uses to keep two palettes distinct), exported together
/// into one real <c>.xsq</c> via <see cref="XsqExporter"/>, and re-parsed back via
/// <see cref="XsqStructuralReader"/> -- the diff below runs against that real exported/re-parsed file, not
/// against in-memory IR alone.
/// </summary>
public sealed class Phase30ReusablePresetRoundTripTests : IDisposable
{
    private const long TicksPerSecond = 10_000_000L;

    private readonly string _presetDirectory = Path.Combine(Path.GetTempPath(), "xlai-phase30-presets-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_presetDirectory))
        {
            Directory.Delete(_presetDirectory, recursive: true);
        }
    }

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            new DisplayModel(new ModelId("Arch"), "Arch", "Arch", [], [], NodeCount: 300),
        ],
        Groups: [],
        Warnings: []);

    private static BarGrid BuildBarGrid(int barCount, long barTicks)
    {
        var beats = Enumerable.Range(0, barCount).Select(bar => new BeatMarker(Guid.NewGuid(), bar * barTicks, 0, bar, 1.0, false)).ToArray();
        var beatGrid = new BeatGrid(Guid.NewGuid(), new SongId(Guid.NewGuid()), "h", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), null, beats, [], 0);
        return BarGrid.FromBeatGrid(beatGrid, barCount * barTicks);
    }

    private static SectionMusicContext BuildSection(BarGrid barGrid, long barTicks, int barCount)
    {
        var window = new TimeWindow(0, barCount * barTicks);
        var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
        {
            ["Bars"] = new TimingTrack("Bars", barGrid.Bars.Select(b => new TimingMark("", b.Window.StartTicks, b.Window.EndTicks)).ToList()),
        };
        return new SectionMusicContext(window, SectionKind.Chorus, 0.9, barGrid, tracks, VocalActive: false);
    }

    private static LookCompilerSettings BuildSettings(ColorPaletteDefinition warmGold, ColorPaletteDefinition iceBlue)
    {
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var palettes = new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = warmGold, ["ice-blue"] = iceBlue };
        return new LookCompilerSettings(vocabulary, recipes, registry, palettes);
    }

    /// <summary>
    /// Step 30.1 confirmed blend mode on layer 1 (discrete/Mode B, so the layer's own <see cref="LookLayer.Duty"/>
    /// shapes real relative timing, not just one flat full-span effect) plus a step-30.2-shaped movement
    /// layer on layer 2 (Mode A/bound, carrying a real value curve) -- the same two confirmed shapes 30.1
    /// and 30.2 shipped, composed into one Look and saved/applied as a single preset.
    /// </summary>
    private static Look BuildGeneratedLook() => new(
        Id: "generated-look", Name: "Generated Look", Intent: "a generated Look worth saving as a preset",
        EnergyCeiling: 0.9, Motion: "chase", PaletteRef: "warm-gold", Subject: "Tree",
        Layers:
        [
            new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "2 is Mask", "Bars", 0.5),
            new LookLayer(2, "Tree", HitMotionCompound.MovementEffect, LookLayer.RoleSecondary, null, "Normal", null, 1.0,
                Curves: new Dictionary<string, LookCurveBinding> { [HitMotionCompound.MovementValueCurveParameterKey] = new(HitMotionCompound.MovementCurveType) }),
        ]);

    [Fact]
    public async Task SavePresetFromGeneratedLook_ApplyToDifferentTargetAndPalette_CompileExportAndReparse_StructureMatchesOnlyColorDiffers()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5);

        var warmGold = PaletteBuilder.Build(["#FFC880"]);
        var iceBlue = PaletteBuilder.Build(["#80D0FF", "#FFFFFF"]);
        var settings = BuildSettings(warmGold, iceBlue);

        // --- Save: capture a generated Look's own structure as a named, persisted preset. ---
        var generatedLook = BuildGeneratedLook();
        var catalog = new LookPresetCatalog(new JsonLookPresetStore(_presetDirectory));
        await catalog.SaveAsync("hit-and-mask", generatedLook, "generated Look worth keeping", CancellationToken.None);

        // --- Apply: re-expand the saved preset onto a different model and a different palette. ---
        var appliedLook = await catalog.ApplyAsync(
            "hit-and-mask", "applied-look", "Applied Look", "an applied preset on a new target/palette",
            energyCeiling: 0.9, paletteRef: "ice-blue", subject: "Arch", targetProps: "Arch", CancellationToken.None);
        Assert.NotNull(appliedLook);

        // --- Compile both against the SAME section/bar grid, so only target/palette can differ. ---
        var originalIr = LookCompiler.Compile(generatedLook, section, layout, settings);
        var appliedIr = LookCompiler.Compile(appliedLook!, section, layout, settings);
        var merged = originalIr.Merge(appliedIr);

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xsq");
        try
        {
            await new XsqExporter(settings.RecipeCatalog, settings.Vocabulary)
                .ExportNewAsync(layout, merged.ToSequenceDocument(), path, "2026.03", CancellationToken.None);
            var xsq = new XsqStructuralReader().Read(path);

            var treePlacements = Assert.Single(xsq.ElementEffects, e => e.Name == "Tree").Effects;
            var archPlacements = Assert.Single(xsq.ElementEffects, e => e.Name == "Arch").Effects;

            // Same effect-type sequence, same relative layer order/timing (both compiled against an
            // identical section/bar grid, so absolute start/end ticks must match too).
            Assert.Equal(treePlacements.Count, archPlacements.Count);
            Assert.True(treePlacements.Count >= 3, "Expected at least the Mode B discrete 'On' windows plus the Mode A 'Shockwave' movement effect.");

            for (var i = 0; i < treePlacements.Count; i++)
            {
                var tree = treePlacements[i];
                var arch = archPlacements[i];

                Assert.Equal(tree.Name, arch.Name); // same effect type at the same relative position
                Assert.Equal(tree.StartTimeMs, arch.StartTimeMs); // same relative timing
                Assert.Equal(tree.EndTimeMs, arch.EndTimeMs);

                // Same EffectDB settings content (blend mode / value-curve parameters) -- the exact text
                // an unconfirmed-value guess or a palette-leaked setting would have changed; palette data
                // is never embedded in this string (it is carried only via a separate palette index/table).
                Assert.Equal(xsq.EffectDb[tree.Ref], xsq.EffectDb[arch.Ref]);

                // Only the color actually differs: the two placements must reference DIFFERENT real
                // palette-table entries, and those entries' own raw text must differ.
                Assert.NotEqual(tree.Palette, arch.Palette);
                Assert.NotEqual(xsq.ColorPalettes[tree.Palette], xsq.ColorPalettes[arch.Palette]);
            }

            // Confirm the specific confirmed shapes both actually reached the real exported file, not
            // just "something matched": the step-30.1 confirmed blend mode, and the step-30.2 confirmed
            // Shockwave/Ramp movement parameter.
            Assert.Contains("T_CHOICE_LayerMethod=2 is Mask", string.Join("|", xsq.EffectDb));
            Assert.Contains("E_VALUECURVE_Shockwave_End_Radius=", string.Join("|", xsq.EffectDb));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
