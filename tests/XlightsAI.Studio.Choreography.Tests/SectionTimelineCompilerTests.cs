using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 28a §5/§8 item 3: the two required cases for <see cref="SectionTimelineCompiler"/> -- anchor-exact compilation and HandoffFromProps staggering. Skips cleanly with no real xLights install present, same discipline as <see cref="LookCompilerTests"/>.</summary>
public sealed class SectionTimelineCompilerTests
{
    private const long TicksPerSecond = 10_000_000L;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            new DisplayModel(new ModelId("MiniStar1"), "MiniStar1", "Star", [], [], NodeCount: 60),
            new DisplayModel(new ModelId("MiniStar2"), "MiniStar2", "Star", [], [], NodeCount: 20),
        ],
        Groups: [],
        Warnings: []);

    private static BarGrid BuildBarGrid(int barCount, long barTicks)
    {
        var beats = Enumerable.Range(0, barCount).Select(bar => new BeatMarker(Guid.NewGuid(), bar * barTicks, 0, bar, 1.0, false)).ToArray();
        var beatGrid = new BeatGrid(Guid.NewGuid(), new SongId(Guid.NewGuid()), "h", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), null, beats, [], 0);
        return BarGrid.FromBeatGrid(beatGrid, barCount * barTicks);
    }

    private static SectionMusicContext BuildSection(BarGrid barGrid, int barCount, long barTicks) =>
        new(new TimeWindow(0, barCount * barTicks), SectionKind.Verse, 0.5, barGrid, new Dictionary<string, TimingTrack>(StringComparer.Ordinal), false);

    private static LookCompilerSettings BuildSettings()
    {
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var palettes = new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) };
        return new LookCompilerSettings(vocabulary, recipes, registry, palettes);
    }

    [Fact]
    public void Compile_CueOnRealTransientAnchor_CompiledEffectTickSpanMatchesRealTransientTime()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, 5, barTicks);
        var settings = BuildSettings();

        var look = new Look(
            "test-base", "Test Base", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

        var transient = new TransientHit(3 * TicksPerSecond, 0.9f);
        var timeline = new SectionTimeline(Guid.NewGuid(), "test-base", Cues:
        [
            new SectionCue("transient-1", "MiniStar1", "On", IntensityTier.Full, LookLayer.RoleSubject),
        ]);

        var ir = SectionTimelineCompiler.Compile(look, timeline, section, layout, settings, transientHits: [transient]);

        var overlay = Assert.Single(ir.Effects, e => e.Target == new ModelId("MiniStar1"));
        Assert.Equal(transient.TimeTicks, overlay.StartTicks);
    }

    [Fact]
    public void Compile_HandoffFromProps_CompilesToTwoStaggeredInstances_NotSimultaneous()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, 5, barTicks);
        var settings = BuildSettings();

        var look = new Look(
            "test-handoff", "Test Handoff", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

        var transient = new TransientHit(4 * TicksPerSecond, 0.9f);
        var timeline = new SectionTimeline(Guid.NewGuid(), "test-handoff", Cues:
        [
            new SectionCue("transient-1", "MiniStar2", "On", IntensityTier.Full, LookLayer.RoleSubject, HandoffFromProps: "MiniStar1"),
        ]);

        var ir = SectionTimelineCompiler.Compile(look, timeline, section, layout, settings, transientHits: [transient]);

        var toEffect = Assert.Single(ir.Effects, e => e.Target == new ModelId("MiniStar2"));
        var fromEffect = Assert.Single(ir.Effects, e => e.Target == new ModelId("MiniStar1"));
        Assert.NotEqual(toEffect.StartTicks, fromEffect.StartTicks); // a real stagger, not two simultaneous independent effects.
    }
}
