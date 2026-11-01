using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-02 Step 010 — "Parse xlights_rgbeffects.xml into normalized stable model IDs/groups/
/// geometry where present." Required acceptance: "Golden fixture verifies model names, groups and
/// positions." Failure/edge case: "Invalid model/format/version must fail safely without rewriting
/// source files." These tests exercise the real <see cref="ShowLayoutImporter"/> against real
/// fixture directories on disk (not a mocked file system), and assert the persisted domain state
/// (<see cref="DisplayGraph"/>) it returns, not merely that a call happened.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step010AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>
    /// Golden fixture: two models, each with explicit WorldPosX/WorldPosY geometry, and two
    /// overlapping modelGroups. Asserts exact model names, kinds, every group each model belongs to
    /// (overlap preserved, not flattened), and the parsed preview point for each model.
    /// </summary>
    [Fact]
    public async Task Step010_Acceptance_Passes_ModelsGroupsAndPositionsMatchGoldenFixture()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-with-geometry");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(graph.SourceLayoutHash));
        Assert.Equal(64, graph.SourceLayoutHash.Length); // SHA-256 hex digest.
        Assert.Equal(2, graph.Models.Count);

        var arch = Assert.Single(graph.Models, m => m.XlightsName == "Arch-01");
        Assert.Equal(new ModelId("Arch-01"), arch.Id);
        Assert.Equal("Arches", arch.Kind);
        Assert.Equal(["Front-Yard", "All-Props"], arch.GroupNames);
        var archPoint = Assert.Single(arch.PreviewPoints);
        Assert.Equal(10.5, archPoint.X);
        Assert.Equal(-2.25, archPoint.Y);

        var megaTree = Assert.Single(graph.Models, m => m.XlightsName == "MegaTree-Center");
        Assert.Equal(new ModelId("MegaTree-Center"), megaTree.Id);
        Assert.Equal("Tree 360", megaTree.Kind);
        Assert.Equal(["Front-Yard", "All-Props"], megaTree.GroupNames);
        var megaTreePoint = Assert.Single(megaTree.PreviewPoints);
        Assert.Equal(0, megaTreePoint.X);
        Assert.Equal(0, megaTreePoint.Y);
    }

    /// <summary>
    /// Real-run finding (2026-11-01): a real "Arches" model (confirmed against a real show's own
    /// SourceVersion "2026.17" export) carries NumArches/NodesPerArch instead of NumStrings/NodesPerString
    /// or the legacy parm1/parm2 -- without a fallback to those names, this model silently computed to 0
    /// nodes, which every downstream consumer correctly treats as "no real pixels," so the model never
    /// reached a compiled show at all (confirmed against two real exported .xsq files: zero "Arch"-named
    /// entries anywhere in DisplayElements, not merely zero effects on an entry that existed).
    /// </summary>
    [Fact]
    public async Task ArchesModelWithModernNumArchesNodesPerArchAttributes_ComputesRealNonzeroNodeCount()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-arches-modern-attributes");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        var arch = Assert.Single(graph.Models);
        Assert.Equal("Arches-1 Outside", arch.XlightsName);
        Assert.Equal(200, arch.NodeCount); // NumArches=2 * NodesPerArch=100, same formula as parm1*parm2.
    }

    /// <summary>Re-running the import against unchanged bytes yields the identical source layout hash (deterministic, read-only).</summary>
    [Fact]
    public async Task Import_IsDeterministic_SameBytesSameHash()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-with-geometry");

        var first = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);
        var second = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        Assert.Equal(first.SourceLayoutHash, second.SourceLayoutHash);
    }

    /// <summary>"Geometry where present": a fixture with no WorldPosX/WorldPosY attributes parses with empty preview points, never an invented position.</summary>
    [Fact]
    public async Task ModelsWithoutGeometry_HaveEmptyPreviewPoints_NeverGuessed()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-complete"); // Step 009's fixture: no WorldPosX/Y attributes.

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        Assert.Equal(2, graph.Models.Count);
        Assert.All(graph.Models, m => Assert.Empty(m.PreviewPoints));
        var arch = Assert.Single(graph.Models, m => m.XlightsName == "Arch-01");
        Assert.Equal(["Front-Yard"], arch.GroupNames);
    }

    [Fact]
    public async Task MissingDirectory_ThrowsActionableError_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var missingDirectory = Path.Combine(Path.GetTempPath(), "xlai-step010-does-not-exist-" + Guid.NewGuid().ToString("N"));

        var ex = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => importer.ImportReadOnlyAsync(missingDirectory, CancellationToken.None));

        Assert.Contains(missingDirectory, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(missingDirectory));
    }

    [Fact]
    public async Task MissingRgbEffectsFile_ThrowsActionableError_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-missing-rgbeffects");

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));

        Assert.Contains(ShowDirectoryFileNames.RgbEffects, ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidDirectoryInput_ThrowsActionableError_NegativePath(string invalidDirectory)
    {
        var importer = new ShowLayoutImporter();

        await Assert.ThrowsAsync<ArgumentException>(
            () => importer.ImportReadOnlyAsync(invalidDirectory, CancellationToken.None));
    }

    [Fact]
    public async Task AlreadyCancelledToken_ThrowsWithoutReadingAnything_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-with-geometry");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => importer.ImportReadOnlyAsync(directory, cts.Token));
    }

    /// <summary>"Invalid model/format/version must fail safely without rewriting source files": not-well-formed XML fails safely, the source file is untouched.</summary>
    [Fact]
    public async Task MalformedXml_FailsSafely_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-malformed-xml");
        var sourcePath = Path.Combine(directory, ShowDirectoryFileNames.RgbEffects);
        var originalBytes = await File.ReadAllBytesAsync(sourcePath);

        await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(sourcePath));
    }

    /// <summary>A document whose root element is not a recognized xLights rgbeffects document fails safely instead of silently returning an empty graph.</summary>
    [Fact]
    public async Task WrongRootElement_FailsSafely_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-wrong-root");

        await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));
    }

    /// <summary>A model element missing its required "name" attribute fails safely rather than producing a DisplayModel with a null/empty identity.</summary>
    [Fact]
    public async Task ModelMissingRequiredNameAttribute_FailsSafely_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-model-missing-name");

        await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));
    }

    /// <summary>
    /// XXE protection (PHASE-02 Section 2: "limits on DTD/entity expansion"): a document declaring a
    /// DOCTYPE with an external entity is rejected outright (DTDs are prohibited entirely) rather
    /// than the external entity ever being resolved/expanded.
    /// </summary>
    [Fact]
    public async Task XxeAttempt_IsRejected_NeverResolvesExternalEntity()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-xxe-attempt");

        var ex = await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));

        Assert.DoesNotContain("root:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>PHASE-02 Section 2: "limits on... large documents" — a document past the configured size bound is rejected rather than fully parsed.</summary>
    [Fact]
    public async Task OversizedDocument_IsRejected_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-oversized");

        await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));
    }
}
