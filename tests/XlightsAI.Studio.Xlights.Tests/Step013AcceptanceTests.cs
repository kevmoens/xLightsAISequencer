using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-02 Step 013 — "Port minimal editable XSQ writer and required sequencing metadata."
/// Required acceptance: "Generated file passes XML/structural tests and round-trip parser."
/// Failure/edge case: "Invalid model/format/version must fail safely without rewriting source
/// files." These tests exercise the real <see cref="XsqExporter"/> and <see cref="XsqStructuralReader"/>
/// against a real, already-validated <see cref="DisplayGraph"/>/<see cref="SequenceDocument"/> (built
/// the same way Step 012's own gate-demo fixture is), and real file-system I/O — not mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step013AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step013-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// The exact gate-demo targets/plan shape (PHASE-02 Section 1). Proves the full, real domain -&gt;
    /// adapter pathway: the exporter writes a real <c>.xsq</c> file, and the required acceptance
    /// itself — a structural roundtrip through the real reference reader — matches every field of the
    /// original <see cref="SequenceDocument"/>/<see cref="DisplayGraph"/> exactly (no dangling
    /// EffectDB/palette refs, models/groups correctly typed, timing round-trips to the millisecond).
    /// </summary>
    [Fact]
    public async Task Step013_Acceptance_Passes_GeneratedFilePassesStructuralAndRoundTripTests()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();
        const long songDurationTicks = 300_000_000; // 30s at 100ns/tick.
        var plan = factory.CreatePlan(layout, ["Roofline", "Arch 1", "Arch 2"], songDurationTicks, registry);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "sequence.xsq");

        var result = await exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None);

        Assert.Equal(outputPath, result.Path);
        Assert.True(File.Exists(outputPath));
        Assert.False(string.IsNullOrWhiteSpace(result.Sha256));
        Assert.NotEmpty(result.Warnings); // the disclosed "On" settings placeholder (see XsqExporter's own doc).

        // Required acceptance: a round-trip through the real reference reader, not just "XML parsed
        // without throwing" — every field is checked against the original plan/layout.
        var reader = new XsqStructuralReader();
        var structural = reader.Read(outputPath);

        Assert.Equal("2026.03", structural.XlightsVersion);
        Assert.Equal(XsqExporter.FrameIntervalMs, structural.FrameIntervalMs);
        Assert.Equal(30.0, structural.SequenceDurationSeconds, precision: 3);

        Assert.Single(structural.ColorPalettes);
        Assert.Single(structural.EffectDb);

        Assert.Equal(3, structural.DisplayElements.Count);
        Assert.Equal(["Roofline", "Arch 1", "Arch 2"], structural.DisplayElements.Select(e => e.Name));
        Assert.All(structural.DisplayElements, e => Assert.Equal("model", e.Type));

        Assert.Equal(3, structural.ElementEffects.Count);
        for (var i = 0; i < plan.Effects.Count; i++)
        {
            var expected = plan.Effects[i];
            var actualElement = structural.ElementEffects[i];
            Assert.Equal(expected.Target.Value, actualElement.Name);
            var placement = Assert.Single(actualElement.Effects);
            Assert.Equal(0, placement.Ref); // single shared EffectDB entry -> never dangling.
            Assert.Equal(0, placement.Palette); // single shared palette -> never dangling.
            Assert.Equal("On", placement.Name);
            Assert.Equal(TimeSpan.FromTicks(expected.StartTicks).TotalMilliseconds, placement.StartTimeMs);
            Assert.Equal(TimeSpan.FromTicks(expected.EndTicks).TotalMilliseconds, placement.EndTimeMs);
        }
    }

    /// <summary>
    /// A plan may equally target a real model group (Step 011/012's own targetable entity). PHASE-00
    /// finding (docs/PHASE0-FINDINGS.md): real xLights types both models AND model groups as
    /// <c>type="model"</c> in DisplayElements/ElementEffects -- <c>type="modelGroup"</c> was never
    /// observed in any of the 20 real .xsq files inspected -- so the exported element is typed
    /// <c>model</c>, not <c>modelGroup</c>. Group membership is still validated at export time (an
    /// unresolved target still fails safely); only the emitted XML shape changed.
    /// </summary>
    [Fact]
    public async Task Export_TargetingARealModelGroup_IsTypedAsModel()
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var factory = new ThreeEffectPlanFactory();
        var plan = factory.CreatePlan(layout, ["Roofline", "Arch 1", "Front-Arches"], 300_000_000, registry);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "group-target.xsq");
        await exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None);

        var structural = new XsqStructuralReader().Read(outputPath);
        var groupElement = structural.DisplayElements.Single(e => e.Name == "Front-Arches");
        Assert.Equal("model", groupElement.Type);
    }

    [Fact]
    public async Task ExistingOutputFile_FailsSafely_DoesNotOverwrite_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = OneEffectPlan(layout);
        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "existing.xsq");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(outputPath, "not touched");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("not touched", await File.ReadAllTextAsync(outputPath)); // never overwritten.
    }

    [Fact]
    public async Task UnresolvedEffectTarget_FailsSafely_NoFileWritten_NegativePath()
    {
        // A SequenceDocument built against a DIFFERENT layout than the one supplied to the exporter —
        // defense-in-depth, independent of ThreeEffectPlanFactory's own validation.
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var plan = new SequenceDocument(Guid.NewGuid(), 1,
        [
            registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Ghost-Prop"), 0, 100_000_000),
        ]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "unresolved.xsq");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None));

        Assert.Contains("Ghost-Prop", ex.Message);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task UnknownRecipeId_FailsSafely_NoFileWritten_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = new SequenceDocument(Guid.NewGuid(), 1,
        [
            new EffectInstance(Guid.NewGuid(), new ModelId("Roofline"), 0, 0, 100_000_000, "effect.sparkle.v1", "1",
                new Dictionary<string, string>()),
        ]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "unknown-recipe.xsq");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None));

        Assert.Contains("effect.sparkle.v1", ex.Message);
        Assert.False(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sequence.xml")] // wrong extension -- "format" must fail safely.
    public async Task InvalidOutputPath_FailsSafely_NoFileWritten_NegativePath(string rawPath)
    {
        var layout = GateDemoLayout();
        var plan = OneEffectPlan(layout);
        var exporter = new XsqExporter();
        var outputPath = string.IsNullOrWhiteSpace(rawPath) ? rawPath : Path.Combine(_root, rawPath);

        await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None));

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Assert.False(File.Exists(outputPath));
        }
    }

    [Fact]
    public async Task BlankTargetXlightsVersion_FailsSafely_NoFileWritten_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = OneEffectPlan(layout);
        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "blank-version.xsq");

        await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportNewAsync(layout, plan, outputPath, "   ", CancellationToken.None));

        Assert.False(File.Exists(outputPath));
    }

    /// <summary>The reference reader itself fails safely (never returns a silently-empty/default structural document) against a file that is not well-formed XML.</summary>
    [Fact]
    public async Task StructuralReader_MalformedXml_FailsSafely_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var malformedPath = Path.Combine(_root, "malformed.xsq");
        await File.WriteAllTextAsync(malformedPath, "<xsequence><head></xsequence>");

        Assert.Throws<XsqStructuralReadException>(() => new XsqStructuralReader().Read(malformedPath));
    }

    /// <summary>The reference reader rejects a dangling EffectDB ref rather than silently returning it — PHASE-02 Section 12's own "no dangling palette or EffectDB refs" serializer invariant, verified from the reader side too.</summary>
    [Fact]
    public void StructuralReader_DanglingEffectDbRef_FailsSafely_NegativePath()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "dangling-ref.xsq");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <xsequence BaseChannel="0" FixedPointTiming="25" ModelBlending="true">
              <head><version>2026.03</version><sequenceType>Animation</sequenceType><sequenceTiming>25 ms</sequenceTiming><sequenceDuration>1.000</sequenceDuration></head>
              <ColorPalettes><ColorPalette>C_BUTTON_Palette1=#FFFFFF</ColorPalette></ColorPalettes>
              <EffectDB><Effect></Effect></EffectDB>
              <DisplayElements><Element type="model" name="Roofline" visible="1" /></DisplayElements>
              <ElementEffects>
                <Element type="model" name="Roofline">
                  <EffectLayer><Effect ref="5" name="On" startTime="0" endTime="1000" palette="0" selected="0" /></EffectLayer>
                </Element>
              </ElementEffects>
            </xsequence>
            """);

        var ex = Assert.Throws<XsqStructuralReadException>(() => new XsqStructuralReader().Read(path));
        Assert.Contains("dangling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SequenceDocument OneEffectPlan(DisplayGraph layout)
    {
        var registry = new EffectRecipeRegistry();
        return new SequenceDocument(Guid.NewGuid(), 1,
        [
            registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId(layout.Models[0].XlightsName), 0, 100_000_000),
        ]);
    }

    /// <summary>Builds the gate demo's own layout in-memory (Roofline, Arch 1, Arch 2, plus a model group), matching PHASE-02 Section 1's exact phase-gate prop names.</summary>
    private static DisplayGraph GateDemoLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(new ModelId("Roofline"), "Roofline", "Star", ["Front-Arches"], []),
            new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", ["Front-Arches"], []),
            new DisplayModel(new ModelId("Arch 2"), "Arch 2", "Arches", ["Front-Arches"], []),
        ],
        Groups:
        [
            new DisplayModelGroup("Front-Arches",
            [
                new DisplayGroupMember("Roofline", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch 1", DisplayGroupMemberKind.Model),
                new DisplayGroupMember("Arch 2", DisplayGroupMemberKind.Model),
            ]),
        ],
        Warnings: []);
}
