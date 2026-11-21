using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 4 task 7 ("A Look -&gt; ChoreographyIR compiler") and task 5 (Mode A/B emission).
/// Skips cleanly when no real xLights install is present (see <see cref="RealXLightsFixture"/>) -- this
/// codebase's own discipline forbids inventing effect parameters, so these tests necessarily exercise
/// the real generated vocabulary rather than a hand-fabricated one.
/// </summary>
public sealed class LookCompilerTests
{
    private const long TicksPerSecond = 10_000_000L;

    private static DisplayGraph BuildLayout() => new(
        "TEST-HASH",
        Models:
        [
            new DisplayModel(new ModelId("Tree"), "Tree", "Tree", [], [], NodeCount: 900),
            new DisplayModel(new ModelId("MiniStar1"), "MiniStar1", "Star", [], [], NodeCount: 60),
            new DisplayModel(new ModelId("MiniStar2"), "MiniStar2", "Star", [], [], NodeCount: 40),
            new DisplayModel(new ModelId("MiniStar3"), "MiniStar3", "Star", [], [], NodeCount: 20),
        ],
        Groups: [new DisplayModelGroup("MiniStars",
        [
            new DisplayGroupMember("MiniStar1", DisplayGroupMemberKind.Model),
            new DisplayGroupMember("MiniStar2", DisplayGroupMemberKind.Model),
            new DisplayGroupMember("MiniStar3", DisplayGroupMemberKind.Model),
        ])],
        Warnings: []);

    private static BarGrid BuildBarGrid(int barCount, long barTicks)
    {
        var beats = Enumerable.Range(0, barCount).Select(bar => new BeatMarker(Guid.NewGuid(), bar * barTicks, 0, bar, 1.0, false)).ToArray();
        var beatGrid = new BeatGrid(Guid.NewGuid(), new SongId(Guid.NewGuid()), "h", Guid.NewGuid(), Guid.NewGuid(), "t/1", new BeatGridSettings(4, 1000), null, beats, [], 0);
        return BarGrid.FromBeatGrid(beatGrid, barCount * barTicks);
    }

    private static SectionMusicContext BuildSection(
        BarGrid barGrid, long barTicks, int barCount, double ceiling, bool vocalActive = false,
        double targetChangeRatePerSecond = double.PositiveInfinity)
    {
        var window = new TimeWindow(0, barCount * barTicks);
        var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
        {
            ["Bars"] = new TimingTrack("Bars", barGrid.Bars.Select(b => new TimingMark("", b.Window.StartTicks, b.Window.EndTicks)).ToList()),
            ["Beats"] = new TimingTrack("Beats", barGrid.Bars.Select(b => new TimingMark("", b.Window.StartTicks, b.Window.EndTicks)).ToList()),
        };
        return new SectionMusicContext(window, SectionKind.Verse, ceiling, barGrid, tracks, vocalActive, targetChangeRatePerSecond);
    }

