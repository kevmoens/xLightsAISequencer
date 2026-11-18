using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
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
        // "Arches" has only one real member ("Arch 1") -- the group's own effect IS that member's own
        // direct moment, so there is nothing distinct for RuleModelOnlyGroupCovered to flag.
        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleModelOnlyGroupCovered);
    }

    [Fact]
    public void ValidateShow_MultiMemberGroupMemberCoveredOnlyViaTheGroup_FlagsModelOnlyGroupCovered()
    {
        // User feedback (2026-11-15), confirmed against a real generation ("Carol of the Bells"): a
        // 3-member group's single group-level effect correctly satisfies RuleModelCoverage for every
        // member (real pixels genuinely lit up for all three), but two of those members ("Matrix 2",
        // "Matrix 3") never received their own direct, individually-targeted effect anywhere else in the
        // whole show -- a viewer scrubbing their own row in xLights sees them completely empty.
        var layout = new DisplayGraph(
            "TEST-HASH",
            Models:
            [
                new DisplayModel(new ModelId("Matrix 1"), "Matrix 1", "Matrix", [], [], NodeCount: 1488),
                new DisplayModel(new ModelId("Matrix 2"), "Matrix 2", "Matrix", [], [], NodeCount: 900),
                new DisplayModel(new ModelId("Matrix 3"), "Matrix 3", "Matrix", [], [], NodeCount: 900),
            ],
            Groups:
            [
                new DisplayModelGroup("Matrixes",
                [
                    new DisplayGroupMember("Matrix 1", DisplayGroupMemberKind.Model),
                    new DisplayGroupMember("Matrix 2", DisplayGroupMemberKind.Model),
                    new DisplayGroupMember("Matrix 3", DisplayGroupMemberKind.Model),
                ]),
            ],
            Warnings: []);
        var look = BuildLook("covers-via-group-only", "hold", []);
        // Only ONE compiled effect touches this group all show long, and it targets the group itself --
        // never any individual member directly.
        var groupEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Matrixes"), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>());
        var ir = new ChoreographyIR([], [groupEffect], []);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Matrixes")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        // RuleModelCoverage stays silent -- every member genuinely received real pixels via the group.
        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleModelCoverage);

        var flagged = violations.Where(v => v.Rule == ChoreographyLegalityValidator.RuleModelOnlyGroupCovered)
            .Select(v => v.AffectedModelId)
            .ToHashSet();
        Assert.Equal(new HashSet<string?> { "Matrix 1", "Matrix 2", "Matrix 3" }, flagged);
    }

    [Fact]
    public void ValidateShow_HeroLookIdSuppliedButNeverSelectedByAnySection_FlagsHeroLookNeverUsed()
    {
        // User feedback (2026-11-15), confirmed against a real generation ("Carol of the Bells"): the
        // Show Bible's own declared peak/held-back Look ("chorus-final-surge", built around
        // "group:Everything") was never selected by any of the 13 independent per-section calls.
        var layout = BuildLayout();
        var usedLook = BuildLook("verse-glass", "hold", []);
        var ir = BuildIrWithEffectCount(1);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", usedLook, ir, [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage,
            heroLookIds: ["verse-glass", "chorus-final-surge"]);

        var violation = Assert.Single(violations, v => v.Rule == ChoreographyLegalityValidator.RuleHeroLookNeverUsed);
        Assert.Equal("chorus-final-surge", violation.AffectedModelId);
    }

    [Fact]
    public void ValidateShow_EveryHeroLookIdSelectedBySomeSection_DoesNotFlagHeroLookNeverUsed()
    {
        var layout = BuildLayout();
        var look = BuildLook("chorus-final-surge", "hold", []);
        var ir = BuildIrWithEffectCount(1);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage,
            heroLookIds: ["chorus-final-surge"]);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleHeroLookNeverUsed);
    }

    [Fact]
    public void ValidateShow_NoHeroLookIdsSupplied_NeverFlagsHeroLookNeverUsed()
    {
        // A caller that predates Show Bible hero-Look awareness (heroLookIds omitted, the default)
        // behaves exactly as before -- this rule is simply never attempted.
        var layout = BuildLayout();
        var look = BuildLook("has-tree", "hold", []);
        var ir = BuildIrWithEffectCount(1);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleHeroLookNeverUsed);
    }

    /// <summary>User feedback (2026-11-14): a real generated show never once used "Everything"/"Roof"/etc. as
    /// their own real group-level effect, even though every member happened to be covered some other way --
    /// RuleModelCoverage stayed silent the whole time. This rule targets the group's own identity directly.</summary>
    [Fact]
    public void ValidateShow_GroupUsedDirectlyAtLeastOnce_DoesNotFlagGroupNeverTargetedDirectly()
    {
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

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleGroupNeverTargetedDirectly);
    }

    [Fact]
    public void ValidateShow_GroupMembersCoveredOnlyIndividually_FlagsGroupNeverTargetedDirectly()
    {
        var layout = new DisplayGraph(
            "TEST-HASH",
            Models:
            [
                new DisplayModel(new ModelId("Arch 1"), "Arch 1", "Arch", [], [], NodeCount: 50),
                new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            ],
            Groups: [new DisplayModelGroup("Arches", [new DisplayGroupMember("Arch 1", DisplayGroupMemberKind.Model)])],
            Warnings: []);
        var look = BuildLook("covers-member-only", "hold", []);
        // "Arch 1" is covered directly, but the "Arches" group itself is never the target of any effect.
        var archEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Arch 1"), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>());
        var treeEffect = new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, 1000, 2000, "recipe", "1", new Dictionary<string, string>());
        var ir = new ChoreographyIR([], [archEffect, treeEffect], []);
        var coverage = PixelCoverageLadder.Build(layout, 7);

        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arch 1"), new ModelId("Tree")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        var violation = Assert.Single(violations, v => v.Rule == ChoreographyLegalityValidator.RuleGroupNeverTargetedDirectly);
        Assert.Equal("Arches", violation.AffectedModelId);
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

    private static DisplayGraph BuildArchesLayout(params (string Name, double X)[] arches) => new(
        "TEST-HASH",
        Models: arches.Select(a => new DisplayModel(new ModelId(a.Name), a.Name, "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(a.X, 0, 0))).ToList(),
        Groups: [new DisplayModelGroup("Arches", arches.Select(a => new DisplayGroupMember(a.Name, DisplayGroupMemberKind.Model)).ToList())],
        Warnings: []);

    /// <summary>User feedback (2026-11-14): the real "stayed on a single model" pattern -- one of a qualifying group's own 3+ positioned members gets a real per-member effect more than once, while the group's other members never get one at all.</summary>
    [Fact]
    public void ValidateShow_SameSingleGroupMemberRepeated_FlagsIndividualModelDiversityAdvisory()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300));
        var look = BuildLook("arch1-twice", "hold", []);
        var effects = new[]
        {
            new EffectInstance(Guid.NewGuid(), new ModelId("Arch1"), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>()),
            new EffectInstance(Guid.NewGuid(), new ModelId("Arch1"), 0, 1000, 2000, "recipe", "1", new Dictionary<string, string>()),
        };
        var ir = new ChoreographyIR([], effects, []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arch1")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        var violation = Assert.Single(violations, v => v.Rule == ChoreographyLegalityValidator.RuleIndividualModelDiversity);
        Assert.Equal("Arch1", violation.AffectedModelId);
    }

    [Fact]
    public void ValidateShow_DifferentGroupMembersEachGetTheirOwnMoment_DoesNotFlagIndividualModelDiversity()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300));
        var look = BuildLook("each-arch-once", "hold", []);
        var effects = new[]
        {
            new EffectInstance(Guid.NewGuid(), new ModelId("Arch1"), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>()),
            new EffectInstance(Guid.NewGuid(), new ModelId("Arch2"), 0, 1000, 2000, "recipe", "1", new Dictionary<string, string>()),
            new EffectInstance(Guid.NewGuid(), new ModelId("Arch3"), 0, 2000, 3000, "recipe", "1", new Dictionary<string, string>()),
        };
        var ir = new ChoreographyIR([], effects, []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleIndividualModelDiversity);
    }

    [Fact]
    public void ValidateShow_GroupWithFewerThanThreePositionedMembers_NeverFlaggedForIndividualModelDiversity()
    {
        // Only 2 positioned members -- below the real threshold where "only one named directly" is even
        // a meaningful observation.
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200));
        var look = BuildLook("arch1-twice", "hold", []);
        var effects = new[]
        {
            new EffectInstance(Guid.NewGuid(), new ModelId("Arch1"), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>()),
            new EffectInstance(Guid.NewGuid(), new ModelId("Arch1"), 0, 1000, 2000, "recipe", "1", new Dictionary<string, string>()),
        };
        var ir = new ChoreographyIR([], effects, []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arch1")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleIndividualModelDiversity);
    }

    private static EffectInstance[] EffectsOn(params string[] modelNames) =>
        modelNames.Select(name => new EffectInstance(Guid.NewGuid(), new ModelId(name), 0, 0, 1000, "recipe", "1", new Dictionary<string, string>())).ToArray();

    /// <summary>User feedback (2026-11-16): "much of the sequence... only a single model with an effect" -- a section whose Look's compiled IR only ever touches one real model, for its whole window, is exactly the sparse pattern this self-healing rule exists to catch (and its own real SectionId, carried for RevisionDirectiveDeriver to target the fix at).</summary>
    [Fact]
    public void ValidateShow_SectionLightsOnlyOneModel_FlagsSectionModelBreadth()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300));
        var look = BuildLook("arch1-only", "hold", []);
        var ir = new ChoreographyIR([], EffectsOn("Arch1", "Arch1"), []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arch1")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        var violation = Assert.Single(violations, v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
        Assert.Equal("s1", violation.AffectedSectionId);
    }

    /// <summary>User feedback (2026-11-16): the floor was raised from 2 to 3 ("multiple at a time on multiple models... more and more") -- two distinct models is now still below it.</summary>
    [Fact]
    public void ValidateShow_SectionLightsTwoDistinctModels_StillFlagsSectionModelBreadth()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300));
        var look = BuildLook("arch1-and-arch2", "hold", []);
        var ir = new ChoreographyIR([], EffectsOn("Arch1", "Arch2"), []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arch1"), new ModelId("Arch2")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
    }

    [Fact]
    public void ValidateShow_SectionLightsThreeDistinctModels_DoesNotFlagSectionModelBreadth()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300));
        var look = BuildLook("arch1-arch2-arch3", "hold", []);
        var ir = new ChoreographyIR([], EffectsOn("Arch1", "Arch2", "Arch3"), []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment(
                "s1", look, ir, [new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3")]),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
    }

    /// <summary>Phrase-scoped sub-windows (build-plan Phase 10 §3e) compile to several separate SectionAssignment entries sharing one SectionId -- a later phrase deliberately receding to one subject must not be flagged on its own slice; only the whole section's own union across every phrase is measured.</summary>
    [Fact]
    public void ValidateShow_PhraseScopedSectionUnionsAcrossSlices_DoesNotFlagSectionModelBreadth()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300));
        var fullLook = BuildLook("full", "hold", []);
        var recedeLook = BuildLook("recede", "hold", []);
        var fullIr = new ChoreographyIR([], EffectsOn("Arch1", "Arch2", "Arch3"), []);
        var recedeIr = new ChoreographyIR([], EffectsOn("Arch1"), []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment("s1", fullLook, fullIr, [new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3")]),
            new ChoreographyLegalityValidator.SectionAssignment("s1", recedeLook, recedeIr, [new ModelId("Arch1")]),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
    }

    [Fact]
    public void ValidateShow_DeclaredDarkSectionLightsOnlyOneModel_DoesNotFlagSectionModelBreadth()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300));
        var look = BuildLook("arch1-only-dark", "hold", [], declaredDarkness: true);
        var ir = new ChoreographyIR([], EffectsOn("Arch1"), []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, [new ModelId("Arch1")]) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
    }

    [Fact]
    public void ValidateShow_DenseSectionKindLightsThreeModels_StillFlagsSectionModelBreadth()
    {
        // Dense (Chorus/Bridge/Solo) section kinds need 5+, not 3+ -- three is still below their own,
        // two-higher floor.
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300), ("Arch4", 400), ("Arch5", 500));
        var look = BuildLook("arch-trio-chorus", "hold", []);
        var ir = new ChoreographyIR([], EffectsOn("Arch1", "Arch2", "Arch3"), []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment(
                "s1", look, ir, [new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3")], Kind: SectionKind.Chorus),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
    }

    [Fact]
    public void ValidateShow_DenseSectionKindLightsFiveModels_DoesNotFlagSectionModelBreadth()
    {
        var layout = BuildArchesLayout(("Arch1", 100), ("Arch2", 200), ("Arch3", 300), ("Arch4", 400), ("Arch5", 500));
        var look = BuildLook("arch-quintet-chorus", "hold", []);
        var ir = new ChoreographyIR([], EffectsOn("Arch1", "Arch2", "Arch3", "Arch4", "Arch5"), []);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment(
                "s1", look, ir, [new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3"), new ModelId("Arch4"), new ModelId("Arch5")], Kind: SectionKind.Chorus),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == ChoreographyLegalityValidator.RuleSectionModelBreadth);
    }

    private static ChoreographyIR BuildIrWithEffectCount(int count)
    {
        var effects = Enumerable.Range(0, count)
            .Select(i => new EffectInstance(Guid.NewGuid(), new ModelId("Tree"), 0, i * 1000L, (i + 1) * 1000L, "recipe", "1", new Dictionary<string, string>()))
            .ToList();
        return new ChoreographyIR([], effects, []);
    }

    private static EnergyEnvelope BuildEnergyEnvelope(float normalizedEnergy, int sampleRate = 8000, int seconds = 10) =>
        new(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", "decoder/1", "test/1",
            new RhythmSettings(sampleRate, 256, 128),
            [new EnergyFrame(0, seconds * (long)sampleRate, normalizedEnergy, normalizedEnergy)]);

    [Fact]
    public void ValidateShow_UndeclaredDarkSectionWithAudibleRealAudio_FlagsSilenceCrossValidation()
    {
        // Build-plan "richer analysis tools" Phase 5 (2026-11-02).
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("dark-look", "hold", []);
        var window = new TimeWindow(0, 10 * TicksPerSecond);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectCount(0), [new ModelId("Tree")], window) };
        var audioEnergy = BuildEnergyEnvelope(normalizedEnergy: 0.9f);

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage, audioEnergy);

        Assert.Contains(violations, v => v.Rule == "declared-darkness-vs-measured-audio-silence");
    }

    [Fact]
    public void ValidateShow_DeclaredDarkSectionWithAudibleRealAudio_FlagsSilenceCrossValidation()
    {
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("declared-dark-look", "hold", [], declaredDarkness: true);
        var window = new TimeWindow(0, 10 * TicksPerSecond);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectCount(0), [new ModelId("Tree")], window) };
        var audioEnergy = BuildEnergyEnvelope(normalizedEnergy: 0.9f);

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage, audioEnergy);

        Assert.Contains(violations, v => v.Rule == "declared-darkness-vs-measured-audio-silence");
    }

    [Fact]
    public void ValidateShow_DeclaredDarkSectionWithGenuinelyQuietRealAudio_DoesNotFlagSilenceCrossValidation()
    {
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("declared-dark-look", "hold", [], declaredDarkness: true);
        var window = new TimeWindow(0, 10 * TicksPerSecond);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectCount(0), [new ModelId("Tree")], window) };
        var audioEnergy = BuildEnergyEnvelope(normalizedEnergy: 0.05f);

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage, audioEnergy);

        Assert.DoesNotContain(violations, v => v.Rule == "declared-darkness-vs-measured-audio-silence");
    }

    [Fact]
    public void ValidateShow_NoAudioEnergySupplied_SkipsSilenceCrossValidation_RatherThanInventingValues()
    {
        var layout = BuildLayout();
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("dark-look", "hold", []);
        var window = new TimeWindow(0, 10 * TicksPerSecond);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, BuildIrWithEffectCount(0), [new ModelId("Tree")], window) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "declared-darkness-vs-measured-audio-silence");
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

    private static DisplayGraph BuildMultiModelLayout(int modelCount)
    {
        var models = Enumerable.Range(1, modelCount)
            .Select(i => new DisplayModel(new ModelId($"M{i}"), $"M{i}", "Star", [], [], NodeCount: 50))
            .ToList();
        return new DisplayGraph("TEST-HASH", models, Groups: [], Warnings: []);
    }

    private static EffectInstance BuildFocalEffect(int modelIndex, long startTicks, long endTicks) =>
        new(Guid.NewGuid(), new ModelId($"M{modelIndex}"), 0, startTicks, endTicks, "recipe", "1", new Dictionary<string, string>());

    [Fact]
    public void ValidateShow_FiveModelsSimultaneouslyFocal_FlagsConcurrentFocalEffectCapViolation()
    {
        // Build-plan Phase 26: default (non-dense) cap is 4 concurrent focal models -- a 5th model with a
        // short, clearly-not-a-hold effect overlapping the same instant must trip the new rule.
        var layout = BuildMultiModelLayout(5);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("busy-look", "hold", []);
        var window = new TimeWindow(0, 10 * TicksPerSecond);

        var effects = Enumerable.Range(1, 5).Select(i => BuildFocalEffect(i, 0, 1 * TicksPerSecond)).ToList();
        var ir = new ChoreographyIR([], effects, []);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, Enumerable.Range(1, 5).Select(i => new ModelId($"M{i}")).ToList(), window) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == "concurrent-focal-effect-cap");
    }

    [Fact]
    public void ValidateShow_FourModelsSimultaneouslyFocal_DoesNotFlagConcurrentFocalEffectCapViolation()
    {
        var layout = BuildMultiModelLayout(4);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("ok-look", "hold", []);
        var window = new TimeWindow(0, 10 * TicksPerSecond);

        var effects = Enumerable.Range(1, 4).Select(i => BuildFocalEffect(i, 0, 1 * TicksPerSecond)).ToList();
        var ir = new ChoreographyIR([], effects, []);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, Enumerable.Range(1, 4).Select(i => new ModelId($"M{i}")).ToList(), window) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "concurrent-focal-effect-cap");
    }

    [Fact]
    public void ValidateShow_FiveModelsFocalInChorusSection_DoesNotFlagConcurrentFocalEffectCapViolation()
    {
        // Build-plan Phase 26: Chorus/Bridge/Solo sections get a one-higher cap (5) than an ordinary
        // section, grounded in PatternLibrary.DensityBySection measuring those kinds as reliably denser.
        var layout = BuildMultiModelLayout(5);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("chorus-look", "hold", []);
        var window = new TimeWindow(0, 10 * TicksPerSecond);

        var effects = Enumerable.Range(1, 5).Select(i => BuildFocalEffect(i, 0, 1 * TicksPerSecond)).ToList();
        var ir = new ChoreographyIR([], effects, []);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, Enumerable.Range(1, 5).Select(i => new ModelId($"M{i}")).ToList(), window, Kind: SectionKind.Chorus),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "concurrent-focal-effect-cap");
    }

    [Fact]
    public void ValidateShow_SixModelsFocalInChorusSection_FlagsConcurrentFocalEffectCapViolation()
    {
        var layout = BuildMultiModelLayout(6);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("chorus-look", "hold", []);
        var window = new TimeWindow(0, 10 * TicksPerSecond);

        var effects = Enumerable.Range(1, 6).Select(i => BuildFocalEffect(i, 0, 1 * TicksPerSecond)).ToList();
        var ir = new ChoreographyIR([], effects, []);
        var sections = new[]
        {
            new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, Enumerable.Range(1, 6).Select(i => new ModelId($"M{i}")).ToList(), window, Kind: SectionKind.Chorus),
        };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.Contains(violations, v => v.Rule == "concurrent-focal-effect-cap");
    }

    [Fact]
    public void ValidateShow_FiveModelsWithLongHoldEffects_DoesNotFlagConcurrentFocalEffectCapViolation()
    {
        // Build-plan Phase 26: an effect spanning >= 60% of its own section's window (here 7s of a 10s
        // window) is a steady-state "hold," not a focal gesture -- excluded from the concurrency count
        // even when five of them overlap, so ordinary base+accent layering never trips this rule.
        var layout = BuildMultiModelLayout(5);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("holds-look", "hold", []);
        var window = new TimeWindow(0, 10 * TicksPerSecond);

        var effects = Enumerable.Range(1, 5).Select(i => BuildFocalEffect(i, 0, 7 * TicksPerSecond)).ToList();
        var ir = new ChoreographyIR([], effects, []);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, Enumerable.Range(1, 5).Select(i => new ModelId($"M{i}")).ToList(), window) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "concurrent-focal-effect-cap");
    }

    [Fact]
    public void ValidateShow_SectionMissingWindow_SkipsConcurrentFocalEffectCapCheck_RatherThanInventingValues()
    {
        var layout = BuildMultiModelLayout(5);
        var coverage = PixelCoverageLadder.Build(layout, 7);
        var look = BuildLook("legacy-look", "hold", []);

        var effects = Enumerable.Range(1, 5).Select(i => BuildFocalEffect(i, 0, 1 * TicksPerSecond)).ToList();
        var ir = new ChoreographyIR([], effects, []);
        var sections = new[] { new ChoreographyLegalityValidator.SectionAssignment("s1", look, ir, Enumerable.Range(1, 5).Select(i => new ModelId($"M{i}")).ToList()) };

        var violations = ChoreographyLegalityValidator.ValidateShow(sections, layout, coverage);

        Assert.DoesNotContain(violations, v => v.Rule == "concurrent-focal-effect-cap");
    }
}
