using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 4 task 4 ("Compositional legality checks"), architecture §7.</summary>
public sealed class ChoreographyLegalityValidatorTests
{
    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("TinyStar"), "TinyStar", "Star", [], [], NodeCount: 20),
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
        ],
        Groups: [],
        Warnings: []);

    private static Look BuildLook(string id, string motion, IReadOnlyList<LookLayer> layers, bool declaredDarkness = false, string? uniqueness = null) =>
        new(id, id, "test", 0.5, motion, "p", "TinyStar", layers, DeclaredDarkness: declaredDarkness, Uniqueness: uniqueness);

    [Fact]
    public void ValidateSection_FireOnATinyProp_FlagsResolutionMismatch()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var layout = BuildLayout();
        var fireRecipeId = recipes.Recipes.First(r => r.XlightsEffectName == "Fire").RecipeId;

        var look = BuildLook("fire-look", "hold", [new LookLayer(1, "TinyStar", "Fire", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);
        var ir = new ChoreographyIR([], [new EffectInstance(Guid.NewGuid(), new ModelId("TinyStar"), 0, 0, 1000, fireRecipeId, "1", new Dictionary<string, string>())], []);

        var violations = ChoreographyLegalityValidator.ValidateSection(look, ir, layout, vocabulary);

        Assert.Contains(violations, v => v.Rule == "resolution-aware-effect-matching");
    }

    [Fact]
    public void ValidateSection_LinearRampCurve_FlagsEasingViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, _) = RealXLightsFixture.BuildCatalogs();
        var layout = BuildLayout();

        var look = BuildLook("ramp-look", "hold", [new LookLayer(1, "Tree", "Color Wash", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);
        var effect = new EffectInstance(
            Guid.NewGuid(), new ModelId("Tree"), 0, 0, 1000, "effect.generated.color wash.v1", "1", new Dictionary<string, string>(),
            ValueCurves: [new EffectValueCurve("C_SLIDER_Brightness", "Ramp", 0, 100)]);
        var ir = new ChoreographyIR([], [effect], []);

        var violations = ChoreographyLegalityValidator.ValidateSection(look, ir, layout, vocabulary);

        Assert.Contains(violations, v => v.Rule == "easing-on-every-ramp");
    }

    [Fact]
    public void ValidateSection_PopMotionWithTransitionIn_FlagsHardCutViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, _) = RealXLightsFixture.BuildCatalogs();
        var layout = BuildLayout();

        var look = BuildLook("pop-look", "pop",
            [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 1.0, TransitionIn: new LookTransition("Fade", NoteValue.QuarterBeat))]);
        var ir = new ChoreographyIR([], [], []);

        var violations = ChoreographyLegalityValidator.ValidateSection(look, ir, layout, vocabulary);

        Assert.Contains(violations, v => v.Rule == "hard-cut-for-pops-fade-for-builds");
    }

    [Fact]
    public void ValidateSection_BuildMotionWithNoTransitionIn_FlagsFadeViolation()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, _) = RealXLightsFixture.BuildCatalogs();
        var layout = BuildLayout();

        var look = BuildLook("build-look", "build", [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 1.0)]);
        var ir = new ChoreographyIR([], [], []);

        var violations = ChoreographyLegalityValidator.ValidateSection(look, ir, layout, vocabulary);

        Assert.Contains(violations, v => v.Rule == "hard-cut-for-pops-fade-for-builds");
    }

    [Fact]
    public void ValidateShow_SamePeakLookBoundToTwoSections_FlagsUniquenessViolation()
    {
        var layout = BuildLayout();
        var look = BuildLook("peak-look", "hold", [], uniqueness: "reserved:peak");
        var ir = new ChoreographyIR([], [], []);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Tree")]),
            new ChoreographyLegalityValidator.SectionAssignment("s2", look, ir, [new ModelId("Tree")]),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == "peak-reserved-look-binds-one-section");
    }

    [Fact]
    public void ValidateShow_SectionTargetingOnlySmallProps_FlagsMissingPixelHeavyProp()
    {
        var layout = BuildLayout();
        var look = BuildLook("small-only", "hold", []);
        var ir = new ChoreographyIR([], [], []);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("TinyStar")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == "never-schedule-without-a-pixel-heavy-prop");
    }

    [Fact]
    public void ValidateShow_SectionTargetingThePixelHeavyProp_DoesNotFlagMissingPixelHeavyProp()
    {
        var layout = BuildLayout();
        var look = BuildLook("has-tree", "hold", []);
        var ir = new ChoreographyIR([], [], []);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "never-schedule-without-a-pixel-heavy-prop");
    }

    [Fact]
    public void ValidateShow_ModelNeverTargetedByAnyEffect_FlagsModelCoverageViolationWithAffectedModelId()
    {
        // User feedback (2026-10-31): real generated shows left an Arch-category prop with zero effects
        // for the whole song -- nothing previously checked whole-show model coverage at all.
        var layout = BuildLayout();
        var look = BuildLook("has-tree", "hold", []);
        var ir = BuildIrWithEffectCount(1); // targets only "Tree" -- "TinyStar" is never targeted.
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        var violation = Assert.Single(violations, v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage);
        Assert.Equal("TinyStar", violation.AffectedModelId);
        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage && v.AffectedModelId == "Tree");
    }

    [Fact]
    public void ValidateShow_EveryModelTargetedSomewhere_DoesNotFlagModelCoverageViolation()
    {
        var layout = BuildLayout();
        var look = BuildLook("covers-both", "hold", []);
        var treeEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>());
        var tinyStarEffect = new EffectInstance(Guid.NewGuid(), new ModelId("TinyStar"), 0, 1000, 2000, "recipe", "1", new Dictionary<string, string>());
        var ir = new ChoreographyIR([], [treeEffect, tinyStarEffect], []);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Tree"), new ModelId("TinyStar")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage);
    }

    [Fact]
    public void ValidateShow_ModelCoveredViaAGroupEffect_DoesNotFlagModelCoverageViolation()
    {
        // A whole-show effect targeting a real group (LookCompiler's own group-collapse optimization)
        // must count as covering every real member model, not just a literal per-model target.
        var layout = new DisplayGraph(
            "TEST-HASH",
            Models:
            [
                new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arch", [], [], NodeCount: 50),
                new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            ],
            Groups: [new DisplayModelGroup("Arches", [new DisplayGroupMember("Arch 1", DisplayGroupMemberKind.Model)])],
            Warnings: []);
        var look = BuildLook("covers-group", "hold", []);
        var groupEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Arches"), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>());
        var treeEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, 1000, 2000, "recipe", "1", new Dictionary<string, string>());
        var ir = new ChoreographyIR([], [groupEffect, treeEffect], []);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arches"), new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage);
    }

    [Fact]
    public void ValidateShow_ModelWithZeroNodeCount_NeverFlaggedForModelCoverage()
    {
        var layout = new DisplayGraph(
            "TEST-HASH",
            Models:
            [
                new DisplayModel(new ModelId("Bookkeeping"), "Bookkeeping", "Other", [], [], NodeCount: 0),
                new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            ],
            Groups: [],
            Warnings: []);
        var look = BuildLook("has-tree", "hold", []);
        var ir = BuildIrWithEffectCount(1);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage && v.AffectedModelId == "Bookkeeping");
    }

    private static ChoreographyIR BuildIrWithEffectCount(int count)
    {
        var effects = Enumerable.Range(0, count)
            .Select(i => new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, i * 1000L, (i + 1) * 1000L, "recipe", "1", new Dictionary<string, string>()))
            .ToList();
        return new ChoreographyIR([], effects, []);
    }

    [Fact]
    public void ValidateShow_QuietestSectionNotMeasurablyQuieterThanLoudest_FlagsDensityContrastViolation()
    {
        // Build-plan Phase 12 §3c: two sections with a very different declared ceiling (0.1 vs 0.85) but
        // nearly the same measured change-rate (flat density, the exact pre-Phase-12 defect) should trip
        // the new soft finding.
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var quietLook = BuildLook("quiet-look", "hold", []);
        var loudLook = BuildLook("loud-look", "hold", []);

        var window = new TimeWindow(0, 10 * TicksPerSecond);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment("quiet", quietLook, BuildIrWithEffectCount(10), [new ModelId("Tree")], window, 0.10),
            new ChoreographyLegalityValidator.SectionAssignment("loud", loudLook, BuildIrWithEffectCount(12), [new ModelId("Tree")], window, 0.85),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == "declared-vs-measured-change-density");
    }

    [Fact]
    public void ValidateShow_QuietestSectionMeasurablyQuieterThanLoudest_DoesNotFlagDensityContrastViolation()
    {
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var quietLook = BuildLook("quiet-look", "hold", []);
        var loudLook = BuildLook("loud-look", "hold", []);

        var window = new TimeWindow(0, 10 * TicksPerSecond);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment("quiet", quietLook, BuildIrWithEffectCount(2), [new ModelId("Tree")], window, 0.10),
            new ChoreographyLegalityValidator.SectionAssignment("loud", loudLook, BuildIrWithEffectCount(20), [new ModelId("Tree")], window, 0.85),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "declared-vs-measured-change-density");
    }

    [Fact]
    public void ValidateShow_SectionsMissingEnergyCeilingOrWindow_SkipsDensityContrastCheck_RatherThanInventingValues()
    {
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("legacy-look", "hold", []);

        // Neither Window nor EnergyCeiling supplied -- the exact shape every pre-Phase-12 caller
        // (including every other test above) already constructs.
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectCount(1), [new ModelId("Tree")]),
            new ChoreographyLegalityValidator.SectionAssignment("s2", look, BuildIrWithEffectCount(100), [new ModelId("Tree")]),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "declared-vs-measured-change-density");
    }

    private const long TicksPerSecond = 10_000_000L;

    private static ChoreographyIR BuildIrWithEffectRecipes(IReadOnlyList<string> recipeIds)
    {
        var effects = recipeIds
            .Select((recipeId, i) => new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, i * 1000L, (i + 1) * 1000L, recipeId, "1", new Dictionary<string, string>()))
            .ToList();
        return new ChoreographyIR([], effects, []);
    }

    [Fact]
    public void ValidateShow_OneEffectDominatesWholeSong_FlagsEffectConcentrationViolation()
    {
        // Build-plan Phase 13 §2c: a real generated-recipe id is "effect.generated.<lowercase-effect-name>.v<version>"
        // (see ChoreographyLegalityValidator.ResolveXlightsEffectName's own doc comment) -- it recovers the
        // real, original-cased effect name by matching against the owning Look's own declared layer effect
        // names, so the Look must declare a layer using that effect for the name to resolve at all.
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("dominant-look", "hold",
            [new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.5),
             new LookLayer(2, "Tree", "Bars", LookLayer.RoleSecondary, null, "Normal", "Bars", 0.5)]);

        var recipeIds = Enumerable.Repeat("effect.generated.twinkle.v1", 9).Append("effect.generated.bars.v1").ToList();
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectRecipes(recipeIds), [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == "whole-song-effect-concentration" && v.Message.Contains("Twinkle", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateShow_EffectUsageEvenlySpread_DoesNotFlagEffectConcentrationViolation()
    {
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("spread-look", "hold",
            [new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.5),
             new LookLayer(2, "Tree", "Bars", LookLayer.RoleSecondary, null, "Normal", "Bars", 0.5),
             new LookLayer(3, "Tree", "Color Wash", LookLayer.RoleSecondary, null, "Normal", "Bars", 0.5),
             new LookLayer(4, "Tree", "On", LookLayer.RoleSecondary, null, "Normal", "Bars", 0.5)]);

        // 4 distinct effects, no single one exceeding MaxSingleEffectShare (0.35) of the 10 total.
        var recipeIds = new List<string>
        {
            "effect.generated.twinkle.v1", "effect.generated.twinkle.v1", "effect.generated.twinkle.v1",
            "effect.generated.bars.v1", "effect.generated.bars.v1", "effect.generated.bars.v1",
            "effect.generated.color wash.v1", "effect.generated.color wash.v1",
            "effect.generated.on.v1", "effect.generated.on.v1",
        };
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectRecipes(recipeIds), [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "whole-song-effect-concentration");
    }

    [Fact]
    public void ValidateShow_TooFewResolvedEffects_SkipsEffectConcentrationCheck()
    {
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("tiny-look", "hold", [new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.5)]);

        var recipeIds = Enumerable.Repeat("effect.generated.twinkle.v1", 3).ToList(); // below MinEffectsForConcentrationCheck
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectRecipes(recipeIds), [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "whole-song-effect-concentration");
    }
}