    private static LookCompilerSettings BuildSettings()
    {
        var (vocabulary, recipes) = RealXLightsFixture.BuildCatalogs();
        var registry = new EffectRecipeRegistry(recipes);
        var palettes = new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) };
        return new LookCompilerSettings(vocabulary, recipes, registry, palettes);
    }

    [Fact]
    public void Compile_ModeA_SingleStrandBoundToTimingTrack_EmitsOneLongEffectWithTimingTrackParameter()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-bound", "Test Bound", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "SingleStrand", LookLayer.RoleSubject, null, "Normal", "Beats", 0.35)]);

        var ir = LookCompiler.Compile(look, section, layout, settings);

        var effect = Assert.Single(ir.Effects);
        Assert.Equal(0, effect.StartTicks);
        Assert.Equal(section.Window.EndTicks, effect.EndTicks); // one long effect spanning the whole section -- Mode A.
        Assert.Contains(effect.Parameters, p => p.Key.Contains("SingleStrand_TimingTrack", StringComparison.Ordinal) && p.Value == "Beats");
    }

    /// <summary>
    /// Confirmed as a real bug against a real generation (see <see cref="TimescaleResolver"/>'s own doc
    /// comment): <see cref="DirectorPromptBuilder"/>'s documented schema tells the AI to write
    /// <c>Timescale: "beat"</c> (lowercase, singular), not the real track name "Beats" this test's own
    /// section exposes -- an exact-match lookup of "beat" against "Beats" always missed, silently
    /// degrading a beat-synced Look to one event per bar. Same assertions as
    /// <see cref="Compile_ModeA_SingleStrandBoundToTimingTrack_EmitsOneLongEffectWithTimingTrackParameter"/>,
    /// but declared with the AI's own lowercase/singular word, to prove it now resolves identically.
    /// </summary>
    [Fact]
    public void Compile_ModeA_LowercaseSingularBeatTimescale_ResolvesToRealBeatsTrack()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-bound-lowercase", "Test Bound Lowercase", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "SingleStrand", LookLayer.RoleSubject, null, "Normal", "beat", 0.35)]);

        var ir = LookCompiler.Compile(look, section, layout, settings);

        var effect = Assert.Single(ir.Effects);
        Assert.Equal(0, effect.StartTicks);
        Assert.Equal(section.Window.EndTicks, effect.EndTicks); // one long effect spanning the whole section -- Mode A, same as the real "Beats" spelling.
        Assert.Contains(effect.Parameters, p => p.Key.Contains("SingleStrand_TimingTrack", StringComparison.Ordinal) && p.Value == "Beats");
    }

    /// <summary>Build-plan Phase 30 step 30.1: a <see cref="LookLayer.Blend"/> confirmed by <see cref="ConfirmedLayerBlendModeCatalog"/> now reaches the compiled <see cref="Xlights.EffectInstance.BlendMode"/> (Mode A).</summary>
    [Fact]
    public void Compile_ModeA_ConfirmedBlend_FlowsIntoCompiledEffectInstanceBlendMode()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-blend-confirmed", "Test Blend Confirmed", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "SingleStrand", LookLayer.RoleSubject, null, "2 is Mask", "Beats", 0.35)]);

        var ir = LookCompiler.Compile(look, section, layout, settings);

        var effect = Assert.Single(ir.Effects);
        Assert.Equal("2 is Mask", effect.BlendMode);
    }

    /// <summary>An unconfirmed/placeholder <see cref="LookLayer.Blend"/> value (never one of the 8 confirmed candidates) must never reach the compiled IR -- it silently falls back to the harmless default, same discipline <see cref="Xlights.XsqExporter"/> has always followed for every other unverified xLights XML value.</summary>
    [Fact]
    public void Compile_ModeA_UnconfirmedBlend_FallsBackToNormal_NeverReachesCompiledEffectInstance()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-blend-unconfirmed", "Test Blend Unconfirmed", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "SingleStrand", LookLayer.RoleSubject, null, "Bright", "Beats", 0.35)]);

        var ir = LookCompiler.Compile(look, section, layout, settings);

        var effect = Assert.Single(ir.Effects);
        Assert.Equal("Normal", effect.BlendMode);
    }

    [Fact]
    public void Compile_ModeB_TwinkleWithNoTimingTrackInput_EmitsOneDiscreteEffectPerBar()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-discrete", "Test Discrete", "test", 0.5, "chase", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.25)]);

        var ir = LookCompiler.Compile(look, section, layout, settings);

        Assert.Equal(5, ir.Effects.Count); // one per bar, Mode B.
        Assert.All(ir.Effects, e => Assert.True(e.EndTicks - e.StartTicks <= barTicks));
    }

    [Fact]
    public void Compile_ModeB_LowTargetChangeRate_ThinsMarksIntoFewerLongerEffects()
    {
        // Build-plan Phase 12 §3a: a section-level target change-rate throttles Mode B's own inter-mark
        // cadence by merging consecutive marks, never by speeding one up -- an unconstrained section (the
        // default) compiles exactly as before, while a low-target-rate section compiles into measurably
        // fewer, longer-spanning effects for the identical Look/section bar grid.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = TicksPerSecond; // one bar per second, so a natural rate of 1 mark/sec.
        var barGrid = BuildBarGrid(10, barTicks);
        var settings = BuildSettings();

        var look = new Look(
            "test-thinned", "Test Thinned", "test", 0.5, "chase", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Bars", 0.25)]);

        var unthrottled = BuildSection(barGrid, barTicks, 10, 0.5);
        var throttled = BuildSection(barGrid, barTicks, 10, 0.5, targetChangeRatePerSecond: 0.2);

        var unthrottledIr = LookCompiler.Compile(look, unthrottled, layout, settings);
        var throttledIr = LookCompiler.Compile(look, throttled, layout, settings);

        Assert.Equal(10, unthrottledIr.Effects.Count); // one per bar, unconstrained -- same as the existing Mode B test above.
        Assert.Equal(2, throttledIr.Effects.Count); // natural rate 1/sec -> keepEvery = ceil(1/0.2) = 5 -> 10 marks merge into 2 windows.
        Assert.All(throttledIr.Effects, e => Assert.True(e.EndTicks - e.StartTicks > barTicks)); // each merged effect spans several bars, not just one.
    }

    [Fact]
    public void Compile_PropDelays_StaggerGroupMembersByMassTier()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-delay", "Test Delay", "test", 0.5, "hold", "warm-gold", "group:MiniStars",
            [new LookLayer(
                1, "group:MiniStars", "Color Wash", LookLayer.RoleSubject, null, "Normal", null, 0.9,
                Curves: new Dictionary<string, LookCurveBinding> { ["C_SLIDER_Brightness"] = new("Timing Track Fade Proportional", "Beats") })],
            PropDelays: new Dictionary<string, string>
            {
                [PropDelayResolver.TierMass] = "+0ms",
                [PropDelayResolver.TierMid] = "+60ms",
                [PropDelayResolver.TierAccent] = "+120ms",
            });

        var ir = LookCompiler.Compile(look, section, layout, settings);

        Assert.Equal(3, ir.Effects.Count); // one per model, Mode A (curve-bound) -- not one per bar.
        var startTimes = ir.Effects.Select(e => e.StartTicks).Distinct().ToList();
        Assert.Equal(3, startTimes.Count); // three distinct pixel weights -> three distinct tiers -> three distinct stagger offsets.
    }

    [Fact]
    public void Compile_GroupReferenceWithNoPropDelays_TargetsTheGroupItselfOnce_NotEachMember()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, barTicks, 5, 0.5);
        var settings = BuildSettings();

        // Same "group:MiniStars" reference as Compile_PropDelays_StaggerGroupMembersByMassTier, but with
        // no PropDelays declared -- real xLights represents a group-targeted effect identically to a
        // model-targeted one (XsqExporter/PropGroupResolver's own doc comments, verified against 20 real
        // .xsq files), so with no genuine per-member stagger requested, every compiled effect should
        // target the real group "MiniStars" directly, never duplicated once per member model (MiniStar1/
        // 2/3). Color Wash + "Beats" selects Mode B (one discrete effect per mark, confirmed via a real
        // run), so this asserts the group-target invariant across however many effects that produces,
        // not a specific count -- the count is EmissionStrategySelector's own concern, not this fix's.
        var look = new Look(
            "test-group-collapse", "Test Group Collapse", "test", 0.5, "hold", "warm-gold", "group:MiniStars",
            [new LookLayer(1, "group:MiniStars", "Color Wash", LookLayer.RoleSubject, null, "Normal", "Beats", 0.9)]);

        var ir = LookCompiler.Compile(look, section, layout, settings);

        Assert.NotEmpty(ir.Effects);
        Assert.All(ir.Effects, e => Assert.Equal("MiniStars", e.Target.Value));
    }

    [Fact]
    public void Compile_InvalidParameterOverride_ThrowsChoreographyCompilationException()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        var barGrid = BuildBarGrid(5, 2 * TicksPerSecond);
        var section = BuildSection(barGrid, 2 * TicksPerSecond, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-invalid", "Test Invalid", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "Bars", LookLayer.RoleSubject, new Dictionary<string, string> { ["E_SLIDER_Bars_BarCount"] = "9999" }, "Normal", "Bars", 0.9)]);

        Assert.Throws<ChoreographyCompilationException>(() => LookCompiler.Compile(look, section, layout, settings));
    }

    [Fact]
    public void Compile_ChaseMotionUnderVocalActivity_BiasesTowardHold_HigherDutyAndSteadierCadence()
    {
        // Build-plan Phase 8 task 2 / architecture §10: "steady single-colour full-house hold under
        // vocals, frequency-reactive movement under instrumental breaks." Same Look, same low (chase-like)
        // declared duty on a discrete (no timing-track-binding) effect -- only VocalActive differs.
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var settings = BuildSettings();

        var look = new Look(
            "test-chase", "Test Chase", "test", 0.5, "chase", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "Twinkle", LookLayer.RoleSubject, null, "Normal", "Beats", 0.25)]);

        var instrumentalSection = BuildSection(barGrid, barTicks, 5, 0.5, vocalActive: false);
        var vocalSection = BuildSection(barGrid, barTicks, 5, 0.5, vocalActive: true);

        var instrumentalIr = LookCompiler.Compile(look, instrumentalSection, layout, settings);
        var vocalIr = LookCompiler.Compile(look, vocalSection, layout, settings);

        // Hold bias floors duty near-full coverage -- each effect should cover far more of its own
        // (steadier, Bars-cadenced) window than the instrumental 25%-duty chase does of its own.
        var instrumentalDuty = instrumentalIr.Effects.Average(e => (e.EndTicks - e.StartTicks) / (double)barTicks);
        var vocalDuty = vocalIr.Effects.Average(e => (e.EndTicks - e.StartTicks) / (double)barTicks);
        Assert.True(vocalDuty > instrumentalDuty, $"vocal-active duty {vocalDuty} should exceed instrumental duty {instrumentalDuty}.");
    }

    [Fact]
    public void Compile_UnresolvableGroupReference_SkipsLayer_RatherThanThrowing()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildLayout();
        var barGrid = BuildBarGrid(5, 2 * TicksPerSecond);
        var section = BuildSection(barGrid, 2 * TicksPerSecond, 5, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-missing-group", "Test Missing Group", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "group:DoesNotExist", "Color Wash", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

        var ir = LookCompiler.Compile(look, section, layout, settings);

        Assert.Empty(ir.Effects);
    }

    private static DisplayGraph BuildArchesLayout() => new(
        "TEST-HASH-ARCHES",
        Models:
        [
            new DisplayModel(new ModelId("Arch1"), "Arch1", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(300, 0, 0)),
            new DisplayModel(new ModelId("Arch2"), "Arch2", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(100, 0, 0)),
            new DisplayModel(new ModelId("Arch3"), "Arch3", "Arches", [], [], NodeCount: 50, Bounds: new DisplayBounds(200, 0, 0)),
        ],
        Groups: [new DisplayModelGroup("Arches",
        [
            new DisplayGroupMember("Arch1", DisplayGroupMemberKind.Model),
            new DisplayGroupMember("Arch2", DisplayGroupMemberKind.Model),
            new DisplayGroupMember("Arch3", DisplayGroupMemberKind.Model),
        ])],
        Warnings: []);

    /// <summary>
    /// User feedback (2026-11-13): a Look declaring <see cref="LookTravel"/> against a group compiles
    /// into one effect per real individual member, each landing on a successive mark of the declared
    /// track in true physical left-to-right order (by real <c>DisplayBounds.X</c>, Arch2=100 &lt;
    /// Arch3=200 &lt; Arch1=300) -- never group declaration order, never a flat ms offset.
    /// </summary>
    [Fact]
    public void Compile_TravelDeclared_AssignsOrderedMembersToSuccessiveMarksInPhysicalOrder()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildArchesLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(3, barTicks);
        var section = BuildSection(barGrid, barTicks, 3, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-travel", "Test Travel", "test", 0.5, "chase", "warm-gold", "group:Arches",
            [new LookLayer(1, "group:Arches", "Twinkle", LookLayer.RoleSubject, null, "Normal", null, 0.9)],
            Travel: new LookTravel(LookTravel.OrderLeftToRight, "Beats"));

        var ir = LookCompiler.Compile(look, section, layout, settings);

        Assert.Equal(3, ir.Effects.Count);
        var byModel = ir.Effects.ToDictionary(e => e.Target);
        Assert.Equal(0, byModel[new ModelId("Arch2")].StartTicks); // leftmost (X=100) gets the first mark.
        Assert.Equal(barTicks, byModel[new ModelId("Arch3")].StartTicks); // middle (X=200) gets the second mark.
        Assert.Equal(2 * barTicks, byModel[new ModelId("Arch1")].StartTicks); // rightmost (X=300) gets the third mark.
    }

    [Fact]
    public void Compile_TravelDeclared_RightToLeft_ReversesPhysicalOrder()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildArchesLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(3, barTicks);
        var section = BuildSection(barGrid, barTicks, 3, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-travel-rtl", "Test Travel RTL", "test", 0.5, "chase", "warm-gold", "group:Arches",
            [new LookLayer(1, "group:Arches", "Twinkle", LookLayer.RoleSubject, null, "Normal", null, 0.9)],
            Travel: new LookTravel(LookTravel.OrderRightToLeft, "Beats"));

        var ir = LookCompiler.Compile(look, section, layout, settings);

        var byModel = ir.Effects.ToDictionary(e => e.Target);
        Assert.Equal(0, byModel[new ModelId("Arch1")].StartTicks); // rightmost (X=300) now fires first.
        Assert.Equal(barTicks, byModel[new ModelId("Arch3")].StartTicks);
        Assert.Equal(2 * barTicks, byModel[new ModelId("Arch2")].StartTicks); // leftmost (X=100) fires last.
    }

    [Fact]
    public void Compile_TravelDeclared_MoreMembersThanMarks_ExtraMembersGetNoEffect()
    {
        if (!RealXLightsFixture.IsAvailable)
        {
            return;
        }

        var layout = BuildArchesLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(2, barTicks); // only 2 marks for 3 members.
        var section = BuildSection(barGrid, barTicks, 2, 0.5);
        var settings = BuildSettings();

        var look = new Look(
            "test-travel-overflow", "Test Travel Overflow", "test", 0.5, "chase", "warm-gold", "group:Arches",
            [new LookLayer(1, "group:Arches", "Twinkle", LookLayer.RoleSubject, null, "Normal", null, 0.9)],
            Travel: new LookTravel(LookTravel.OrderLeftToRight, "Beats"));

        var ir = LookCompiler.Compile(look, section, layout, settings);

        Assert.Equal(2, ir.Effects.Count); // Arch1 (rightmost, 3rd in order) never gets a real mark to land on.
        Assert.DoesNotContain(ir.Effects, e => e.Target == new ModelId("Arch1"));
    }
}
