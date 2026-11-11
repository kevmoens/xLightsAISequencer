using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §2 (Phase A): "for every real effect name in the xLights vocabulary, produce a tiny,
/// isolated synthetic sequence that exercises it on its own, with nothing else to confuse a later visual
/// read." Builds one <see cref="Look"/>/<see cref="LookLayer"/> per effect/resolution-tier combination
/// against a small synthetic fixture layout and compiles it via the same <see cref="LookCompiler"/> real
/// generation already uses -- never a parallel compile path -- recording whether that compile itself
/// succeeded (a real, disclosed failure, not a silent skip) before any rendering is ever attempted.
///
/// Deliberately stops at compilation: Phase B (export + real headless render) and beyond are separate,
/// not-yet-implemented phases per the build plan's own "one phase at a time, confirm before the next"
/// discipline.
/// </summary>
public static class EffectFixturePlanner
{
    /// <summary>
    /// Phase 18 §2's own disclosed first-pass simplification: "a short, fixed duration per effect... needs a
    /// real measured decision per effect family... disclosed as an open design question to resolve with a few
    /// real effects before committing to one default." Six seconds (three 2-second bars, the same bar length
    /// <c>LookCompilerTests</c> already uses) is a reasonable starting guess -- long enough to show at least
    /// one or two cycles of most periodic effects -- not a validated-against-real-footage constant; expected
    /// to be revisited once Phase B/C produce real renders to actually look at.
    /// </summary>
    public const double DefaultFixtureDurationSeconds = 6.0;

    private const long TicksPerSecond = 10_000_000L;
    private const long BarTicks = 2 * TicksPerSecond;

    /// <summary>
    /// A low-resolution model -- well under <see cref="ChoreographyLegalityValidator.LowResolutionNodeCountThreshold"/>
    /// (60), the same "well under" margin <c>ChoreographyLegalityValidatorTests</c>' own 20-pixel TinyStar
    /// fixture uses -- standing in for a short string/outline prop.
    /// </summary>
    public static readonly EffectFixtureModel LowResolutionModel = new("FixtureLowResString", "Star", NodeCount: 20);

    /// <summary>A high-resolution model -- the same 900-pixel Tree shape every existing Choreography test fixture already uses as its Tree/Matrix stand-in.</summary>
    public static readonly EffectFixtureModel HighResolutionModel = new("FixtureHighResMatrix", "Tree", NodeCount: 900);

    /// <summary>
    /// Real xLights "Vert Matrix" shape for <see cref="HighResolutionModel"/>: <see cref="HighResolutionMatrixColumns"/>
    /// strings × <see cref="HighResolutionMatrixRows"/> pixels/string = 900 nodes (<see cref="ModelGeometryCalculator"/>'s
    /// own cross-checked <c>parm1 * parm2</c> formula). A real 2D shape, not a 1-row Single Line --
    /// discovered necessary once several effects' own real metadata (Shape/Kaleidoscope/Circles/Warp/Lines/
    /// Morph/Life/Fireworks: all genuinely 2D-plane effects in their own declared properties, e.g. Morph's
    /// own <c>Morph_Start_Y1</c>/<c>Morph_End_Y1</c> spanning a real 0-100% vertical range) rendered
    /// completely dark on a buffer verified to be exactly 1 pixel tall.
    /// </summary>
    public const int HighResolutionMatrixColumns = 30;

    public const int HighResolutionMatrixRows = 30;

    private const string PaletteRef = "fixture-neutral";

    /// <summary>
    /// Real xLights documentation finding (manual.xlights.org, Music effect): "Select at least two colors"
    /// is required for that effect to produce any visible output at all -- a single-color palette (this
    /// fixture's own original choice) left it permanently dark regardless of real audio being present. Two
    /// real, distinct colors here costs nothing for every other effect (they still see palette index 1 as
    /// their primary/only referenced color where that's all they use) while unblocking Music.
    /// </summary>
    private static readonly string[] PaletteColorsHex = ["#FFC880", "#4080FF"];

    /// <summary>
    /// Enumerates every real effect name in <paramref name="vocabulary"/> (no invented effect list), compiles
    /// one fixture <see cref="Look"/> per effect/resolution-tier combination, and returns the full manifest --
    /// every entry's own compile outcome, success or failure, is recorded, never silently dropped.
    /// </summary>
    public static EffectFixtureManifest Build(
        EffectVocabularyCatalog vocabulary, EffectRecipeCatalog recipes, string effectMetadataDirectory,
        double? durationSecondsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectMetadataDirectory);

