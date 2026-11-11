using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// User follow-up (2026-11-10): confirms a Look layer declaring <c>Timescale: "Transients"</c> actually
/// reacts to real detected transient hits end to end through <see cref="ChoreographyEngine.Compile"/> --
/// no new compiler mechanism, just <see cref="TimingTrackEmitter.Transients"/> feeding the SAME generic
/// Mode B discrete-emission machinery <c>Beats</c>/<c>Bars</c> already use (<see cref="LookCompiler.ResolveEventWindows"/>
/// looks up a layer's own declared <see cref="LookLayer.Timescale"/> against whatever named tracks exist,
/// with no special-casing per track name).
/// </summary>
public sealed class TransientsTimingTrackEngineWiringTests
{
    private const long TicksPerSecond = 10_000_000L;
    private const long BarTicks = 2 * TicksPerSecond;
    private const int BarCount = 10;
    private const long SongEndTicks = BarCount * BarTicks;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models: [new DisplayModel(new ModelId("Arch"), "Arch", "Arch", [], [], NodeCount: 200)],
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

    [Fact]
    public void Compile_LayerBoundToTransients_ProducesShortDiscreteEffectsAtRealHits()
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
        var beatGrid = BuildBeatGrid(new SongId(Guid.NewGuid()));
        var (sections, sectionId) = BuildSections(beatGrid.SongId);
        var phrases = BuildPhrases(beatGrid.SongId);

        // "On" is not one of EmissionStrategySelector's seven timing-track-BOUND effects, so it always
        // compiles via Mode B (discrete) -- exactly the real reference show's own dominant technique.
        var look = new Look(
            "look", "Look", "A short, high-duty accent layer bound to real detected hits.", 0.9, "hold", "warm-gold", "Arch",
            [new LookLayer(1, "Arch", "On", LookLayer.RoleSubject, null, "Normal", "Transients", 0.9)]);

        var fillerSchedule = SectionFillerSchedule.Flat(RealXLightsFixture.BuildFillerPool(recipes), SongEndTicks);

        var hitTicks = new[] { 3 * BarTicks, 6 * BarTicks + (BarTicks / 2) };
        var instants = hitTicks.Select(t => new EmphasisInstant(t, EmphasisSignalKind.TransientHit)).ToList();

        var input = new ChoreographyEngine.Input(
            layout, beatGrid, sections, phrases, BandEnergy: null, VocalActivity: null,
            new LookLibrary([look]), LookIdBySectionId: new Dictionary<Guid, string> { [sectionId] = "look" },
            EnergyCeilingByKind: new Dictionary<SectionKind, double> { [SectionKind.Verse] = 1.0 },
            SongEndTicks, compilerSettings, fillerSchedule,
            EmphasisInstants: instants);

        var result = ChoreographyEngine.Compile(input);

        // One compiled "On" effect per real hit, each starting exactly at (or clamped very close to) the
        // real hit's own tick -- never spanning a whole bar/beat the way the regular grid would. This
        // Look declares only the one layer, so every effect targeting Arch at Layer 1 is this layer's own
        // output (Layer 0 is reserved for the always-moving gap-filler's own filler effects).
        var archTarget = new ModelId("Arch");
        var onEffects = result.IR.Effects.Where(e => e.Target == archTarget && e.Layer == 1).OrderBy(e => e.StartTicks).ToList();

        Assert.True(onEffects.Count >= hitTicks.Length, $"Expected at least {hitTicks.Length} 'On' effect(s) from the Transients-bound layer, found {onEffects.Count}.");

        foreach (var hit in hitTicks)
        {
            Assert.Contains(onEffects, e => Math.Abs(e.StartTicks - hit) < BarTicks / 4);
        }

        // Each one is SHORT -- well under a beat, nothing close to the quarter-bar+ spans a Bars/Beats-bound layer would produce.
        Assert.All(onEffects, e => Assert.True(e.EndTicks - e.StartTicks <= BarTicks / 4));
    }
}
