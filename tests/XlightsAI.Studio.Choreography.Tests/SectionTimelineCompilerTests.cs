using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
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

    internal static LookCompilerSettings BuildPortableSettings()
    {
        var vocabulary = new EffectVocabularyCatalog(
            new[] { "On", "Twinkle", "Bars", "Color Wash", "Shockwave", "SingleStrand" }
                .ToDictionary(n => n, n => new EffectVocabulary(n, "E_", []), StringComparer.Ordinal),
            new EffectVocabulary("Buffer", "B_", []), new EffectVocabulary("Blending", "T_", []), new EffectVocabulary("Color", "C_", []));
        var recipes = new EffectRecipeCatalog();
        GeneratedEffectRecipeFactory.RegisterAll(vocabulary, recipes);
        return new LookCompilerSettings(vocabulary, recipes, new EffectRecipeRegistry(recipes),
            new Dictionary<string, ColorPaletteDefinition> { ["warm-gold"] = PaletteBuilder.Build(["#FFC880"]) });
    }

    private static Look BuildBaseLook() => new(
        "base", "Base", "Test", 0.5, "hold", "warm-gold", "Tree",
        [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

    [Theory]
    [InlineData(IntensityTier.Off)]
    [InlineData(IntensityTier.Dim)]
    [InlineData(IntensityTier.Bright)]
    [InlineData(IntensityTier.Full)]
    public async Task Compile_HoldThroughBeats_ExportsFullDurationUnlessOff(string intensity)
    {
        var timeline = new SectionTimeline(Guid.NewGuid(), "base", Cues:
        [
            new SectionCue("section-start", "MiniStar1", "On", intensity, LookLayer.RoleSubject, Kind: CueKind.Hold),
        ]);
        var xsq = await ExportAsync(timeline);
        var placements = xsq.ElementEffects.Where(x => x.Name == "MiniStar1").SelectMany(x => x.Effects).ToArray();
        if (intensity == IntensityTier.Off)
        {
            Assert.Empty(placements);
        }
        else
        {
            var hold = Assert.Single(placements);
            Assert.Equal((0, 10000), (hold.StartTimeMs, hold.EndTimeMs));
        }
    }

    [Theory]
    [InlineData(IntensityTier.Full, "MiniStar1,MiniStar2")]
    [InlineData(IntensityTier.Bright, "MiniStar2")]
    public async Task Compile_BackToBackAccents_DimOnlyTheRightUnheldModels(string intensity, string dimTargets)
    {
        var timeline = new SectionTimeline(Guid.NewGuid(), "base", Cues:
        [
            new SectionCue("section-start", "Tree", "On", IntensityTier.Full, LookLayer.RoleSubject, Kind: CueKind.Hold),
            new SectionCue("transient-1", "MiniStar1", "On", IntensityTier.Bright, LookLayer.RoleSupport, "transient-2"),
            new SectionCue("transient-2", "MiniStar2", "On", intensity, LookLayer.RoleSupport, "transient-3"),
        ]);
        var xsq = await ExportAsync(timeline);
        var targets = xsq.ElementEffects.Where(e => e.Effects.Any(p => p.Name == "Twinkle" && p.EndTimeMs == 4000))
            .Select(e => e.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(dimTargets.Split(','), targets);
        var hold = Assert.Single(Assert.Single(xsq.ElementEffects, e => e.Name == "Tree").Effects);
        Assert.Equal((0, 10000), (hold.StartTimeMs, hold.EndTimeMs));
    }

    [Fact]
    public async Task Compile_ExplicitTakeover_EndsHoldAndRunsThroughItsOwnEndpoint()
    {
        var timeline = new SectionTimeline(Guid.NewGuid(), "base", Cues:
        [
            new SectionCue("section-start", "MiniStar1", "On", IntensityTier.Full, LookLayer.RoleSubject, Kind: CueKind.Hold),
            new SectionCue("transient-2", "MiniStar1", "Twinkle", IntensityTier.Dim, LookLayer.RoleSubject, "transient-3", Kind: CueKind.Takeover),
        ]);
        var xsq = await ExportAsync(timeline);
        var effects = Assert.Single(xsq.ElementEffects, e => e.Name == "MiniStar1").Effects;
        Assert.Equal(new[] { ("On", 0L, 4000L), ("Twinkle", 4000L, 8000L) },
            effects.Select(e => (e.Name, e.StartTimeMs, e.EndTimeMs)));
    }

    [Fact]
    public async Task Compile_UsesTheOfferedSalientNoteIdInsteadOfReindexingTheFullList()
    {
        var shortNote = new NoteEvent(Guid.NewGuid(), TicksPerSecond, TicksPerSecond + 1000, 60, .2, .8, false);
        var salient = new NoteEvent(Guid.NewGuid(), 3 * TicksPerSecond, 7 * TicksPerSecond, 64, .9, .8, false);
        var section = BuildSection(BuildBarGrid(5, 2 * TicksPerSecond), 5, 2 * TicksPerSecond);
        var catalog = SectionTimingCatalog.Build(section, salientNotes: [salient], allNotes: [shortNote, salient]);
        var timeline = new SectionTimeline(Guid.NewGuid(), "base", Cues:
        [
            new SectionCue("vocalnote-1", "MiniStar1", "On", IntensityTier.Bright, LookLayer.RoleSubject,
                Assert.Single(catalog.Sustains).EndId, Kind: CueKind.Hold),
        ]);
        var xsq = await ExportAsync(timeline, catalog, [shortNote, salient]);
        var hold = Assert.Single(Assert.Single(xsq.ElementEffects, e => e.Name == "MiniStar1").Effects);
        Assert.Equal((3000, 7000), (hold.StartTimeMs, hold.EndTimeMs));
    }

    [Fact]
    public void BuildTimingCatalog_WordEstimateCannotMoveASeamSpanningNote()
    {
        var section = BuildSection(BuildBarGrid(5, 2 * TicksPerSecond), 5, 2 * TicksPerSecond);
        var note = new NoteEvent(Guid.NewGuid(), 3 * TicksPerSecond, 7 * TicksPerSecond, 64, .9, .8, false);
        var words = new[]
        {
            new TranscribedWord("wrong", 6 * TicksPerSecond, 8 * TicksPerSecond, .99f),
            new TranscribedWord("matching", 2 * TicksPerSecond, 6 * TicksPerSecond, .65f),
        };
        var before = SectionTimingCatalog.Build(section with { Window = new TimeWindow(0, 5 * TicksPerSecond) },
            allNotes: [note], words: words);
        var after = SectionTimingCatalog.Build(section with { Window = new TimeWindow(5 * TicksPerSecond, 10 * TicksPerSecond) },
            allNotes: [note], words: words);
        var span = Assert.Single(before.Sustains);
        Assert.Equal("matching", span.EstimatedWord);
        Assert.Equal(note.StartTicks, before.Marks[span.StartId]);
        Assert.Equal(note.EndTicks, before.Marks[span.EndId]);
        Assert.Equal(note.StartTicks, after.Marks[span.StartId]);
        Assert.Equal(note.EndTicks, after.Marks[span.EndId]);
    }

    private static async Task<XsqStructuralDocument> ExportAsync(
        SectionTimeline timeline, SectionTimingCatalog? catalog = null, IReadOnlyList<NoteEvent>? allNotes = null)
    {
        var layout = BuildLayout();
        var settings = BuildPortableSettings();
        var section = BuildSection(BuildBarGrid(5, 2 * TicksPerSecond), 5, 2 * TicksPerSecond);
        var now = DateTimeOffset.UtcNow;
        var brightness = new EffectVisualCharacterCatalog(now,
        [
            new EffectVisualCharacter("On", "High", "Bright", now, BrightnessTier: BrightnessTier.Bright),
            new EffectVisualCharacter("Twinkle", "High", "Dim", now, BrightnessTier: BrightnessTier.Dim),
        ]);
        var ir = SectionTimelineCompiler.Compile(BuildBaseLook(), timeline, section, layout, settings,
            transientHits: [new(2 * TicksPerSecond, .9f), new(4 * TicksPerSecond, .9f), new(8 * TicksPerSecond, .9f)],
            vocalNotes: allNotes, brightnessCatalog: brightness, catalog: catalog);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xsq");
        try
        {
            await new XsqExporter(settings.RecipeCatalog, settings.Vocabulary)
                .ExportNewAsync(layout, ir.ToSequenceDocument(), path, "2026.03", CancellationToken.None);
            return new XsqStructuralReader().Read(path);
        }
        finally
        {
            File.Delete(path);
        }
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

    /// <summary>
    /// Build-plan Phase 30 step 30.2 ("hit-motion-compound") gate, per the acceptance definition's own
    /// wording: "a compound cue exports as exactly two layers over its cue's resolved span (base hit
    /// effect + movement overlay), both within frame tolerance (±1 frame) of the cue's declared bounds,
    /// and the movement layer's value-curve or effect parameters differ from the pipeline's flat/static
    /// default for that effect type." Uses the real xLights install's own vocabulary (Shockwave's real
    /// Shockwave_End_Radius property) -- never a portable/synthetic one -- so this exercises the real
    /// confirmed shape, not a stand-in. Also confirms the compound reaches a real exported XSQ, not just
    /// the compiled IR.
    /// </summary>
    [Fact]
    public async Task Compile_CompoundAccentCue_ExportsExactlyTwoLayersWithinFrameToleranceAndARealRampCurve()
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
            "test-compound", "Test Compound", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

        var transient = new TransientHit(2 * TicksPerSecond, 0.9f);
        var timeline = new SectionTimeline(Guid.NewGuid(), "test-compound", Cues:
        [
            new SectionCue("transient-1", "MiniStar1", "On", IntensityTier.Full, LookLayer.RoleSubject, Compound: true),
        ]);

        var ir = SectionTimelineCompiler.Compile(look, timeline, section, layout, settings, transientHits: [transient]);

        var placements = ir.Effects.Where(e => e.Target == new ModelId("MiniStar1")).ToArray();
        Assert.Equal(2, placements.Length);

        var baseHit = Assert.Single(placements, e => e.Layer > 0);
        var movement = Assert.Single(placements, e => e.Layer < 0);
        Assert.Equal(-baseHit.Layer, movement.Layer); // a distinct IR layer, never colliding with the base hit's own.

        const long frameTolerance = 500_000; // 50ms real xLights frame grid, expressed in 100ns ticks.
        Assert.InRange(Math.Abs(movement.StartTicks - baseHit.StartTicks), 0, frameTolerance);
        Assert.InRange(Math.Abs(movement.EndTicks - baseHit.EndTicks), 0, frameTolerance);

        var curve = Assert.Single(movement.ValueCurvesOrEmpty);
        Assert.Equal(HitMotionCompound.MovementValueCurveParameterKey, curve.ParameterKey);
        Assert.Equal(HitMotionCompound.MovementCurveType, curve.CurveType);
        Assert.NotEqual(curve.P1, curve.P2); // a real ramp -- never the effect's own flat/static default.

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xsq");
        try
        {
            await new XsqExporter(settings.RecipeCatalog, settings.Vocabulary)
                .ExportNewAsync(layout, ir.ToSequenceDocument(), path, "2026.03", CancellationToken.None);
            var xsq = new XsqStructuralReader().Read(path);

            var exported = Assert.Single(xsq.ElementEffects, e => e.Name == "MiniStar1").Effects;
            Assert.Equal(2, exported.Count);
            Assert.Contains(exported, e => e.Name == "On");
            Assert.Contains(exported, e => e.Name == "Shockwave");

            var exportedStart = exported[0].StartTimeMs;
            var exportedEnd = exported[0].EndTimeMs;
            foreach (var effect in exported)
            {
                Assert.InRange(Math.Abs(effect.StartTimeMs - exportedStart), 0, xsq.FrameIntervalMs);
                Assert.InRange(Math.Abs(effect.EndTimeMs - exportedEnd), 0, xsq.FrameIntervalMs);
            }

            Assert.Contains(xsq.EffectDb, s => s.Contains("E_VALUECURVE_Shockwave_End_Radius", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Build-plan Phase 30 step 30.2: opt-in, per cue -- a cue that does NOT declare <see cref="SectionCue.Compound"/>
    /// compiles to exactly one effect on its target, same as every pre-existing accent cue, never a
    /// blanket second layer.
    /// </summary>
    [Fact]
    public void Compile_NonCompoundAccentCue_CompilesToExactlyOneLayer()
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
            "test-noncompound", "Test NonCompound", "test", 0.5, "hold", "warm-gold", "Tree",
            [new LookLayer(1, "Tree", "On", LookLayer.RoleSubject, null, "Normal", "Bars", 0.9)]);

        var transient = new TransientHit(2 * TicksPerSecond, 0.9f);
        var timeline = new SectionTimeline(Guid.NewGuid(), "test-noncompound", Cues:
        [
            new SectionCue("transient-1", "MiniStar1", "On", IntensityTier.Full, LookLayer.RoleSubject),
        ]);

        var ir = SectionTimelineCompiler.Compile(look, timeline, section, layout, settings, transientHits: [transient]);

        Assert.Single(ir.Effects, e => e.Target == new ModelId("MiniStar1"));
    }

    /// <summary>
    /// Build-plan Phase 30 step 30.2's own gate: "A section using compounds must still pass existing
    /// contrast/hold/accent validation unchanged." Builds the same hold+accent shape
    /// <see cref="SectionTimelineValidatorTests"/> already exercises, but with the accent cue declaring
    /// <see cref="SectionCue.Compound"/> -- the real <see cref="SectionTimelineValidator"/> must report
    /// the identical (empty) result either way.
    /// </summary>
    [Fact]
    public void Validate_HoldPlusCompoundAccent_PassesTheSameAsHoldPlusPlainAccent()
    {
        var layout = BuildLayout();
        const long barTicks = 2 * TicksPerSecond;
        var barGrid = BuildBarGrid(5, barTicks);
        var section = BuildSection(barGrid, 5, barTicks);
        var settings = BuildPortableSettings();
        var lookLibrary = new LookLibrary([BuildBaseLook()]);

        SectionTimeline BuildTimeline(bool compound) => new(Guid.NewGuid(), "base", Cues:
        [
            new SectionCue("section-start", "Tree", "On", IntensityTier.Full, LookLayer.RoleSubject, Kind: CueKind.Hold),
            new SectionCue("transient-1", "MiniStar1", "On", IntensityTier.Bright, LookLayer.RoleSupport, Compound: compound),
            new SectionCue("transient-2", "MiniStar2", "On", IntensityTier.Bright, LookLayer.RoleSupport),
        ]);

        var transientHits = new[] { new TransientHit(2 * TicksPerSecond, .9f), new TransientHit(3 * TicksPerSecond, .9f) };
        var plainErrors = SectionTimelineValidator.Validate(
            BuildTimeline(compound: false), lookLibrary, layout, section, transientHits: transientHits, vocabulary: settings.Vocabulary);
        var compoundErrors = SectionTimelineValidator.Validate(
            BuildTimeline(compound: true), lookLibrary, layout, section, transientHits: transientHits, vocabulary: settings.Vocabulary);

        Assert.Empty(plainErrors);
        Assert.Empty(compoundErrors);
    }
}
