using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 22: confirms <see cref="ChoreographyEngine.Compile"/> itself wires the new,
/// additive <c>Input.EmphasisInstants</c>/<c>Input.BrightnessCatalog</c> fields through to a real
/// <c>Result.EmphasisContrast</c> report end to end (against the real generated vocabulary, same fixture
/// every other Phase 4 engine test uses) -- and that omitting them (every pre-existing caller) leaves the
/// engine's own behavior completely unchanged.
/// </summary>
public sealed class EmphasisContrastEngineWiringTests
{
    private const long TicksPerSecond = 10_000_000L;
    private const long BarTicks = 2 * TicksPerSecond;
    private const int BarCount = 4;
    private const long SongEndTicks = BarCount * BarTicks;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models: [new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900)],
        Groups: [],
        Warnings: []);

    private static BeatGrid BuildBeatGrid(SongId songId)
    {
        var beats = Enumerable.Range(0, BarCount * 4).Select(i => new BeatMarker(Guid.NewGuid(), i * (BarTicks / 4), i % 4, i / 4, 1.0, false)).ToArray();
        return new BeatGrid(Guid.NewGuid(), songId, "h", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), new TempoHypothesis(120, 0.9, "4/4"), beats, [], 0);
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

    private static ChoreographyEngine.Input BuildBaseInput(IReadOnlyList<EmphasisInstant>? instants, EffectVisualCharacterCatalog? brightnessCatalog)
    {
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var palettes = new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) };
        var compilerSettings = new LookCompilerSettings(vocabulary, recipes, registry, palettes);

        var layout = BuildLayout();
        var beatGrid = BuildBeatGrid(new SongId(Guid.NewGuid()));
        var (sections, sectionId) = BuildSections(beatGrid.SongId);
        var phrases = BuildPhrases(beatGrid.SongId);

        var look = new Look(
            "look", "Look", "One bound subject layer.", 0.9, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "Color Wash", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

        var fillerSchedule = SectionFillerSchedule.Flat(RealXLightsFixture.BuildFillerPool(recipes), SongEndTicks);

        return new ChoreographyEngine.Input(
            layout, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: null,
            new LookLibrary([look]), LookIdBySectionId: new Dictionary<Guid, string> { [sectionId] = "look" },
            EnergyCeilingByKind: new Dictionary<SectionKind, double> { [SectionKind.Verse] = 1.0 },
            SongEndTicks, compilerSettings, fillerSchedule,
            EmphasisInstants: instants, BrightnessCatalog: brightnessCatalog);
    }

    [Fact]
    public void Compile_WithoutEmphasisInputs_ResultEmphasisContrastStaysNull()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var input = BuildBaseInput(instants: null, brightnessCatalog: null);
        var result = ChoreographyEngine.Compile(input);

        Assert.Null(result.EmphasisContrast);
    }

    [Fact]
    public void Compile_WithRealEmphasisInputs_PopulatesEmphasisContrastReport()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var brightnessCatalog = new EffectVisualCharacterCatalog(now,
        [
            new EffectVisualCharacter("Color Wash", "High", "desc", now, MeanBrightness: 0.9, PeakBrightness: 0.9, BrightnessTier: BrightnessTier.Bright),
            new EffectVisualCharacter("Twinkle", "High", "desc", now, MeanBrightness: 0.1, PeakBrightness: 0.3, BrightnessTier: BrightnessTier.Dim),
        ]);

        // A real transient hit landing mid-song, on a target whose own whole section plays one flat
        // bright effect -- exactly the "no real contrast" case this phase exists to repair.
        var instants = new List<EmphasisInstant> { new(2 * BarTicks, EmphasisSignalKind.TransientHit) };

        var input = BuildBaseInput(instants, brightnessCatalog);
        var result = ChoreographyEngine.Compile(input);

        Assert.NotNull(result.EmphasisContrast);
        Assert.True(result.EmphasisContrast!.MeasuredPairCount >= 0);

        // The always-moving rule must never regress as a side effect of a deliberate dip.
        var largestGap = AlwaysMovingGapSplitter.LargestGapTicks(new ModelId("Tree"), result.IR.Effects, new TimeWindow(0, SongEndTicks));
        Assert.True(largestGap <= BarTicks, $"Largest gap {largestGap} ticks exceeds one bar ({BarTicks} ticks).");
    }
}
