using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-4 prerequisite — closes the gap disclosed throughout Phase 1/2 ("XsqExporter does not yet
/// serialize [settings/value curves]"): with a real <see cref="EffectVocabularyCatalog"/> supplied,
/// <see cref="XsqExporter"/> now serializes a real, verified settings string per effect (parameters +
/// value curves, via <see cref="EffectSettingsSerializer"/>) and performs real content-addressed
/// <c>EffectDB</c> dedup instead of every effect sharing the single hardcoded empty placeholder. Without
/// Mode A/B emission (architecture §6) would be meaningless -- a compiled value curve bound to a timing
/// track would never actually reach the exported file.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Phase04VocabularyAwareExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase04-export-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static EffectVocabularyCatalog BuildFixtureVocabulary() =>
        EffectVocabularyBuilder.BuildFromDirectory(Path.Combine(AppContext.BaseDirectory, "Fixtures", "effectmetadata"));

    private static DisplayGraph OneModelLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models: [new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [])],
        Groups: [],
        Warnings: []);

    private static (EffectRecipeCatalog RecipeCatalog, EffectVocabularyCatalog Vocabulary) BuildCatalogs()
    {
        var vocabulary = BuildFixtureVocabulary();
        var recipeCatalog = EffectRecipeCatalog.CreateDefault();
        GeneratedEffectRecipeFactory.RegisterAll(vocabulary, recipeCatalog);
        return (recipeCatalog, vocabulary);
    }

    [Fact]
    public async Task ExportAsync_WithVocabulary_SerializesRealNonDefaultParameters()
    {
        var (recipeCatalog, vocabulary) = BuildCatalogs();
        var layout = OneModelLayout();

        var effect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Tree"), Layer: 0, StartTicks: 0, EndTicks: 10_000_000,
            RecipeId: "effect.generated.bars.v1", RecipeVersion: "1",
            Parameters: new Dictionary<string, string>
            {
                ["E_SLIDER_Bars_BarCount"] = "5",
                ["E_CHOICE_Bars_Direction"] = "up", // default -- must be omitted from the serialized string.
            });
        var document = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: [effect]);

        var exporter = new XsqExporter(recipeCatalog, vocabulary);
        var outputPath = Path.Combine(_root, "sequence.xsq");
        var result = await exporter.ExportAsync(layout, document, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        Assert.True(File.Exists(outputPath));

        var reader = new XsqStructuralReader();
        var structural = reader.Read(outputPath);

        Assert.Single(structural.EffectDb);
        Assert.Equal("E_SLIDER_Bars_BarCount=5", structural.EffectDb[0]);
        Assert.Equal(0, structural.ElementEffects.Single().Effects.Single().Ref);

        // No more "disclosed placeholder" warning when a real vocabulary is supplied.
        Assert.DoesNotContain(result.Warnings, w => w.Contains("EffectVocabularyCatalog", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportAsync_WithVocabulary_SerializesValueCurve_BoundToTimingTrack()
    {
        var (recipeCatalog, vocabulary) = BuildCatalogs();
        var layout = OneModelLayout();

        var effect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Tree"), Layer: 0, StartTicks: 0, EndTicks: 10_000_000,
            RecipeId: "effect.generated.fire.v1", RecipeVersion: "1",
            Parameters: new Dictionary<string, string>(),
            ValueCurves:
            [
                new EffectValueCurve("E_SLIDER_Fire_Height", "Ramp", Min: 0, Max: 100, P1: 20, P2: 80, TimingTrack: "Beats", FilterLabel: "kick"),
            ]);
        var document = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: [effect]);

        var exporter = new XsqExporter(recipeCatalog, vocabulary);
        var outputPath = Path.Combine(_root, "sequence.xsq");
        await exporter.ExportAsync(layout, document, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        var settingsString = (await File.ReadAllTextAsync(outputPath))
            .Split("<EffectDB>")[1].Split("</EffectDB>")[0];

        Assert.Contains("E_VALUECURVE_Fire_Height", settingsString);
        Assert.Contains("Type=Ramp", settingsString);
        Assert.Contains("TT=Beats", settingsString);
        Assert.Contains("FT=kick", settingsString);
    }

    [Fact]
    public async Task ExportAsync_WithVocabulary_TwoEffectsWithIdenticalSettings_ShareOneEffectDbEntry()
    {
        var (recipeCatalog, vocabulary) = BuildCatalogs();
        var layout = new DisplayGraph(
            "TEST-HASH-NOT-A-REAL-FILE",
            Models:
            [
                new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], []),
                new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arches", [], []),
            ],
            Groups: [],
            Warnings: []);

        var parameters = new Dictionary<string, string> { ["E_SLIDER_Bars_BarCount"] = "7" };
        var effect1 = new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, 0, 10_000_000, "effect.generated.bars.v1", "1", parameters);
        var effect2 = new EffectInstance(Guid.NewGuid(), new ModelId("Arch 1"), 0, 0, 10_000_000, "effect.generated.bars.v1", "1", parameters);
        var document = new SequenceDocument(Guid.NewGuid(), 1, [effect1, effect2]);

        var exporter = new XsqExporter(recipeCatalog, vocabulary);
        var outputPath = Path.Combine(_root, "sequence.xsq");
        await exporter.ExportAsync(layout, document, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        var reader = new XsqStructuralReader();
        var structural = reader.Read(outputPath);

        Assert.Single(structural.EffectDb);
        Assert.All(
            structural.ElementEffects.SelectMany(e => e.Effects),
            e => Assert.Equal(0, e.Ref));
    }

    [Fact]
    public async Task ExportAsync_WithVocabulary_UnknownParameter_ThrowsAndWritesNoFile()
    {
        var (recipeCatalog, vocabulary) = BuildCatalogs();
        var layout = OneModelLayout();

        var effect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Tree"), 0, 0, 10_000_000, "effect.generated.bars.v1", "1",
            new Dictionary<string, string> { ["E_SLIDER_DoesNotExist"] = "1" });
        var document = new SequenceDocument(Guid.NewGuid(), 1, [effect]);

        var exporter = new XsqExporter(recipeCatalog, vocabulary);
        var outputPath = Path.Combine(_root, "sequence.xsq");

        await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => exporter.ExportAsync(layout, document, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None));
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task ExportAsync_WithoutVocabulary_PreservesExactPreviousBehavior()
    {
        var layout = OneModelLayout();
        var effect = new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, 0, 10_000_000, "effect.on.v1", "1", new Dictionary<string, string>());
        var document = new SequenceDocument(Guid.NewGuid(), 1, [effect]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "sequence.xsq");
        var result = await exporter.ExportNewAsync(layout, document, outputPath, "2026.03", CancellationToken.None);

        var reader = new XsqStructuralReader();
        var structural = reader.Read(outputPath);

        Assert.Single(structural.EffectDb);
        Assert.Equal("", structural.EffectDb[0]);
        Assert.Contains(result.Warnings, w => w.Contains("EffectVocabularyCatalog", StringComparison.Ordinal));
    }

    /// <summary>
    /// Confirmed as a real bug against a real xLights install: a hardcoded "Animation" sequenceType
    /// opened fine (xLights recognized the mediaFile) but played no audio at all, where a real working
    /// .xsq with the same real mediaFile used "Media" instead -- xLights apparently uses sequenceType,
    /// not just a non-empty mediaFile, to decide whether a sequence is audio-driven.
    /// </summary>
    [Fact]
    public async Task ExportAsync_WithRealMediaFile_WritesSequenceTypeMedia_NotAnimation()
    {
        var layout = OneModelLayout();
        var effect = new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, 0, 10_000_000, "effect.on.v1", "1", new Dictionary<string, string>());
        var document = new SequenceDocument(Guid.NewGuid(), 1, [effect], Metadata: new SequenceMetadata(MediaFile: @"C:\xLights\song.mp3"));

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "sequence.xsq");
        await exporter.ExportNewAsync(layout, document, outputPath, "2026.03", CancellationToken.None);

        var xml = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("<sequenceType>Media</sequenceType>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<sequenceType>Animation</sequenceType>", xml, StringComparison.Ordinal);
    }

    /// <summary>Companion to the test above: an empty mediaFile (no real audio at all) must still write "Animation", exactly as the golden fixture already expects -- never a regression for a media-less sequence.</summary>
    [Fact]
    public async Task ExportAsync_WithNoMediaFile_StillWritesSequenceTypeAnimation()
    {
        var layout = OneModelLayout();
        var effect = new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, 0, 10_000_000, "effect.on.v1", "1", new Dictionary<string, string>());
        var document = new SequenceDocument(Guid.NewGuid(), 1, [effect]);

        var exporter = new XsqExporter();
        var outputPath = Path.Combine(_root, "sequence.xsq");
        await exporter.ExportNewAsync(layout, document, outputPath, "2026.03", CancellationToken.None);

        var xml = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("<sequenceType>Animation</sequenceType>", xml, StringComparison.Ordinal);
    }
}
