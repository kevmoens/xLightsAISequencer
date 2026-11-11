using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 22 (contrast-driven emphasis): §2c's real measurement and §2d's bounded deterministic
/// repair, both exercised here against a small synthetic compiled IR (no real xLights vocabulary needed --
/// same discipline as <see cref="AlwaysMovingGapSplitterTests"/>).
/// </summary>
public sealed class EmphasisContrastPlannerTests
{
    private const long TicksPerSecond = 10_000_000L;
    private static readonly ModelId Tree = new("Tree");
    private static readonly ModelId Arch = new("Arch");

    private sealed class FakeRecipe(string recipeId, string xlightsEffectName) : IEffectRecipe
    {
        public string RecipeId => recipeId;
        public string RecipeVersion => "1";
        public string XlightsEffectName => xlightsEffectName;
        public bool IsSupportedParameterValue(string key, string value) => true;
        public IReadOnlyDictionary<string, string> CreateDefaultParameters() => new Dictionary<string, string>();
    }

    /// <summary>Deterministic stand-in for <see cref="Random"/> -- always picks the minimum end of the §2a 0.5-1 beat window, so a regression test can predict the exact resulting tick boundaries.</summary>
    private sealed class FixedFractionRandom : Random
    {
        public override double NextDouble() => 0.0;
    }

    private sealed class StubRecipeRegistry : IEffectRecipeRegistry
    {
        public EffectInstance CreateVerified(string recipeId, ModelId target, long startTicks, long endTicks)
        {
            if (endTicks <= startTicks)
            {
                throw new EffectPlanInvalidException("empty window");
            }

            return new EffectInstance(Guid.NewGuid(), target, 0, startTicks, endTicks, recipeId, "1", new Dictionary<string, string>());
        }

        public bool IsSupportedParameterValue(string recipeId, string key, string value) => true;
    }

    private static EffectRecipeCatalog BuildRecipeCatalog()
    {
        var catalog = new EffectRecipeCatalog();
        catalog.Register(new FakeRecipe("effect.generated.colorwash.v1", "Color Wash"));
        catalog.Register(new FakeRecipe("effect.generated.twinkle.v1", "Twinkle"));
        catalog.Register(new FakeRecipe("effect.generated.on.v1", "On"));
        return catalog;
    }

    private static EffectVisualCharacterCatalog BuildBrightnessCatalog()
    {
        var now = DateTimeOffset.UtcNow;
        return new EffectVisualCharacterCatalog(now,
        [
            new EffectVisualCharacter("Color Wash", "High", "desc", now, MeanBrightness: 0.9, PeakBrightness: 0.9, BrightnessTier: BrightnessTier.Bright),
            new EffectVisualCharacter("On", "High", "desc", now, MeanBrightness: 0.95, PeakBrightness: 0.95, BrightnessTier: BrightnessTier.Bright),
            new EffectVisualCharacter("Twinkle", "High", "desc", now, MeanBrightness: 0.1, PeakBrightness: 0.3, BrightnessTier: BrightnessTier.Dim),
        ]);
    }

