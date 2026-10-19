using System.Xml.Linq;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-02 Step 015 ("Implement atomic export, filename safety, backups and golden XSQ regression
/// tests"). Required acceptance: "Never overwrite source without explicit approval; no regression in
/// fixture output." Failure/edge case: "Invalid model/format/version must fail safely without
/// rewriting source files." These tests exercise the real <see cref="XsqExporter"/> against a real,
/// already-validated <see cref="DisplayGraph"/>/<see cref="SequenceDocument"/> (the exact gate-demo
/// shape, matching Step013AcceptanceTests' own fixture) and real file-system I/O — not mocks.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step015AcceptanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step015-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>The gate demo's own layout shape (PHASE-02 Section 1: Roofline, Arch 1, Arch 2), matching Step013AcceptanceTests' own <c>GateDemoLayout()</c>.</summary>
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

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "golden", name);

    /// <summary>
    /// Normalizes an XSQ document for a semantic (not byte/whitespace) comparison: attributes sorted by
    /// name (order is never meaningful in this exporter's own output), insignificant whitespace-only
    /// text dropped, and leaf-element text content trimmed. PHASE-02 Step 015's own required acceptance
    /// text: "compare golden semantic XML, not only whitespace."
    /// </summary>
    private static bool IsSemanticallyEqualToGolden(string actualPath, string goldenPath)
    {
        static XElement Normalize(XElement e)
        {
            var children = e.Elements().ToList();
            return children.Count == 0
                ? new XElement(e.Name, e.Attributes().OrderBy(a => a.Name.ToString(), StringComparer.Ordinal), e.Value.Trim())
                : new XElement(e.Name, e.Attributes().OrderBy(a => a.Name.ToString(), StringComparer.Ordinal), children.Select(Normalize));
        }

        var actual = Normalize(XDocument.Load(actualPath).Root!);
        var golden = Normalize(XDocument.Load(goldenPath).Root!);
        return XNode.DeepEquals(actual, golden);
    }

    /// <summary>
    /// Required acceptance (regression half): the real exporter's output for the phase gate's own
    /// exact demo shape still semantically matches the frozen golden fixture captured from Step 013's
    /// own verified, independently-inspected output (STEP-13.md Section 6) — not merely "parses", and
    /// not a brittle byte-for-byte/whitespace comparison.
    /// </summary>
    [Fact]
    public async Task Step015_Acceptance_Passes_MatchesGoldenFixtureSemantically()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var outputPath = Path.Combine(_root, "sequence.xsq");

        await new XsqExporter().ExportAsync(layout, plan, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, CancellationToken.None);

        var goldenPath = FixturePath("golden-three-effect-sequence.xsq");
        Assert.True(File.Exists(goldenPath), $"Golden fixture missing at '{goldenPath}'.");
        Assert.True(
            IsSemanticallyEqualToGolden(outputPath, goldenPath),
            $"Exported XSQ no longer matches the frozen golden fixture.\nActual:\n{await File.ReadAllTextAsync(outputPath)}\nGolden:\n{await File.ReadAllTextAsync(goldenPath)}");
    }

    /// <summary>Required acceptance (filename-safety half): a colliding output path never overwrites the existing file; the exporter instead picks the next available "name (1).xsq" path and reports the real path it actually used.</summary>
    [Fact]
    public async Task AppendCollisionSuffix_NeverOverwrites_PicksNextAvailablePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var outputPath = Path.Combine(_root, "sequence.xsq");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(outputPath, "original, must survive untouched");

        var exporter = new XsqExporter();
        var result = await exporter.ExportAsync(layout, plan, outputPath, "2026.03", XsqExportPolicy.AppendCollisionSuffix, CancellationToken.None);

        Assert.Equal("original, must survive untouched", await File.ReadAllTextAsync(outputPath)); // never overwritten
        Assert.Equal(Path.Combine(_root, "sequence (1).xsq"), result.Path);
        Assert.True(File.Exists(result.Path));
        Assert.Null(result.BackupPath); // nothing was overwritten, so nothing was backed up

        // A second collision picks the next available suffix in turn, rather than colliding with the first.
        var result2 = await exporter.ExportAsync(layout, plan, outputPath, "2026.03", XsqExportPolicy.AppendCollisionSuffix, CancellationToken.None);
        Assert.Equal(Path.Combine(_root, "sequence (2).xsq"), result2.Path);
        Assert.Equal("original, must survive untouched", await File.ReadAllTextAsync(outputPath));
    }

    /// <summary>Required acceptance (explicit-approval half): <see cref="XsqExportPolicy.OverwriteWithBackup"/> IS the caller's own explicit approval — the pre-existing file's real bytes are preserved at a reported backup path before being replaced, never silently discarded.</summary>
    [Fact]
    public async Task OverwriteWithBackup_PreservesOriginalBytesBeforeReplacing()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var outputPath = Path.Combine(_root, "sequence.xsq");
        Directory.CreateDirectory(_root);
        const string originalContent = "original content that must be preserved in a backup";
        await File.WriteAllTextAsync(outputPath, originalContent);

        var result = await new XsqExporter().ExportAsync(layout, plan, outputPath, "2026.03", XsqExportPolicy.OverwriteWithBackup, CancellationToken.None);

        Assert.Equal(outputPath, result.Path); // same path as requested, unlike AppendCollisionSuffix
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(originalContent, await File.ReadAllTextAsync(result.BackupPath!)); // the real original bytes, intact
        Assert.NotEqual(originalContent, await File.ReadAllTextAsync(outputPath)); // the real new export now lives at outputPath
        Assert.Contains("xsequence", await File.ReadAllTextAsync(outputPath), StringComparison.Ordinal);
    }

    /// <summary><see cref="IXsqExporter.ExportNewAsync"/> (Step 013's own original method, still present and unmodified) remains exactly equivalent to <see cref="XsqExportPolicy.RejectIfExists"/> — it still fails safely on a pre-existing file and never overwrites it.</summary>
    [Fact]
    public async Task ExportNewAsync_StillRejectsExistingFile_BackwardCompatible_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var outputPath = Path.Combine(_root, "sequence.xsq");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(outputPath, "not touched");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => new XsqExporter().ExportNewAsync(layout, plan, outputPath, "2026.03", CancellationToken.None));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("not touched", await File.ReadAllTextAsync(outputPath));
    }

    /// <summary>Required failure/edge case: "Invalid model/format/version must fail safely without rewriting source files" — an unresolved effect target is still rejected before any file is written, for every policy, not only the default.</summary>
    [Theory]
    [InlineData(XsqExportPolicy.RejectIfExists)]
    [InlineData(XsqExportPolicy.AppendCollisionSuffix)]
    [InlineData(XsqExportPolicy.OverwriteWithBackup)]
    public async Task UnresolvedEffectTarget_FailsSafely_ForEveryPolicy_NegativePath(XsqExportPolicy policy)
    {
        var layout = GateDemoLayout();
        var registry = new EffectRecipeRegistry();
        var plan = new SequenceDocument(Guid.NewGuid(), 1,
        [
            registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Ghost-Prop"), 0, 100_000_000),
        ]);

        var outputPath = Path.Combine(_root, $"unresolved-{policy}.xsq");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => new XsqExporter().ExportAsync(layout, plan, outputPath, "2026.03", policy, CancellationToken.None));

        Assert.Contains("Ghost-Prop", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>An unknown/invalid policy value fails safely before any file is written.</summary>
    [Fact]
    public async Task UnknownPolicy_FailsSafely_NoFileWritten_NegativePath()
    {
        var layout = GateDemoLayout();
        var plan = GateDemoPlan(layout);
        var outputPath = Path.Combine(_root, "sequence.xsq");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(outputPath, "existing");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(
            () => new XsqExporter().ExportAsync(layout, plan, outputPath, "2026.03", (XsqExportPolicy)999, CancellationToken.None));

        Assert.Contains("Unknown export policy", ex.Message, StringComparison.Ordinal);
        Assert.Equal("existing", await File.ReadAllTextAsync(outputPath)); // never touched
    }
}
