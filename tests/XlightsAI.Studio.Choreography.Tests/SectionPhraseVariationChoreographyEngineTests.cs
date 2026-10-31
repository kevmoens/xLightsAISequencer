using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 10 §3e: "let a section declare up to 2-3 phrase-scoped sub-section variations instead
/// of one flat Look for its entire duration." Exercises <see cref="ChoreographyEngine"/>'s own
/// <c>Input.PhraseLookAssignmentsBySectionId</c> end to end (no LLM/SectionScriptCompiler involved -- that
/// join is <see cref="SectionScriptCompilerTests"/>'s own scope) against the real generated vocabulary:
/// one section compiled from two phrase-scoped sub-windows, each its own Look, and confirms the compiled
/// IR actually differs per phrase (not the same Look rendered flat across the whole section).
/// </summary>
public sealed class SectionPhraseVariationChoreographyEngineTests
{
    private const long TicksPerSecond = 10_000_000L;
    private const long BarTicks = 2 * TicksPerSecond;
    private const int BarCount = 4;
    private const long SongEndTicks = BarCount * BarTicks;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            new DisplayModel(new ModelId("Outline"), "Outline", "Outline", [], [], NodeCount: 300),
        ],
        Groups: [],
        Warnings: []);

    private static BeatGrid BuildBeatGrid()
    {
        var beats = Enumerable.Range(0, BarCount).Select(bar => new BeatMarker(Guid.NewGuid(), bar * BarTicks, 0, bar, 1.0, false)).ToArray();
        return new BeatGrid(Guid.NewGuid(), new SongId(Guid.NewGuid()), "h", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), new TempoHypothesis(120, 0.9, "4/4"), beats, [], 0);
    }

    private static (SectionLabelAnalysis Sections, Guid SectionId) BuildSections(SongId songId)
    {
        var sectionId = Guid.NewGuid();
        var analysis = new SectionLabelAnalysis(
            Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), "t/1",
            new SectionLabelSettings(0.95, 0.4, "t/1"),
            [new SectionLabelCandidate(sectionId, Guid.NewGuid(), 0, BarCount, SectionKind.Verse, 1.0, ["test"], false)]);
        return (analysis, sectionId);
    }

    private static PhraseSegmentationAnalysis BuildPhrases(SongId songId) => new(
        Guid.NewGuid(), songId, "hash", Guid.NewGuid(), Guid.NewGuid(), null, "t/1",
        new PhraseSegmentationSettings(4, 1, 0, "t/1"), []);

    [Fact]
    public void Compile_TwoPhraseScopedAssignments_SecondPhraseDropsSubjectLayer_IRDiffersPerPhrase()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var palettes = new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) };
        var compilerSettings = new LookCompilerSettings(vocabulary, recipes, registry, palettes, RealXLightsFixture.BuildFillerPool(recipes));

        var layout = BuildLayout();
        var beatGrid = BuildBeatGrid();
        var (sections, sectionId) = BuildSections(beatGrid.SongId);
        var phrases = BuildPhrases(beatGrid.SongId);

        // "full" has both layers active (the base Look a section not using phrase staging would get for
        // its whole duration); "subject-only" drops the Outline layer -- the kind of "phrase 1 establishes
        // the Look, a later phrase recedes" staging §2e describes.
        var fullLook = new Look(
            "full", "Full", "Both layers.", 0.6, "hold", "warm-gold", "Tree",
            [
                new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.5),
                new LookLayer(2, "Outline", "Color Wash", LookLayer.RoleSupport, null, "Normal", "Bars", 0.9),
            ]);
        var subjectOnlyLook = fullLook with { Id = "subject-only", Layers = [fullLook.Layers[0]] };

        var phraseAssignments = new List<SectionPhraseLookAssignment>
        {
            new(new BarIndex(0), new BarIndex(2), "full"),
            new(new BarIndex(2), new BarIndex(4), "subject-only"),
        };

        var input = new ChoreographyEngine.Input(
            layout, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: null,
            new LookLibrary([fullLook, subjectOnlyLook]), LookIdBySectionId: new Dictionary<Guid, string>(),
            EnergyCeilingByKind: new Dictionary<SectionKind, double> { [SectionKind.Verse] = 1.0 },
            SongEndTicks, compilerSettings,
            PhraseLookAssignmentsBySectionId: new Dictionary<Guid, IReadOnlyList<SectionPhraseLookAssignment>> { [sectionId] = phraseAssignments });

        var result = ChoreographyEngine.Compile(input);

        Assert.Empty(result.Violations);

        var outlineModel = new ModelId("Outline");
        var secondPhraseStartTicks = 2 * BarTicks;

        // Layer z=2 (the Outline/Color Wash layer the "full" Look declares) is the real, intentional
        // layer under test -- distinct from the always-moving gap splitter's own filler effects, which
        // always compile at Layer 0 (ChoreographyEngine's own global, per-target pass that re-fills a
        // target's own timeline gaps with the filler pool once ANY effect has targeted it -- expected,
        // unrelated to this feature, and why this assertion checks Layer, not just Target).
        // First phrase (bars [0,2)): both layers active -- layer z=2 produces at least one effect whose
        // own window starts within the first phrase.
        Assert.Contains(result.IR.Effects, e => e.Target == outlineModel && e.Layer == 2 && e.StartTicks < secondPhraseStartTicks);

        // Second phrase (bars [2,4)): the "subject-only" Look dropped layer z=2 entirely for this phrase
        // -- no layer z=2 effect starts anywhere in the second phrase's own window. This is the actual
        // proof the phrase-scoped variation changed the compiled IR, not just the prompt text.
        Assert.DoesNotContain(result.IR.Effects, e => e.Target == outlineModel && e.Layer == 2 && e.StartTicks >= secondPhraseStartTicks);

        // The subject layer (Tree/Twinkle) stays active across both phrases.
        var treeModel = new ModelId("Tree");
        Assert.Contains(result.IR.Effects, e => e.Target == treeModel && e.StartTicks < secondPhraseStartTicks);
        Assert.Contains(result.IR.Effects, e => e.Target == treeModel && e.StartTicks >= secondPhraseStartTicks);
    }
}
