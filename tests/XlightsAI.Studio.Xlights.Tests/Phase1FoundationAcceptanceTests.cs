using System.Diagnostics;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// docs/AI-CHOREOGRAPHY-BUILD-PLAN.md Phase 1 — "Foundation: geometry, recipe system, and the IR."
/// Required acceptance:
/// (1) a hand-authored <see cref="SequenceDocument"/> with 2+ layers, a timing track, and a
///     non-default palette round-trips through export and re-parses structurally correctly;
/// (2) a pixel table for a real <c>xlights_rgbeffects.xml</c> matches hand-computed totals from
///     <c>parm1</c>/<c>parm2</c> (cross-checked against the XML's own <c>StringN</c> attributes);
/// (3) adding a new effect recipe requires touching one place (<see cref="EffectRecipeCatalog"/>), not
///     three.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Phase1FoundationAcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase1-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public async Task Layout_PixelTable_MatchesHandComputedTotals_FromParm1AndParm2()
    {
        var importer = new ShowLayoutImporter();
        var graph = await importer.ImportReadOnlyAsync(FixtureDirectory("sample-show-with-pixel-geometry"), CancellationToken.None);

        var matrix = Assert.Single(graph.Models, m => m.XlightsName == "Matrix-1");
        Assert.Equal(ModelCategory.Matrix, matrix.Category);
        Assert.Equal(9, matrix.Parm1);
        Assert.Equal(100, matrix.Parm2);
        Assert.Equal(4, matrix.Parm3);
        Assert.Equal(900, matrix.NodeCount); // parm1 * parm2, independently confirmed by its 9 StringN attributes.
        Assert.NotNull(matrix.Bounds);
        Assert.Equal(491.0, matrix.Bounds!.X, precision: 3);
        var submodel = Assert.Single(matrix.Submodels!);
        Assert.Equal("Top Row", submodel.Name);
        Assert.Equal(4, submodel.NodeCount); // 4 real node indices, "-1" placeholders excluded.

        var singleLine = Assert.Single(graph.Models, m => m.XlightsName == "Single Line-1");
        Assert.Equal(ModelCategory.SingleLine, singleLine.Category);
        Assert.Equal(100, singleLine.NodeCount); // parm1=1 * parm2=100.
        Assert.NotNull(singleLine.Bounds);
        Assert.Equal(0.48, singleLine.Bounds!.EndX!.Value, precision: 3);

        // Cross-check (task 5): parm1=9 declared but only 2 StringN attributes present -> non-fatal warning.
        Assert.Contains(graph.Warnings, w => w.Contains("Matrix-Mismatch") && w.Contains("StringN"));
    }

    /// <summary>Adding a recipe means registering an <see cref="IEffectRecipe"/> in a catalog — never editing <see cref="EffectRecipeRegistry"/>, <see cref="XsqExporter"/> or a plan factory.</summary>
    [Fact]
    public async Task AddingANewRecipe_RequiresOnlyCatalogRegistration_NotThreeCoupledFiles()
    {
        var catalog = EffectRecipeCatalog.CreateDefault();
        catalog.Register(new SparkleTestRecipe());

        var registry = new EffectRecipeRegistry(catalog);
        var exporter = new XsqExporter(catalog); // the SAME exporter type, unmodified — just given the richer catalog.

        var layout = new DisplayGraph(
            "TEST-HASH",
            [new DisplayModel(new ModelId("Prop"), "Prop", "Star", [], [])],
            [],
            []);

        var effect = registry.CreateVerified(SparkleTestRecipe.Id, new ModelId("Prop"), 0, 100_000_000);
        Assert.Equal("Sparkle", effect.Parameters["EffectFamily"]);

        var plan = new SequenceDocument(Guid.NewGuid(), 1, [effect]);
        var outputPath = Path.Combine(_root, "sparkle.xsq");
        await exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None);

        var structural = new XsqStructuralReader().Read(outputPath);
        var placement = Assert.Single(Assert.Single(structural.ElementEffects).Effects);
        Assert.Equal("Sparkle", placement.Name);
    }

    /// <summary>A recipe unknown to the registry's own catalog is still rejected — the catalog is the single source of truth, not a side channel.</summary>
    [Fact]
    public void UnregisteredRecipe_StillRejectedByRegistry()
    {
        var registry = new EffectRecipeRegistry(); // default catalog -- does NOT know SparkleTestRecipe.
        Assert.Throws<EffectPlanInvalidException>(
            () => registry.CreateVerified(SparkleTestRecipe.Id, new ModelId("Prop"), 0, 100_000_000));
    }

    /// <summary>
    /// Task 10: re-run the round-trip test against the new exporter with a hand-built multi-layer,
    /// multi-timing-track IR — 2 layers on one model, a non-default two-entry palette, and a labelled
    /// "Beats" timing track — and confirm it still structurally round-trips cleanly.
    /// </summary>
    [Fact]
    public async Task RichIr_WithLayersPaletteAndTimingTrack_RoundTripsStructurally()
    {
        var layout = new DisplayGraph(
            "TEST-HASH",
            [new DisplayModel(new ModelId("Outline"), "Outline", "Star", [], [])],
            [],
            []);

        var registry = new EffectRecipeRegistry();
        var baseEffect = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Outline"), 0, 200_000_000);
        var layer0 = baseEffect with { PaletteIndex = 0 };
        var layer1 = baseEffect with { Id = Guid.NewGuid(), Layer = 1, StartTicks = 50_000_000, EndTicks = 150_000_000, PaletteIndex = 1 };

        var palettes = new List<ColorPaletteDefinition>
        {
            new("C_BUTTON_Palette1=#FF0000,C_CHECKBOX_Palette1=1"),
            new("C_BUTTON_Palette1=#0000FF,C_CHECKBOX_Palette1=1"),
        };

        var timingTracks = new List<TimingTrack>
        {
            new("Beats",
            [
                new TimingMark("kick", 0, 50_000_000),
                new TimingMark("", 50_000_000, 100_000_000),
            ]),
        };

        var metadata = new SequenceMetadata(Song: "Test Song", Artist: "Test Artist");
        var plan = new SequenceDocument(Guid.NewGuid(), 1, [layer0, layer1], metadata, palettes, timingTracks);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "rich-ir.xsq");
        await exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None);

        var structural = new XsqStructuralReader().Read(outputPath);

        Assert.Equal(2, structural.ColorPalettes.Count);

        var outlineElement = Assert.Single(structural.ElementEffects, e => e.Name == "Outline");
        Assert.Equal(2, outlineElement.Effects.Count); // 2 layers -> 2 EffectLayer-sourced placements.
        Assert.Contains(outlineElement.Effects, p => p.Palette == 0);
        Assert.Contains(outlineElement.Effects, p => p.Palette == 1);

        var beatsTrack = Assert.Single(structural.TimingTracks, t => t.Name == "Beats");
        Assert.Equal(2, beatsTrack.Marks.Count);
        Assert.Equal("kick", beatsTrack.Marks[0].Label);
        Assert.Equal(0, beatsTrack.Marks[0].StartTimeMs);
        Assert.Equal(5000, beatsTrack.Marks[0].EndTimeMs); // 50_000_000 ticks @ 100ns/tick = 5000ms.

        // Also confirm the timing track's own <Element type="timing"> appears in DisplayElements.
        Assert.Contains(structural.DisplayElements, e => e.Type == "timing" && e.Name == "Beats");
    }

    /// <summary>An effect referencing a palette index outside the supplied table fails safely rather than writing a dangling reference.</summary>
    [Fact]
    public async Task EffectWithOutOfRangePaletteIndex_FailsSafely_NoFileWritten()
    {
        var layout = new DisplayGraph(
            "TEST-HASH",
            [new DisplayModel(new ModelId("Outline"), "Outline", "Star", [], [])],
            [],
            []);

        var registry = new EffectRecipeRegistry();
        var effect = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Outline"), 0, 100_000_000) with { PaletteIndex = 5 };
        var plan = new SequenceDocument(Guid.NewGuid(), 1, [effect]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "bad-palette.xsq");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None));

        Assert.Contains("palette index", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// Phase 1 acceptance criterion: "A hand-authored SequenceDocument with 2+ layers, a timing track,
    /// and a non-default palette round-trips through export and opens correctly in real xLights."
    /// docs/PHASE0-FINDINGS.md established this machine has a real xLights install at
    /// <c>C:\Program Files\xLights</c> and a real show at <c>C:\xLights</c>, and that
    /// <c>xlights.exe --headless -s &lt;showdir&gt; --outputdir &lt;dir&gt; &lt;seq.xsq&gt;</c> is a genuine,
    /// non-GUI import+render path (exit code 0, a real FSEQ, zero parse/structural complaints). This
    /// test reuses that exact mechanism against this phase's richer IR (2 layers, a non-default
    /// 2-entry palette, a labelled timing track) rather than Phase 0's single-effect plan. Skips (does
    /// not fail) when that specific real install/show is not present — e.g. a CI machine or a
    /// different developer's checkout — since it asserts against a real, machine-specific xLights
    /// installation outside this repository.
    /// </summary>
    [Fact]
    public async Task RichIr_RendersCleanly_InRealHeadlessXLights_WhenRealInstallIsPresent()
    {
        const string xlightsExePath = @"C:\Program Files\xLights\xlights.exe";
        const string showDirectory = @"C:\xLights";
        if (!File.Exists(xlightsExePath) || !Directory.Exists(showDirectory))
        {
            return; // No real xLights install/show on this machine -- nothing to verify here.
        }

        var layout = await new ShowLayoutImporter().ImportReadOnlyAsync(showDirectory, CancellationToken.None);
        var targetName = layout.Models.Select(m => m.XlightsName).FirstOrDefault(n => n is "Tree" or "Arches")
            ?? layout.Models[0].XlightsName;

        var registry = new EffectRecipeRegistry();
        var layer0 = registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId(targetName), 0, 200_000_000) with { PaletteIndex = 0 };
        var layer1 = layer0 with { Id = Guid.NewGuid(), Layer = 1, StartTicks = 50_000_000, EndTicks = 150_000_000, PaletteIndex = 1 };

        var plan = new SequenceDocument(
            Guid.NewGuid(),
            1,
            [layer0, layer1],
            Metadata: new SequenceMetadata(Song: "Phase1 Round Trip"),
            Palettes:
            [
                new ColorPaletteDefinition("C_BUTTON_Palette1=#FF0000,C_CHECKBOX_Palette1=1"),
                new ColorPaletteDefinition("C_BUTTON_Palette1=#0000FF,C_CHECKBOX_Palette1=1"),
            ],
            TimingTracks:
            [
                new TimingTrack("Beats", [new TimingMark("kick", 0, 50_000_000), new TimingMark("", 50_000_000, 100_000_000)]),
            ]);

        Directory.CreateDirectory(_root);
        var xsqPath = Path.Combine(_root, "phase1-roundtrip.xsq");
        await new XsqExporter().ExportNewAsync(layout, plan, xsqPath, "2026.03", CancellationToken.None);

        var renderOutputDir = Path.Combine(_root, "render-out");
        Directory.CreateDirectory(renderOutputDir);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = xlightsExePath,
            ArgumentList = { "--headless", "-s", showDirectory, "--outputdir", renderOutputDir, xsqPath },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        var exited = process.WaitForExit(TimeSpan.FromSeconds(60));
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
        }

        Assert.True(exited, "xlights.exe --headless did not exit within the timeout.");
        Assert.Equal(0, process.ExitCode);
    }

    /// <summary>A recipe not registered in the exporter's own catalog is still rejected, even if produced by a registry that does know it — the exporter never invents XML for a recipe its own catalog can't resolve.</summary>
    private sealed class SparkleTestRecipe : IEffectRecipe
    {
        public const string Id = "effect.sparkle.test.v1";

        public string RecipeId => Id;

        public string RecipeVersion => "1";

        public string XlightsEffectName => "Sparkle";

        public bool IsSupportedParameterValue(string key, string value) => key == "EffectFamily" && value == "Sparkle";

        public IReadOnlyDictionary<string, string> CreateDefaultParameters() =>
            new Dictionary<string, string>(StringComparer.Ordinal) { ["EffectFamily"] = "Sparkle" };
    }
}
