using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 5 acceptance criterion: "The real layout produces a reviewable 8-20 Look Show Bible
/// with a 3-5 palette set." Validates against the REAL <c>C:\xLights</c> layout and the real generated
/// vocabulary -- skips cleanly when not present on this machine.
/// </summary>
public sealed class ShowBibleValidatorTests
{
    [Fact]
    public async Task Validate_StructurallyValidContent_AgainstRealLayout_HasNoViolations()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid(lookCount: 12);

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes);

        Assert.Empty(violations);
        Assert.InRange(content.Looks.Looks.Count, ShowBibleValidator.MinLooks, ShowBibleValidator.MaxLooks);
        Assert.InRange(content.Palettes.Count, ShowBibleValidator.MinPalettes, ShowBibleValidator.MaxPalettes);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(25)]
    public async Task Validate_LookCountOutsideRange_FlagsLookCountViolation(int lookCount)
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid(lookCount);

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes);

        Assert.Contains(violations, v => v.Rule == "look-count");
    }

    [Fact]
    public async Task Validate_LookPaletteRefNotDeclared_FlagsViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var brokenLook = content.Looks.Looks[0] with { PaletteRef = "does-not-exist" };
        var broken = content with { Looks = new Choreography.LookLibrary([brokenLook, .. content.Looks.Looks.Skip(1)]) };

        var violations = ShowBibleValidator.Validate(broken, layout, vocabulary, recipes);

        Assert.Contains(violations, v => v.Rule == "look-palette-ref");
    }

    [Fact]
    public async Task Validate_LookSubjectDoesNotResolve_FlagsViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var brokenLook = content.Looks.Looks[0] with { Subject = "group:DoesNotExist" };
        var broken = content with { Looks = new Choreography.LookLibrary([brokenLook, .. content.Looks.Looks.Skip(1)]) };

        var violations = ShowBibleValidator.Validate(broken, layout, vocabulary, recipes);

        Assert.Contains(violations, v => v.Rule == "look-subject");
    }

    [Fact]
    public async Task Validate_LookHasTwoLayersAtTheSameZ_FlagsDuplicateZViolation()
    {
        // Real-run finding (2026-10-31): previously nothing caught this, so a Look with two layers both
        // at the same Z (most commonly the default z=0) passed validation and crashed much later in
        // SectionScriptCompiler's own Layers.ToDictionary(l => l.Z) with a raw framework exception.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var brokenLook = content.Looks.Looks[0];
        var duplicatedLayer = brokenLook.Layers[0] with { Z = brokenLook.Layers[0].Z };
        var broken = content with
        {
            Looks = new Choreography.LookLibrary(
                [brokenLook with { Layers = [.. brokenLook.Layers, duplicatedLayer] }, .. content.Looks.Looks.Skip(1)]),
        };

        var violations = ShowBibleValidator.Validate(broken, layout, vocabulary, recipes);

        Assert.Contains(violations, v => v.Rule == "layer-duplicate-z");
    }

    [Fact]
    public async Task Validate_LookTemplateRefNotInSuppliedCatalog_FlagsViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var brokenLook = content.Looks.Looks[0] with { TemplateRef = "does-not-exist" };
        var broken = content with { Looks = new Choreography.LookLibrary([brokenLook, .. content.Looks.Looks.Skip(1)]) };

        var violations = ShowBibleValidator.Validate(broken, layout, vocabulary, recipes, Knowledge.MultiLayerTemplateCatalogLoader.Load());

        Assert.Contains(violations, v => v.Rule == "look-template-ref");
    }

    [Fact]
    public async Task Validate_RealLayoutHasTreeOrMatrixProp_ButNoLookDeclaresATemplateRef_FlagsRequiredViolation()
    {
        // Build-plan Phase 10 §6b acceptance criterion enforcement: a Director told about a non-empty
        // template catalog, generating against a real layout that has a Tree/Matrix-family prop, must
        // actually declare at least one TemplateRef somewhere -- never a silent no-op.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid(); // no Look declares a TemplateRef

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes, Knowledge.MultiLayerTemplateCatalogLoader.Load());

        Assert.Contains(violations, v => v.Rule == "look-template-ref-required");
    }

    [Fact]
    public async Task Validate_WithNoTemplateCatalogSupplied_NeverFlagsTheRequiredRule()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes); // templates: null

        Assert.DoesNotContain(violations, v => v.Rule == "look-template-ref-required");
    }

    [Fact]
    public async Task Validate_LookTemplateRefInSuppliedCatalog_HasNoViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var templatedLook = content.Looks.Looks[0] with { TemplateRef = "edm-dense-canvas-tree-matrix" };
        var withTemplate = content with { Looks = new Choreography.LookLibrary([templatedLook, .. content.Looks.Looks.Skip(1)]) };

        var violations = ShowBibleValidator.Validate(withTemplate, layout, vocabulary, recipes, Knowledge.MultiLayerTemplateCatalogLoader.Load());

        Assert.DoesNotContain(violations, v => v.Rule == "look-template-ref");
    }

    [Fact]
    public async Task Validate_PaletteCountOutsideRange_FlagsViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var onePalette = content with { Palettes = [content.Palettes[0]] };

        var violations = ShowBibleValidator.Validate(onePalette, layout, vocabulary, recipes);

        Assert.Contains(violations, v => v.Rule == "palette-count");
    }
}
