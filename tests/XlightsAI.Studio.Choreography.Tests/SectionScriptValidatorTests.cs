using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 6 task 1/4: "validates LookId exists in the supplied LookLibrary, every PropBindings entry's Z exists on that Look and its override resolves via PropGroupResolver against the real layout." Uses a small synthetic layout (no real xLights install needed -- this validator never touches effect parameters/vocabulary, only prop/group references) and a minimal synthetic <see cref="EffectVocabularyCatalog"/> (build-plan Phase 15 -- real effect names, never a real xLights install, since this validator's filler-effects check only needs effect names to exist, not their full property schema).</summary>
public sealed class SectionScriptValidatorTests
{
    /// <summary>Six distinct, always-"real" (in this synthetic vocabulary) effect names -- enough to satisfy <see cref="FillerPool.MinimumDistinctStates"/> in every "no violations expected" test below.</summary>
    private static readonly string[] ValidFillerEffectNames = ["On", "Bars", "SingleStrand", "Color Wash", "Twinkle", "Butterfly"];

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models: [new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900)],
        Groups: [],
        Warnings: []);

    private static Look GoldenHold() => Phase4LookLibraryFixture.Build().Find("golden-hold")!;

    private static EffectVocabularyCatalog BuildVocabulary() => new(
        ValidFillerEffectNames.ToDictionary(name => name, name => new EffectVocabulary(name, "E_", []), StringComparer.Ordinal),
        buffer: new EffectVocabulary("Buffer", "B_", []),
        blending: new EffectVocabulary("Blending", "T_", []),
        color: new EffectVocabulary("Color", "C_", []));

    [Fact]
    public void Validate_UnknownLookId_ReportsLookRefViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "no-such-look", FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "look-ref");
    }

    [Fact]
    public void Validate_ValidLookNoBindings_NoViolations()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_MissingFillerEffects_ReportsFillerEffectsMissingViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold");

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "filler-effects-missing");
    }

    [Fact]
    public void Validate_FewerThanSixDistinctFillerEffects_ReportsDistinctCountViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", FillerEffects: ["On", "Bars", "Twinkle"]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "filler-effects-distinct-count");
    }

    [Fact]
    public void Validate_FillerEffectsNameNotInVocabulary_ReportsUnknownEffectViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", FillerEffects: [.. ValidFillerEffectNames[..5], "NotARealEffect"]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "filler-effects-unknown-effect");
    }

    [Fact]
    public void Validate_FillerEffectsByRoleValid_NoViolations()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", FillerEffects: ValidFillerEffectNames,
            FillerEffectsByRole: new Dictionary<string, IReadOnlyList<string>> { ["Accent"] = ValidFillerEffectNames });

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_FillerEffectsByRoleUnknownRoleKey_ReportsUnknownRoleViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", FillerEffects: ValidFillerEffectNames,
            FillerEffectsByRole: new Dictionary<string, IReadOnlyList<string>> { ["NotARole"] = ValidFillerEffectNames });

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "filler-effects-by-role-unknown-role");
    }

    [Fact]
    public void Validate_FillerEffectsByRoleFewerThanSixDistinct_ReportsByRoleDistinctCountViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", FillerEffects: ValidFillerEffectNames,
            FillerEffectsByRole: new Dictionary<string, IReadOnlyList<string>> { ["Foundation"] = ["On", "Bars"] });

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "filler-effects-by-role-distinct-count");
    }

    [Fact]
    public void Validate_FillerEffectsByRoleUnknownEffect_ReportsByRoleUnknownEffectViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", FillerEffects: ValidFillerEffectNames,
            FillerEffectsByRole: new Dictionary<string, IReadOnlyList<string>> { ["Subject"] = [.. ValidFillerEffectNames[..5], "NotARealEffect"] });

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "filler-effects-by-role-unknown-effect");
    }

    [Fact]
    public void Validate_PropBindingUnknownLayerZ_ReportsPropBindingLayerViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 99, Props: "Tree")], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "prop-binding-layer");
    }

    [Fact]
    public void Validate_PropBindingReferencesRealModel_NoViolations()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "Tree")], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_PropBindingReferencesUnknownProp_ReportsPropBindingPropsViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "group:NoSuchGroup")], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "prop-binding-props");
    }

    [Fact]
    public void Validate_UnknownVariation_ReportsVariationRefViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", Variation: "not-a-real-variation", FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "variation-ref");
    }

    [Fact]
    public void Validate_PhraseVariationValid_NoViolations()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 2, ActiveLayerZs: [1, 2, 3]),
        ], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Empty(violations);
    }

    /// <summary>
    /// User-reported real bug (2026-11-08): a real generation crashed with an unrecoverable
    /// <see cref="ChoreographyCompilationException"/> when the Choreographer declared PhraseVariations for
    /// a section with no real phrase segmentation at all -- a defect this validator previously had no way
    /// to catch (deferred to <see cref="SectionScriptCompiler"/>, which only ever throws, never retries).
    /// Confirms the new <paramref name="phraseCountBySectionId"/> cross-check turns that into an ordinary,
    /// retry-worthy violation instead.
    /// </summary>
    [Fact]
    public void Validate_PhraseVariationsButNoPhraseSegmentationForSection_ReportsNoPhrasesViolation()
    {
        var sectionId = Guid.NewGuid();
        var script = new SectionScript(sectionId, "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1),
        ], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(
            script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary(),
            phraseCountBySectionId: new Dictionary<Guid, int>());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-no-phrases");
    }

    [Fact]
    public void Validate_PhraseVariationIndexBeyondRealPhraseCount_ReportsIndexRangeViolation()
    {
        var sectionId = Guid.NewGuid();
        var script = new SectionScript(sectionId, "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 5),
        ], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(
            script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary(),
            phraseCountBySectionId: new Dictionary<Guid, int> { [sectionId] = 2 });

        Assert.Contains(violations, v => v.Rule == "phrase-variation-index-range");
    }

    [Fact]
    public void Validate_PhraseVariationIndexWithinRealPhraseCount_NoViolations()
    {
        var sectionId = Guid.NewGuid();
        var script = new SectionScript(sectionId, "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 2, ActiveLayerZs: [1, 2, 3]),
        ], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(
            script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary(),
            phraseCountBySectionId: new Dictionary<Guid, int> { [sectionId] = 3 });

        Assert.Empty(violations);
    }

    /// <summary>The new cross-check is opt-in via a nullable parameter -- a caller that never passes <c>phraseCountBySectionId</c> (every pre-existing caller/test) keeps behaving exactly as before, even for a section with no real phrase data.</summary>
    [Fact]
    public void Validate_PhraseVariationsWithNoPhraseCountSupplied_SkipsNewCheck()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1),
        ], FillerEffects: ValidFillerEffectNames);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.DoesNotContain(violations, v => v.Rule is "phrase-variation-no-phrases" or "phrase-variation-index-range");
    }

    [Fact]
    public void Validate_TooManyPhraseVariations_ReportsCountViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(1), new SectionPhraseVariation(2), new SectionPhraseVariation(3), new SectionPhraseVariation(4),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-count");
    }

    [Fact]
    public void Validate_NullPhraseVariationEntry_ReportsNullViolation_NeverThrows()
    {
        // Real-run finding (2026-11-10, Phase 24 real-run test): a model's JSON response can include a
        // literal `null` array element inside "PhraseVariations" -- this previously crashed with a raw
        // NullReferenceException instead of a real, retryable validation error.
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations: [null!]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-null");
    }

    [Fact]
    public void Validate_PhraseVariationIndexBelowOne_ReportsIndexViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 0)]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-index");
    }

    [Fact]
    public void Validate_DuplicatePhraseIndex_ReportsDuplicateViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1), new SectionPhraseVariation(PhraseIndex: 1),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-duplicate");
    }

    [Fact]
    public void Validate_PhraseVariationEmptyActiveLayerZs_ReportsLayersViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1, ActiveLayerZs: [])]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-layers");
    }

    [Fact]
    public void Validate_PhraseVariationUnknownActiveLayerZ_ReportsLayersViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1, ActiveLayerZs: [99])]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-layers");
    }

    [Fact]
    public void Validate_PhraseVariationUnknownPropBindingLayer_ReportsPropBindingLayerViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1, PropBindings: [new SectionEventBinding(LayerZ: 99, Props: "Tree")]),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-prop-binding-layer");
    }

    [Fact]
    public void Validate_PhraseVariationPropBindingUnresolvedProps_ReportsPropBindingPropsViolation()
    {
        var script = new SectionScript(Guid.NewGuid(), "golden-hold", PhraseVariations:
        [
            new SectionPhraseVariation(PhraseIndex: 1, PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "group:NoSuchGroup")]),
        ]);

        var violations = SectionScriptValidator.Validate(script, new LookLibrary([GoldenHold()]), BuildLayout(), BuildVocabulary());

        Assert.Contains(violations, v => v.Rule == "phrase-variation-prop-binding-props");
    }
}
