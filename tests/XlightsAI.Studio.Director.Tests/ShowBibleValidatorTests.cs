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
    public async Task Validate_LookTravelWithInvalidOrder_FlagsViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var brokenLook = content.Looks.Looks[0] with { Travel = new Choreography.LookTravel("Sideways", "Beats") };
        var broken = content with { Looks = new Choreography.LookLibrary([brokenLook, .. content.Looks.Looks.Skip(1)]) };

        var violations = ShowBibleValidator.Validate(broken, layout, vocabulary, recipes);

        Assert.Contains(violations, v => v.Rule == "look-travel-order");
    }

    [Fact]
    public async Task Validate_LookTravelWithBlankTimescale_FlagsViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var brokenLook = content.Looks.Looks[0] with { Travel = new Choreography.LookTravel(Choreography.LookTravel.OrderLeftToRight, "") };
        var broken = content with { Looks = new Choreography.LookLibrary([brokenLook, .. content.Looks.Looks.Skip(1)]) };

        var violations = ShowBibleValidator.Validate(broken, layout, vocabulary, recipes);

        Assert.Contains(violations, v => v.Rule == "look-travel-timescale");
    }

    [Fact]
    public async Task Validate_LookTravelWithValidOrderAndTimescale_HasNoTravelViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var validLook = content.Looks.Looks[0] with { Travel = new Choreography.LookTravel(Choreography.LookTravel.OrderRightToLeft, "Beats") };
        var updated = content with { Looks = new Choreography.LookLibrary([validLook, .. content.Looks.Looks.Skip(1)]) };

        var violations = ShowBibleValidator.Validate(updated, layout, vocabulary, recipes);

        Assert.DoesNotContain(violations, v => v.Rule == "look-travel-order");
        Assert.DoesNotContain(violations, v => v.Rule == "look-travel-timescale");
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
    public async Task Validate_RepeatingKindWithTooFewEligibleLooks_FlagsLookDiversityViolation()
    {
        // Build-plan Phase 24 §2a: a Kind with 5 real occurrences needs at least ceil(5/2)=3 distinct
        // eligible Looks; restricting every Look's AllowedSectionKinds away from Chorus except one leaves
        // only 1 eligible, which must be a hard validation failure.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var restrictedLooks = content.Looks.Looks
            .Select((look, i) => i == 0 ? look with { AllowedSectionKinds = [SectionKind.Chorus] } : look with { AllowedSectionKinds = [SectionKind.Verse] })
            .ToList();
        var restricted = content with { Looks = new Choreography.LookLibrary(restrictedLooks) };
        var sectionKindCounts = new Dictionary<SectionKind, int> { [SectionKind.Chorus] = 5 };

        var violations = ShowBibleValidator.Validate(restricted, layout, vocabulary, recipes, sectionKindOccurrenceCounts: sectionKindCounts);

        Assert.Contains(violations, v => v.Rule == "look-diversity-per-kind");
    }

    [Fact]
    public async Task Validate_RepeatingKindWithEnoughEligibleLooks_HasNoDiversityViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid(); // every Look left unrestricted (null AllowedSectionKinds)
        var sectionKindCounts = new Dictionary<SectionKind, int> { [SectionKind.Chorus] = 5 };

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes, sectionKindOccurrenceCounts: sectionKindCounts);

        Assert.DoesNotContain(violations, v => v.Rule == "look-diversity-per-kind");
    }

    [Fact]
    public async Task Validate_KindWithFewerThanThreeOccurrences_IsExemptFromDiversityFloor()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var restrictedLooks = content.Looks.Looks
            .Select(look => look with { AllowedSectionKinds = [SectionKind.Verse] })
            .ToList();
        var restricted = content with { Looks = new Choreography.LookLibrary(restrictedLooks) };
        var sectionKindCounts = new Dictionary<SectionKind, int> { [SectionKind.Bridge] = 2 }; // N=2, exempt

        var violations = ShowBibleValidator.Validate(restricted, layout, vocabulary, recipes, sectionKindOccurrenceCounts: sectionKindCounts);

        Assert.DoesNotContain(violations, v => v.Rule == "look-diversity-per-kind");
    }

    [Fact]
    public async Task Validate_WithNoSectionKindCountsSupplied_NeverFlagsTheDiversityRule()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var restrictedLooks = content.Looks.Looks
            .Select(look => look with { AllowedSectionKinds = [SectionKind.Verse] })
            .ToList();
        var restricted = content with { Looks = new Choreography.LookLibrary(restrictedLooks) };

        var violations = ShowBibleValidator.Validate(restricted, layout, vocabulary, recipes); // sectionKindOccurrenceCounts: null

        Assert.DoesNotContain(violations, v => v.Rule == "look-diversity-per-kind");
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

    [Fact]
    public async Task Validate_ExemplarDrawnBeyondMaxReuseCount_FlagsExemplarReuseMaxViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var overReused = new Pipeline.ExemplarReuseReport(
            DrawCountByExemplarKey: new Dictionary<string, int> { ["Song A::0-8"] = ShowBibleValidator.MaxExemplarReuseCount + 1 },
            GroupStats: new Dictionary<string, Pipeline.ExemplarGroupReuseStats>());

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes, exemplarReuse: overReused);

        Assert.Contains(violations, v => v.Rule == "exemplar-reuse-max");
    }

    [Fact]
    public async Task Validate_ExemplarDrawnWithinMaxReuseCount_NoExemplarReuseMaxViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();
        var withinBudget = new Pipeline.ExemplarReuseReport(
            DrawCountByExemplarKey: new Dictionary<string, int> { ["Song A::0-8"] = ShowBibleValidator.MaxExemplarReuseCount },
            GroupStats: new Dictionary<string, Pipeline.ExemplarGroupReuseStats>());

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes, exemplarReuse: withinBudget);

        Assert.DoesNotContain(violations, v => v.Rule == "exemplar-reuse-max");
    }

    [Fact]
    public async Task Validate_RepeatablePieceGroupUnderDiversified_FlagsExemplarReuseDiversityViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();

        // 4 total draws from this group (>= MinOccurrencesForDiversityFloor) but only 1 distinct exemplar
        // drawn -- needs ceil(4/2)=2 distinct, so this must fail.
        var underDiversified = new Pipeline.ExemplarReuseReport(
            DrawCountByExemplarKey: new Dictionary<string, int> { ["Song A::0-8"] = 4 },
            GroupStats: new Dictionary<string, Pipeline.ExemplarGroupReuseStats> { ["8bar|Chorus|Mid"] = new(TotalDraws: 4, DistinctKeysDrawn: 1) });

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes, exemplarReuse: underDiversified);

        Assert.Contains(violations, v => v.Rule == "exemplar-reuse-diversity");
    }

    [Fact]
    public async Task Validate_RepeatablePieceGroupBelowDiversityFloor_NeverFlagged()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();

        // Only 2 total draws (< MinOccurrencesForDiversityFloor) -- nothing real to diversify yet.
        var belowFloor = new Pipeline.ExemplarReuseReport(
            DrawCountByExemplarKey: new Dictionary<string, int> { ["Song A::0-8"] = 2 },
            GroupStats: new Dictionary<string, Pipeline.ExemplarGroupReuseStats> { ["8bar|Chorus|Mid"] = new(TotalDraws: 2, DistinctKeysDrawn: 1) });

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes, exemplarReuse: belowFloor);

        Assert.DoesNotContain(violations, v => v.Rule == "exemplar-reuse-diversity");
    }

    [Fact]
    public async Task Validate_WithNoExemplarReuseReportSupplied_NeverFlagsExemplarReuseRules()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = await new XlightsAI.Studio.Xlights.ShowLayoutImporter().ImportReadOnlyAsync(RealXLightsFixture.ShowDirectory, CancellationToken.None);
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var content = ShowBibleContentFixture.BuildValid();

        var violations = ShowBibleValidator.Validate(content, layout, vocabulary, recipes); // exemplarReuse: null

        Assert.DoesNotContain(violations, v => v.Rule is "exemplar-reuse-max" or "exemplar-reuse-diversity");
    }
}
