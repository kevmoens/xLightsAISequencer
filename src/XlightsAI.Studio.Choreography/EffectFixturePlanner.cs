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

    private const string PaletteRef = "fixture-neutral";

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
            new Dictionary<string, ColorPaletteDefinition>(StringComparer.Ordinal) { [PaletteRef] = PaletteBuilder.Build(["#FFC880"]) });
        var section = BuildSection(durationSeconds);

        var plans = new List<EffectFixturePlan>();
        foreach (var effectName in vocabulary.Effects.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            plans.Add(BuildPlan(effectName, EffectFixtureResolutionTier.Low, LowResolutionModel, durationSeconds, layout, section, settings));
            plans.Add(BuildPlan(effectName, EffectFixtureResolutionTier.High, HighResolutionModel, durationSeconds, layout, section, settings));
        }

        return new EffectFixtureManifest(DateTimeOffset.UtcNow, effectMetadataDirectory, plans);
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
        Layers: [new LookLayer(
            Z: 1, Props: model.XlightsName, Effect: effectName, Role: LookLayer.RoleSubject,
            Params: null, Blend: "Normal", Timescale: "Beats", Duty: 1.0)]);

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

    private static DisplayGraph BuildFixtureLayout() => new(
        "fixture-layout",
        Models:
        [
            new DisplayModel(new ModelId(LowResolutionModel.XlightsName), LowResolutionModel.XlightsName, LowResolutionModel.Kind, [], [], NodeCount: LowResolutionModel.NodeCount),
            new DisplayModel(new ModelId(HighResolutionModel.XlightsName), HighResolutionModel.XlightsName, HighResolutionModel.Kind, [], [], NodeCount: HighResolutionModel.NodeCount),
        ],
        Groups: [],
        Warnings: []);
}