        var durationSeconds = durationSecondsOverride ?? DefaultFixtureDurationSeconds;
        var registry = new EffectRecipeRegistry(recipes);
        var layout = BuildFixtureLayout();
        var settings = new LookCompilerSettings(
            vocabulary, recipes, registry,
            new Dictionary<string, ColorPaletteDefinition>(StringComparer.Ordinal) { [PaletteRef] = PaletteBuilder.Build(PaletteColorsHex) });
        var section = BuildSection(durationSeconds);

        var plans = new List<EffectFixturePlan>();
        foreach (var effectName in vocabulary.Effects.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            plans.Add(BuildPlan(effectName, EffectFixtureResolutionTier.Low, LowResolutionModel, durationSeconds, layout, section, settings));
            plans.Add(BuildPlan(effectName, EffectFixtureResolutionTier.High, HighResolutionModel, durationSeconds, layout, section, settings));
        }

        return new EffectFixtureManifest(DateTimeOffset.UtcNow, effectMetadataDirectory, plans);
    }

    /// <summary>
    /// Build-plan Phase 18 §3 (Phase B): re-derives the exact <see cref="ChoreographyIR"/> an already-known-
    /// successful <paramref name="plan"/> compiled to -- the same deterministic <see cref="BuildLook"/>/
    /// <see cref="BuildSection"/>/<see cref="LookCompiler.Compile"/> steps <see cref="Build"/> itself ran per
    /// fixture -- so Phase B's own renderer can export/render it without <see cref="EffectFixtureManifest"/>'s
    /// own persisted schema having to carry a non-JSON-stable compiled IR alongside it (Phase A's manifest
    /// shape stays exactly as already persisted).
    /// </summary>
    public static ChoreographyIR CompileLook(EffectFixturePlan plan, EffectVocabularyCatalog vocabulary, EffectRecipeCatalog recipes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(recipes);

        var registry = new EffectRecipeRegistry(recipes);
        var layout = BuildFixtureLayout();
        var settings = new LookCompilerSettings(
            vocabulary, recipes, registry,
            new Dictionary<string, ColorPaletteDefinition>(StringComparer.Ordinal) { [PaletteRef] = PaletteBuilder.Build(PaletteColorsHex) });
        var section = BuildSection(plan.DurationSeconds);
        var look = BuildLook(plan.EffectName, plan.Model);
        return LookCompiler.Compile(look, section, layout, settings);
    }

    private static EffectFixturePlan BuildPlan(
        string effectName, EffectFixtureResolutionTier tier, EffectFixtureModel model, double durationSeconds,
        DisplayGraph layout, SectionMusicContext section, LookCompilerSettings settings)
    {
        try
        {
            var look = BuildLook(effectName, model);
            LookCompiler.Compile(look, section, layout, settings);
            return new EffectFixturePlan(effectName, tier, model, durationSeconds, CompiledSuccessfully: true, CompileFailureReason: null);
        }
        catch (Exception ex)
        {
            // Never let one effect's own compile failure (an unverified parameter, an unresolvable
            // timing-track binding, ...) take down the whole enumeration -- recorded as an explicit,
            // disclosed failure instead, per this phase's own "never a silently skipped entry" discipline.
            return new EffectFixturePlan(effectName, tier, model, durationSeconds, CompiledSuccessfully: false, CompileFailureReason: ex.Message);
        }
    }

    private static Look BuildLook(string effectName, EffectFixtureModel model) => new(
        Id: $"fixture-{effectName.ToLowerInvariant()}-{model.XlightsName.ToLowerInvariant()}",
        Name: $"Fixture: {effectName} on {model.XlightsName}",
        Intent: "effect-visual-characterization-fixture",
        EnergyCeiling: 1.0,
        Motion: "hold",
        PaletteRef: PaletteRef,
        Subject: model.XlightsName,
        Layers: IsCanvasModeEffect(effectName)
            ?
            [
                // Real finding (manual.xlights.org + direct corpus verification): Kaleidoscope/Warp/Adjust
                // are canvas-mode effects that "by itself do nothing" -- they sample/modify a real layer
                // beneath them. A plain "On" base layer gives them real content to operate on; the
                // T_CHECKBOX_Canvas/T_LayersSelected override below (now real, vocabulary-registered
                // settings -- see EffectVocabularyBuilder.BuildLegacyBlendingProperties) is exactly the
                // real serialization a real exported sequence uses to enable this. Confirmed via real
                // render: all three now show real, non-empty content (was 0/240 lit frames, now 240/240).
                new LookLayer(Z: 1, Props: model.XlightsName, Effect: "On", Role: LookLayer.RoleSupport, Params: null, Blend: "Normal", Timescale: "Beats", Duty: 1.0),
                new LookLayer(
                    Z: 2, Props: model.XlightsName, Effect: effectName, Role: LookLayer.RoleSubject,
                    Params: BuildParamOverrides(effectName, model), Blend: "Normal", Timescale: "Beats", Duty: 1.0),
            ]
            : [new LookLayer(
                Z: 1, Props: model.XlightsName, Effect: effectName, Role: LookLayer.RoleSubject,
                Params: BuildParamOverrides(effectName, model), Blend: "Normal", Timescale: "Beats", Duty: 1.0)]);

    /// <summary>
    /// Real xLights effects confirmed (manual.xlights.org, cross-checked against the real sequence
    /// corpus) to be canvas-mode: they sample/modify a real effect layer beneath them and show nothing on
    /// their own. Each needs a real base layer plus the real Canvas/LayersSelected override below --
    /// Duplicate has the same underlying problem (it mirrors another model's own active effect) but needs
    /// a concurrent effect on a DIFFERENT model, not a second layer on this same one, so it is handled
    /// separately via <see cref="BuildParamOverrides"/> alone, not this set.
    /// </summary>
    private static readonly HashSet<string> CanvasModeEffectNames = new(StringComparer.Ordinal) { "Kaleidoscope", "Warp", "Adjust" };

    private static bool IsCanvasModeEffect(string effectName) => CanvasModeEffectNames.Contains(effectName);

    /// <summary>
    /// Two effects' own real metadata default to a blank value that produces nothing visible by design,
    /// confirmed by direct real render -- a real, non-invented alternative value exists for each, so this
    /// fixture gets that instead of staying permanently dark on its own default. Deliberately scoped to
    /// exactly these two effects, not a general parameter sweep (Phase 18 §8's own deferred "Parameter-space
    /// coverage" extension): every other effect still renders on its own real, unmodified default.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? BuildParamOverrides(string effectName, EffectFixtureModel model)
    {
        if (IsCanvasModeEffect(effectName))
        {
            // Real serialization confirmed directly against this machine's own real sequence corpus
            // (C:\xLights\*.xsq): T_CHECKBOX_Canvas=1 paired with T_LayersSelected=0 (0-based index of
            // the real base layer immediately below, added above).
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["T_CHECKBOX_Canvas"] = "1",
                ["T_LayersSelected"] = "0",
            };
        }

        if (string.Equals(effectName, "Text", StringComparison.Ordinal))
        {
            // Text's own real metadata default is a blank string -- a real, meaningful literal (its own
            // effect name) stands in instead of leaving this fixture permanently dark. Real, fully-qualified
            // SettingKey confirmed against the generated vocabulary (EffectVocabularyBuilder.BuildSettingKey's
            // own "text" controlType -> TEXTCTRL prefix): E_TEXTCTRL_Text, not the bare property id.
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["E_TEXTCTRL_Text"] = "Text" };
        }

        if (string.Equals(effectName, "Duplicate", StringComparison.Ordinal))
        {
            // Duplicate's own real metadata default targets no model at all -- this synthetic layout's
            // OTHER tier's own real model is a genuine, already-known target to duplicate from. Real,
            // fully-qualified SettingKey confirmed against the generated vocabulary ("choice" controlType ->
            // CHOICE prefix): E_CHOICE_Duplicate_Model, not the bare property id.
            var otherModel = string.Equals(model.XlightsName, LowResolutionModel.XlightsName, StringComparison.Ordinal)
                ? HighResolutionModel.XlightsName
                : LowResolutionModel.XlightsName;
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["E_CHOICE_Duplicate_Model"] = otherModel };
        }

        // Tried and real-render-confirmed NOT to help (reverted rather than left as dead/misleading code).
        // Shape: E_SLIDER_Shape_StartSize=40 (still 0/240 -- its own tiny 1% default was not the real
        // blocker; real corpus usage confirms StartSize=1-2 is itself the normal real-world value, so the
        // actual blocker is something else entirely, not this parameter).
        // SingleStrand: E_CHOICE_SingleStrand_FX="None" alone, and -- after a real corpus finding that
        // E_NOTEBOOK_SSEFFECT_TYPE (a real, undocumented tab-selector key, now added to the vocabulary via
        // BuildSingleStrandEffectTypeOverride) is set on every real instance -- also tried
        // E_NOTEBOOK_SSEFFECT_TYPE="Chase" plus the real corpus's own E_CHOICE_SingleStrand_TimingTrack="Beats".
        // Still 0/240 every time.
        // Fireworks: E_SLIDER_Fireworks_XLocation/YLocation=50, then (after a real corpus finding that
        // Gravity=0 and BufferStyle="Per Preview" are both commonly real-authored) E_CHECKBOX_Fireworks_Gravity=0
        // plus B_CHOICE_BufferStyle="Per Preview". Still 0/240 every time.
        // All three remain genuinely unexplained after real documentation AND real corpus-usage cross-checks.

        return null;
    }

    private static SectionMusicContext BuildSection(double durationSeconds)
    {
        var durationTicks = (long)(durationSeconds * TicksPerSecond);
        var barCount = Math.Max(1, (int)Math.Ceiling(durationTicks / (double)BarTicks));

        var beats = Enumerable.Range(0, barCount)
            .Select(bar => new BeatMarker(Guid.NewGuid(), bar * BarTicks, BeatWithinBar: 0, BarIndex: bar, Confidence: 1.0, UserCorrected: false))
            .ToArray();
        var beatGrid = new BeatGrid(
            Guid.NewGuid(), new SongId(Guid.NewGuid()), "fixture-hash", Guid.NewGuid(), Guid.NewGuid(),
            "fixture/1", new BeatGridSettings(BeatsPerBar: 4, AlignmentToleranceTicks: 1000),
            SelectedTempo: null, beats, Candidates: [], CorrectionVersion: 0);
        var barGrid = BarGrid.FromBeatGrid(beatGrid, barCount * BarTicks);

        var marks = barGrid.Bars.Select(b => new TimingMark("", b.Window.StartTicks, b.Window.EndTicks)).ToList();
        var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
        {
            ["Bars"] = new TimingTrack("Bars", marks),
            ["Beats"] = new TimingTrack("Beats", marks),
        };

        var window = new TimeWindow(0, barCount * BarTicks);
        return new SectionMusicContext(window, SectionKind.Verse, EnergyCeiling: 1.0, barGrid, tracks, VocalActive: false);
    }

    /// <summary>
    /// Public so Phase B's own renderer can reuse exactly the same two-model synthetic layout for channel
    /// projection that Phase A already compiled every fixture against -- never a second, possibly-divergent
    /// layout definition.
    /// </summary>
    /// <summary>
    /// Matches <see cref="EffectFixtureSyntheticShow"/>'s own declared <c>StringType="RGB Nodes"</c> --
    /// <see cref="Rendering.RenderedFrameProjector"/> silently skips (via <see cref="Rendering.RenderedFrameProjectionResult.SkippedModels"/>)
    /// any model whose <see cref="DisplayModel.StringType"/> is not exactly this value, and
    /// <see cref="DisplayModel.StringType"/> defaults to <c>""</c> when not supplied. Omitting this
    /// previously meant every single Phase B fixture projected zero illuminated points regardless of
    /// which effect it rendered -- discovered only once Phase C's own real-GIF-sanity-check surfaced that
    /// every produced video was a blank black rectangle.
    /// </summary>
    private const string RgbNodesStringType = "RGB Nodes";

    /// <summary>
    /// Both fixture models are real "Single Line" props (matching <see cref="EffectFixtureSyntheticShow"/>'s
    /// own declared <c>DisplayAs="Single Line"</c>), so a real second line endpoint
    /// (<see cref="DisplayBounds.EndX"/>/<see cref="DisplayBounds.EndY"/> -- the same field xLights itself
    /// uses for this exact model family) is genuine, resolvable geometry here, not a guess: it lets
    /// <see cref="Rendering.RenderedFrameProjector"/> spread each fixture's own nodes across real,
    /// distinct positions (left-to-right along this line) instead of collapsing every node onto one point,
    /// so a chase/wipe effect's own real spatial motion is actually visible in Phase C's video, not just its
    /// per-node colour-over-time.
    /// </summary>
    private static DisplayBounds BuildLineBounds() => new(X: 0.0, Y: 0.5, Z: 0.0, EndX: 1.0, EndY: 0.5);

    public static DisplayGraph BuildFixtureLayout() => new(
        "fixture-layout",
        Models:
        [
            new DisplayModel(new ModelId(LowResolutionModel.XlightsName), LowResolutionModel.XlightsName, LowResolutionModel.Kind, [], [], Category: ModelCategory.SingleLine, StringType: RgbNodesStringType, NodeCount: LowResolutionModel.NodeCount, Bounds: BuildLineBounds()),
            new DisplayModel(
                new ModelId(HighResolutionModel.XlightsName), HighResolutionModel.XlightsName, HighResolutionModel.Kind, [], [],
                Category: ModelCategory.Matrix, StringType: RgbNodesStringType, Parm1: HighResolutionMatrixColumns, Parm2: HighResolutionMatrixRows,
                NodeCount: HighResolutionModel.NodeCount),
        ],
        Groups: [],
        Warnings: []);
}