    private static BeatGrid BuildBeatGrid(int beatCount, long beatTicks)
    {
        var beats = new List<BeatMarker>();
        for (var i = 0; i < beatCount; i++)
        {
            beats.Add(new BeatMarker(Guid.NewGuid(), i * beatTicks, i % 4, i / 4, 1.0, false));
        }

        return new BeatGrid(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), Guid.NewGuid(),
            "test/1.0.0", new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: 1000),
            SelectedTempo: null, beats.ToArray(), Candidates: [], CorrectionVersion: 0);
    }

    private static DisplayGraph BuildLayout()
    {
        var tree = new DisplayModel(Tree, "Tree", "Tree", [], [], NodeCount: 100);
        var arch = new DisplayModel(Arch, "Arch", "Arch", [], [], NodeCount: 50);
        var group = new DisplayModelGroup("Accents", [new DisplayGroupMember("Tree", DisplayGroupMemberKind.Model), new DisplayGroupMember("Arch", DisplayGroupMemberKind.Model)]);
        return new DisplayGraph("hash", [tree, arch], [group], []);
    }

    [Fact]
    public void BuildEmphasisSets_ReturnsGroupsIndividualModelsAndWholeShow()
    {
        var sets = EmphasisContrastPlanner.BuildEmphasisSets(BuildLayout());

        Assert.Contains(sets, s => s.Name == "group:Accents" && s.Models.Count == 2);
        Assert.Contains(sets, s => s.Name == "Tree" && s.Models is [var m] && m == Tree);
        Assert.Contains(sets, s => s.Name == "Arch" && s.Models is [var a] && a == Arch);
        Assert.Contains(sets, s => s.Name == "*whole-show*" && s.Models.Count == 2);
    }

    [Fact]
    public void Measure_SameTierTransition_HasNoContrast()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        // Bright ("On") straight into bright ("Color Wash") at the instant -- the exact "no real
        // contrast" case the user's own complaint named.
        var instantTicks = 4 * beatTicks;
        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.on.v1", Tree, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };

        var findings = EmphasisContrastPlanner.Measure(ir, instants, beatGrid, recipeCatalog, brightnessCatalog);

        var finding = Assert.Single(findings);
        Assert.Equal(BrightnessTier.Bright, finding.PrecedingTier);
        Assert.Equal(BrightnessTier.Bright, finding.FollowingTier);
        Assert.False(finding.HasContrast);
    }

    [Fact]
    public void Measure_DimToBrightTransition_HasContrast()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        var instantTicks = 4 * beatTicks;
        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.twinkle.v1", Tree, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };

        var findings = EmphasisContrastPlanner.Measure(ir, instants, beatGrid, recipeCatalog, brightnessCatalog);

        var finding = Assert.Single(findings);
        Assert.True(finding.HasContrast);
    }

    [Fact]
    public void ApplyContrastFallback_PatchesAMissedInstant_WithoutIntroducingAGap()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var barGrid = BarGrid.FromBeatGrid(beatGrid, 8 * beatTicks);
        var layout = BuildLayout();
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        var instantTicks = 4 * beatTicks;
        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.on.v1", Tree, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };
        var sectionWindows = new List<TimeWindow> { new(0, 8 * beatTicks) };

        var (patchedIr, report) = EmphasisContrastPlanner.ApplyContrastFallback(
            ir, instants, sectionWindows, beatGrid, layout, recipeCatalog, brightnessCatalog, registry, new Random(1));

        Assert.Equal(1, report.MeasuredPairCount);
        Assert.Equal(0, report.ContrastingPairCountBeforeFallback);
        Assert.Equal(0.0, report.AdoptionRateBeforeFallback);
        Assert.Equal(1, report.PatchesApplied);
        Assert.Contains(Tree, report.DistinctPatchedTargets);

        var treeEffects = patchedIr.Effects.Where(e => e.Target == Tree).OrderBy(e => e.StartTicks).ToList();
        Assert.True(treeEffects.Count >= 3);

        // No gap introduced -- every consecutive pair is contiguous.
        for (var i = 1; i < treeEffects.Count; i++)
        {
            Assert.Equal(treeEffects[i - 1].EndTicks, treeEffects[i].StartTicks);
        }

        Assert.Equal(0, treeEffects[0].StartTicks);
        Assert.Equal(8 * beatTicks, treeEffects[^1].EndTicks);

        // The patched slice immediately before the instant must now resolve to an Off/Dim tier.
        var patch = treeEffects.Last(e => e.EndTicks == instantTicks);
        var patchRecipe = recipeCatalog.Recipes.First(r => r.RecipeId == patch.RecipeId);
        var patchTier = brightnessCatalog.Find(patchRecipe.XlightsEffectName)?.BrightnessTier;
        Assert.True(patchTier is BrightnessTier.Off or BrightnessTier.Dim);

        // Re-measuring the final IR should show real contrast now.
        var findingsAfter = EmphasisContrastPlanner.Measure(patchedIr, instants, beatGrid, recipeCatalog, brightnessCatalog);
        Assert.All(findingsAfter, f => Assert.True(f.HasContrast));
    }

    /// <summary>
    /// Real-run finding (2026-11-09, full-length real track): trimming the preceding effect down to
    /// exactly the patch's own computed window start can leave a REMAINDER shorter than one real exported
    /// frame (25ms) -- a real <c>XsqExportInvalidException</c> ("rounds to a non-positive duration") on a
    /// real compile, not a hypothetical. The fix absorbs a too-short remainder into the patch itself
    /// (grown backward to the preceding effect's own start) instead of leaving it behind.
    /// </summary>
    [Fact]
    public void ApplyContrastFallback_TrimmedRemainderShorterThanOneFrame_IsAbsorbedIntoThePatch_NeverLeftAsASubFrameEffect()
    {
        const long beatTicks = TicksPerSecond / 2; // 5,000,000 ticks.
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var layout = BuildLayout();
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        var instantTicks = 4 * beatTicks; // 20,000,000 -- FixedFractionRandom forces a 0.5-beat (2,500,000-tick) desired window, so the unclamped desired start is 17,500,000.
        const long precedingStartTicks = 17_400_000L; // Only 100,000 ticks (10ms) before the desired start -- shorter than FrameClock's own 25ms (250,000-tick) frame grid.

        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.on.v1", Tree, precedingStartTicks, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };
        var sectionWindows = new List<TimeWindow> { new(0, 8 * beatTicks) };

        var (patchedIr, report) = EmphasisContrastPlanner.ApplyContrastFallback(
            ir, instants, sectionWindows, beatGrid, layout, recipeCatalog, brightnessCatalog, registry, new FixedFractionRandom());

        Assert.Equal(1, report.PatchesApplied);

        var treeEffects = patchedIr.Effects.Where(e => e.Target == Tree).OrderBy(e => e.StartTicks).ToList();

        // The too-short remainder must never survive as its own effect -- exactly 2 effects remain (the
        // grown patch, then the original following effect), never 3.
        Assert.Equal(2, treeEffects.Count);
        Assert.Equal(precedingStartTicks, treeEffects[0].StartTicks);
        Assert.Equal(instantTicks, treeEffects[0].EndTicks);
        Assert.True(treeEffects[0].EndTicks - treeEffects[0].StartTicks >= FrameClock.FrameIntervalTicks);
        Assert.Equal(instantTicks, treeEffects[1].StartTicks);

        // No gap introduced.
        Assert.Equal(treeEffects[0].EndTicks, treeEffects[1].StartTicks);
    }

    [Fact]
    public void ApplyContrastFallback_AlreadyContrasting_NeverPatches()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var layout = BuildLayout();
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        var instantTicks = 4 * beatTicks;
        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.twinkle.v1", Tree, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };
        var sectionWindows = new List<TimeWindow> { new(0, 8 * beatTicks) };

        var (patchedIr, report) = EmphasisContrastPlanner.ApplyContrastFallback(
            ir, instants, sectionWindows, beatGrid, layout, recipeCatalog, brightnessCatalog, registry, new Random(1));

        Assert.Equal(0, report.PatchesApplied);
        Assert.Equal(1.0, report.AdoptionRateBeforeFallback);
        Assert.Equal(effects.Count, patchedIr.Effects.Count);
    }

    [Fact]
    public void ApplyContrastFallback_TwoCandidateTargetsAtTheSameInstant_PatchesExactlyOne()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var layout = BuildLayout();
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        var instantTicks = 4 * beatTicks;
        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.on.v1", Tree, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
            registry.CreateVerified("effect.generated.on.v1", Arch, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Arch, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };
        var sectionWindows = new List<TimeWindow> { new(0, 8 * beatTicks) };

        var (_, report) = EmphasisContrastPlanner.ApplyContrastFallback(
            ir, instants, sectionWindows, beatGrid, layout, recipeCatalog, brightnessCatalog, registry, new Random(1));

        Assert.Equal(2, report.MeasuredPairCount);
        Assert.Equal(1, report.PatchesApplied);
        Assert.Single(report.DistinctPatchedTargets);
    }

    [Fact]
    public void ValidateEmphasisContrast_FlagsAMissedInstant_AsAdvisoryNeverBlocking()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        var instantTicks = 4 * beatTicks;
        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.on.v1", Tree, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };

        var violations = ChoreographyLegalityValidator.ValidateEmphasisContrast(ir, instants, beatGrid, recipeCatalog, brightnessCatalog);

        var violation = Assert.Single(violations);
        Assert.Equal(ChoreographyLegalityValidator.RuleContrastAtEmphasis, violation.Rule);
        Assert.Equal("Tree", violation.AffectedModelId);
    }

    [Fact]
    public void ValidateEmphasisContrast_RealContrast_ReportsNothing()
    {
        const long beatTicks = TicksPerSecond / 2;
        var beatGrid = BuildBeatGrid(beatCount: 8, beatTicks);
        var registry = new StubRecipeRegistry();
        var recipeCatalog = BuildRecipeCatalog();
        var brightnessCatalog = BuildBrightnessCatalog();

        var instantTicks = 4 * beatTicks;
        var effects = new List<EffectInstance>
        {
            registry.CreateVerified("effect.generated.twinkle.v1", Tree, 0, instantTicks),
            registry.CreateVerified("effect.generated.colorwash.v1", Tree, instantTicks, 8 * beatTicks),
        };
        var ir = new ChoreographyIR([], effects, []);
        var instants = new List<EmphasisInstant> { new(instantTicks, EmphasisSignalKind.TransientHit) };

        var violations = ChoreographyLegalityValidator.ValidateEmphasisContrast(ir, instants, beatGrid, recipeCatalog, brightnessCatalog);

        Assert.Empty(violations);
    }
}
