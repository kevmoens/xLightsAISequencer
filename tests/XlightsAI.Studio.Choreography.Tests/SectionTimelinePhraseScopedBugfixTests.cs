using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 28b bugfix: reproduces a real crash found against a real Carol of the Bells run --
/// `AnchorId 'beat-25' is not one of the real timing marks offered for section window [...]`. Root cause
/// (confirmed against that run's own real persisted <see cref="SectionScript.PhraseVariations"/> data,
/// not a synthetic-only repro): a <see cref="SectionTimeline"/>'s own <see cref="SectionCue.AnchorId"/>
/// is validated/offered against the section's own WHOLE real window (<c>BuildSectionPromptMaterial</c>,
/// <see cref="SectionTimelineValidator"/>), but <see cref="ChoreographyEngine"/>'s phrase-scoped branch
/// (<c>PhraseLookAssignmentsBySectionId</c>) compiles that same section as several NARROWER per-phrase
/// windows -- applying the whole-section timeline's cues uniformly against a phrase window that does not
/// contain an anchor valid for the whole section throws a hard <see cref="ChoreographyCompilationException"/>
/// for an anchor the Choreographer never did anything wrong to name. Fixed by never consulting
/// <see cref="ChoreographyEngine.Input.SectionTimelinesBySectionId"/> for a phrase-scoped sub-window call
/// -- the exact same precedence <c>PhraseLookAssignmentsBySectionId</c> already has over
/// <c>LookIdBySectionId</c> itself now also applies to <c>SectionTimelinesBySectionId</c>.
/// </summary>
public sealed class SectionTimelinePhraseScopedBugfixTests
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
    public void Compile_PhraseScopedSection_WithSectionTimelineCue_NeverThrows_CueIgnoredForEveryPhraseSubWindow()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var palettes = new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) };
        var compilerSettings = new LookCompilerSettings(vocabulary, recipes, registry, palettes);

        var layout = BuildLayout();
        var beatGrid = BuildBeatGrid();
        var (sections, sectionId) = BuildSections(beatGrid.SongId);
        var phrases = BuildPhrases(beatGrid.SongId);

        var look = new Look(
            "full", "Full", "Both layers.", 0.6, "hold", "warm-gold", "Tree",
            [
                new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.5),
                new LookLayer(2, "Outline", "Color Wash", LookLayer.RoleSupport, null, "Normal", "Bars", 0.9),
            ]);

        // Two real phrase sub-windows: [0,2) and [2,4) bars -- same shape as the real Carol of the Bells
        // run's own persisted PhraseVariations data.
        var phraseAssignments = new List<SectionPhraseLookAssignment>
        {
            new(new BarIndex(0), new BarIndex(2), "full"),
            new(new BarIndex(2), new BarIndex(4), "full"),
        };

        // A real cue naming "beat-3" -- a mark that exists for the WHOLE section's own 4-bar window
        // (beat-1..beat-4) but does NOT exist within the FIRST phrase's own narrower [0,2)-bar window
        // (which only ever offers beat-1/beat-2). Before the fix, compiling the first phrase sub-window
        // incorrectly consulted this section-wide timeline and threw resolving "beat-3" against that
        // phrase's own narrower mark set -- the exact real crash shape.
        var timeline = new SectionTimeline(sectionId, "full", Cues:
        [
            new SectionCue("beat-3", "Tree", "On", IntensityTier.Full, LookLayer.RoleSubject),
        ]);

        var fillerSchedule = SectionFillerSchedule.Flat(RealXLightsFixture.BuildFillerPool(recipes), SongEndTicks);

        var input = new ChoreographyEngine.Input(
            layout, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: null,
            new LookLibrary([look]), LookIdBySectionId: new Dictionary<Guid, string>(),
            EnergyCeilingByKind: new Dictionary<SectionKind, double> { [SectionKind.Verse] = 1.0 },
            SongEndTicks, compilerSettings, fillerSchedule,
            PhraseLookAssignmentsBySectionId: new Dictionary<Guid, IReadOnlyList<SectionPhraseLookAssignment>> { [sectionId] = phraseAssignments },
            SectionTimelinesBySectionId: new Dictionary<Guid, SectionTimeline> { [sectionId] = timeline });

        // Must not throw ChoreographyCompilationException -- this is the real bug's own reproduction.
        var result = ChoreographyEngine.Compile(input);

        // The cue must be genuinely ignored for this phrase-scoped section (not silently half-applied):
        // no compiled effect anywhere carries this cue's own SourceAnnotation/intensity-driven override,
        // confirming every phrase sub-window compiled via the plain LookCompiler path instead.
        Assert.DoesNotContain(result.IR.Effects, e => e.SourceAnnotation is not null);
    }
}
