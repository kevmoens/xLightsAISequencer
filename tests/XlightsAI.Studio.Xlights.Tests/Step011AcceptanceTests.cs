namespace XlightsAI.Studio.Xlights.Tests;

/// <summary>
/// PHASE-02 Step 011 — "Validate referenced models, channel/network data where relevant and missing
/// layout elements." Required acceptance: "Fixture with broken references generates warnings rather
/// than partial destructive output." Failure/edge case: "Invalid model/format/version must fail
/// safely without rewriting source files." These tests exercise the real <see cref="ShowLayoutImporter"/>
/// against real fixture directories on disk (not a mocked file system), and assert the persisted
/// domain state (<see cref="DisplayGraph"/>) it returns, not merely that a call happened.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step011AcceptanceTests
{
    private static string FixtureDirectory(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>
    /// Golden fixture: one modelGroup ("Front-Yard") with a broken reference ("Ghost-Prop", no
    /// matching model), another modelGroup ("All-Props") that nests "Front-Yard" by name and also
    /// references a submodel shape ("Arch-01/Segment-1") whose model half does resolve. Proves the
    /// import still succeeds (non-destructive), every member is classified correctly, nested groups
    /// are never flattened into a resolved model list, and the broken reference surfaces as an
    /// explicit warning.
    /// </summary>
    [Fact]
    public async Task Step011_Acceptance_Passes_BrokenReferencesProduceWarningsNotDestructiveFailure()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-broken-references");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        // Non-destructive: the import succeeded and still returned a complete graph, not a partial one.
        Assert.Equal(2, graph.Models.Count);
        Assert.Equal(2, graph.Groups.Count);

        var frontYard = Assert.Single(graph.Groups, g => g.Name == "Front-Yard");
        Assert.Equal(3, frontYard.Members.Count);
        Assert.Contains(frontYard.Members, m => m.RawName == "Arch-01" && m.Kind == DisplayGroupMemberKind.Model);
        Assert.Contains(frontYard.Members, m => m.RawName == "MegaTree-Center" && m.Kind == DisplayGroupMemberKind.Model);
        Assert.Contains(frontYard.Members, m => m.RawName == "Ghost-Prop" && m.Kind == DisplayGroupMemberKind.Unresolved);

        var allProps = Assert.Single(graph.Groups, g => g.Name == "All-Props");
        Assert.Equal(2, allProps.Members.Count);
        // Nested group reference: "Front-Yard" is classified as Group, never expanded into its own members here.
        Assert.Contains(allProps.Members, m => m.RawName == "Front-Yard" && m.Kind == DisplayGroupMemberKind.Group);
        Assert.Contains(allProps.Members, m => m.RawName == "Arch-01/Segment-1" && m.Kind == DisplayGroupMemberKind.Submodel);

        // The broken reference is reported as an explicit warning, by group and raw name.
        Assert.Contains(graph.Warnings, w => w.Contains("Front-Yard") && w.Contains("Ghost-Prop"));

        // Direct model->group membership (Step 010's own contract) is unaffected by this step's validation.
        var arch = Assert.Single(graph.Models, m => m.XlightsName == "Arch-01");
        Assert.Equal(["Front-Yard"], arch.GroupNames);
    }

    /// <summary>A document with neither a &lt;models&gt; nor a &lt;modelGroups&gt; element still imports successfully (empty, not guessed), with explicit warnings for each missing element.</summary>
    [Fact]
    public async Task MissingLayoutElements_ProduceWarnings_NotFailure()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-no-layout-elements");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        Assert.Empty(graph.Models);
        Assert.Empty(graph.Groups);
        Assert.Contains(graph.Warnings, w => w.Contains("<models>"));
        Assert.Contains(graph.Warnings, w => w.Contains("<modelGroups>"));
    }

    /// <summary>The golden Step 010 fixture (no broken references) still produces zero warnings under this step's new validation.</summary>
    [Fact]
    public async Task WellFormedReferences_ProduceNoWarnings()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-with-geometry");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        Assert.Empty(graph.Warnings);
        Assert.Equal(2, graph.Groups.Count);
        Assert.All(graph.Groups, g => Assert.All(g.Members, m => Assert.Equal(DisplayGroupMemberKind.Model, m.Kind)));
    }

    /// <summary>
    /// Failure/edge case required by this step ("Invalid model/format/version must fail safely
    /// without rewriting source files"): a modelGroup element missing its required "name" attribute
    /// still fails safely (negative path) rather than silently producing a group with no identity —
    /// the source file is never rewritten.
    /// </summary>
    [Fact]
    public async Task ModelGroupMissingRequiredNameAttribute_FailsSafely_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-modelgroup-missing-name");
        var sourcePath = Path.Combine(directory, ShowDirectoryFileNames.RgbEffects);
        var originalBytes = await File.ReadAllBytesAsync(sourcePath);

        await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(sourcePath));
    }

    /// <summary>
    /// PHASE-02 Step 011 migration (accepted; see implementation-evidence/PHASE-02/STEP-11.md §9a/§9b):
    /// real xLights itself writes/validates this file's root element as "xrgb" (confirmed against the
    /// real xLights source, <c>XmlNodeKeys.h</c> and <c>TabSequence.cpp</c>'s own
    /// <c>root.name() != "xrgb"</c> check) — not "xrgbEffects", this importer's original, unverified
    /// assumption. A real "xrgb"-rooted document must import successfully.
    /// </summary>
    [Fact]
    public async Task RealRootElementName_Xrgb_IsAccepted()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-with-geometry");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        Assert.Equal(2, graph.Models.Count);
    }

    /// <summary>
    /// PHASE-02 Step 011 migration: real xLights also falls back to a legacy "xlights" root
    /// (confirmed against <c>iPadRenderContext.cpp</c>'s "xrgb" then "xlights" fallback chain) — this
    /// must also import successfully, not be rejected as an unrecognized document.
    /// </summary>
    [Fact]
    public async Task LegacyRootElementName_Xlights_IsAccepted()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-legacy-xlights-root");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        var arch = Assert.Single(graph.Models, m => m.XlightsName == "Arch-01");
        Assert.Equal(["Front-Yard"], arch.GroupNames);
    }

    /// <summary>
    /// A root element that is neither the real current ("xrgb") nor real legacy ("xlights") name is
    /// still rejected as an unrecognized document — the migration only widens acceptance to the two
    /// real names confirmed against the xLights source, not to anything at all.
    /// </summary>
    [Fact]
    public async Task UnrecognizedRootElementName_StillFailsSafely_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-wrong-root");

        await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));
    }

    /// <summary>
    /// PHASE-02 Step 011 migration: a real user-owned xLights show file (github.com/kevmoens/xlights,
    /// commit 19a7975b000dd9160a0630095ac68dfe364f6329) was found to carry a stray
    /// <c>&lt;!DOCTYPE html&gt;</c> declaration before its real "xrgb" root — confirmed absent from
    /// real xLights's own writer source, almost certainly an editor/tool artifact in that user's repo,
    /// but real-world data a robust importer must still tolerate. This fixture reproduces that exact
    /// structural shape (synthetic model content, not the user's real show data) and must still import
    /// successfully — the DOCTYPE is stripped before parsing, never by relaxing DTD processing itself.
    /// </summary>
    [Fact]
    public async Task BenignStrayDoctype_IsToleratedAndStripped_NotRejected()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-stray-doctype");

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        var arch = Assert.Single(graph.Models, m => m.XlightsName == "Arch-01");
        Assert.Equal(["Front-Yard"], arch.GroupNames);
    }

    /// <summary>
    /// The benign-DOCTYPE tolerance above must never widen into accepting an actual XXE vector: a
    /// DOCTYPE with a SYSTEM external reference (and no internal subset/brackets at all — proving this
    /// is detected independently of the existing bracket-based check) is still rejected outright,
    /// never evaluated, and the source file is never rewritten.
    /// </summary>
    [Fact]
    public async Task DoctypeWithExternalSystemReference_StillRejected_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-doctype-external-reference");
        var sourcePath = Path.Combine(directory, ShowDirectoryFileNames.RgbEffects);
        var originalBytes = await File.ReadAllBytesAsync(sourcePath);

        await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(sourcePath));
    }

    /// <summary>The pre-existing Step 010 XXE fixture (DOCTYPE with an internal subset declaring an external entity) must remain rejected after this migration — the bracket-based internal-subset check, not just the SYSTEM/PUBLIC keyword check, independently blocks it.</summary>
    [Fact]
    public async Task PreExistingXxeFixture_StillRejected_AfterMigration_NegativePath()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-xxe-attempt");

        var ex = await Assert.ThrowsAsync<ShowLayoutMalformedException>(
            () => importer.ImportReadOnlyAsync(directory, CancellationToken.None));

        Assert.DoesNotContain("root:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The source layout hash is computed from the real, original (un-stripped) file bytes even when a benign DOCTYPE is present and stripped for parsing — the hash still identifies the actual on-disk document, not an internal parsing adjustment.</summary>
    [Fact]
    public async Task SourceLayoutHash_ReflectsOriginalBytes_EvenWhenDoctypeIsStripped()
    {
        var importer = new ShowLayoutImporter();
        var directory = FixtureDirectory("sample-show-stray-doctype");
        var sourcePath = Path.Combine(directory, ShowDirectoryFileNames.RgbEffects);
        var originalBytes = await File.ReadAllBytesAsync(sourcePath);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(originalBytes));

        var graph = await importer.ImportReadOnlyAsync(directory, CancellationToken.None);

        Assert.Equal(expectedHash, graph.SourceLayoutHash);
    }
}
