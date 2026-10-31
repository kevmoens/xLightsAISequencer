using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 6 task 1/4: "validates LookId exists in the supplied LookLibrary, every PropBindings entry's Z exists on that Look and its override resolves via PropGroupResolver against the real layout." Uses a small synthetic layout (no real xLights install needed -- this validator never touches effect parameters/vocabulary, only prop/group references).</summary>
public sealed class SectionScriptValidatorTests
{
    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models: [new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900)],
        Groups: [],
        Warnings: []);

    private static Look GoldenHold() => Phase4LookLibraryFixture.Build().Find("golden-hold")!;

    [Fact]
    public void Validate_UnknownLookId_ReportsLookRefViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "no-such-look");

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "look-ref");
    }

    [Fact]
    public void Validate_ValidLookNoBindings_NoViolations()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold");

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_PropBindingUnknownLayerZ_ReportsPropBindingLayerViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 99, Props: "Tree")]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "prop-binding-layer");
    }

    [Fact]
    public void Validate_PropBindingReferencesRealModel_NoViolations()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "Tree")]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_PropBindingReferencesUnknownProp_ReportsPropBindingPropsViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "group:NoSuchGroup")]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "prop-binding-props");
    }

    [Fact]
    public void Validate_UnknownVariation_ReportsVariationRefViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", Variation: "not-a-real-variation");

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "variation-ref");
    }

    [Fact]
    public void Validate_PhraseVariationValid_NoViolations()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 2, ActiveLayerZs: [1, 2, 3]),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_TooManyPhraseVariations_ReportsCountViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(1), new SectionPhraseVariation(2), new SectionPhraseVariation(3), new SectionPhraseVariation(4),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-count");
    }

    [Fact]
    public void Validate_PhraseVariationIndexBelowOne_ReportsIndexViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 0)]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-index");
    }

    [Fact]
    public void Validate_DuplicatePhraseIndex_ReportsDuplicateViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1), new SectionPhraseVariation(PhraseIndex: 1),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-duplicate");
    }

    [Fact]
    public void Validate_PhraseVariationEmptyActiveLayerZs_ReportsLayersViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1, ActiveLayerZs: [])]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-layers");
    }

    [Fact]
    public void Validate_PhraseVariationUnknownActiveLayerZ_ReportsLayersViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1, ActiveLayerZs: [99])]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-layers");
    }

    [Fact]
    public void Validate_PhraseVariationUnknownPropBindingLayer_ReportsPropBindingLayerViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1, PropBindings: [new SectionEventBinding(LayerZ: 99, Props: "Tree")]),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-prop-binding-layer");
    }

    [Fact]
    public void Validate_PhraseVariationPropBindingUnresolvedProps_ReportsPropBindingPropsViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1, PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "group:NoSuchGroup")]),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-prop-binding-props");
    }
}
