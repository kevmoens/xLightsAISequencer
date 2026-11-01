namespace XlightsAI.Studio.Director;

/// <summary>Architecture §10: "palette discipline is expressed by which checkboxes are ticked" -- the active-slot band a style constrains a section's declared palette to (<see cref="XlightsAI.Studio.Refinement.Harness.HarnessSection.StyleMinPaletteSlots"/>/<c>StyleMaxPaletteSlots</c> reuse this exact shape).</summary>
/// <param name="MinActiveSlots">Fewest active palette slots this style allows a section to declare.</param>
/// <param name="MaxActiveSlots">Most active palette slots this style allows a section to declare.</param>
/// <param name="Description">One sentence of prose guidance for the Director prompt (e.g. which hue families, which role the slots play).</param>
public sealed record PaletteDiscipline(int MinActiveSlots, int MaxActiveSlots, string Description);

/// <summary>Architecture §10's "density / blend" column -- a target band for how fast the compiled show changes, consumed both as Director prompt guidance and (via <see cref="XlightsAI.Studio.Refinement.Harness.HarnessSection"/>) as a metric-battery target band.</summary>
/// <param name="MinChangeRatePerSecond">Slowest acceptable composition-change rate for this style.</param>
/// <param name="MaxChangeRatePerSecond">Fastest acceptable composition-change rate for this style.</param>
/// <param name="MinDutyCycle">Lowest acceptable pixel-heavy duty cycle for this style.</param>
/// <param name="MaxDutyCycle">Highest acceptable pixel-heavy duty cycle for this style.</param>
/// <param name="Description">One sentence of prose guidance (blend modes, fade lengths, effect pacing).</param>
public sealed record StyleDensity(double MinChangeRatePerSecond, double MaxChangeRatePerSecond, double MinDutyCycle, double MaxDutyCycle, string Description);

/// <summary>
/// Architecture §10: "Styles are coherent bundles of palette discipline, effect pool, density curve,
/// prop-role bias and structural convention -- not cosmetic presets." Build-plan Phase 8 task 1: a
/// structured, serializable style bundle conditioning both the Director/Choreographer prompts
/// (<see cref="DirectorPromptBuilder"/>/<see cref="SectionScriptPromptBuilder"/>) and the metric
/// battery's own target bands (<see cref="XlightsAI.Studio.Refinement.Harness.SequenceMetricBattery"/>).
/// </summary>
/// <param name="Id">Stable identity, also the string a caller may pass to <see cref="StylePresetCatalog.Resolve"/>.</param>
/// <param name="Name">Human-readable display name -- architecture §10's own table names (e.g. "High-Energy / EDM").</param>
/// <param name="PaletteDiscipline">This style's own palette-slot band.</param>
/// <param name="EffectPool">Real xLights effect names this style prefers (architecture §10's "effect vocabulary" column). Empty means unconstrained -- the Director prompt falls back to the full generated vocabulary.</param>
/// <param name="Density">This style's own change-rate/duty-cycle target band.</param>
/// <param name="PropRoleBias">A relative preference weight (roughly <c>[0,1]</c>) per <see cref="MusicalRole"/> (bass/melody/highs) -- prose guidance for the Director prompt, not mechanically enforced everywhere.</param>
/// <param name="Description">One sentence summarizing the style's overall intent.</param>
public sealed record StylePreset(
    string Id,
    string Name,
    PaletteDiscipline PaletteDiscipline,
    IReadOnlyList<string> EffectPool,
    StyleDensity Density,
    IReadOnlyDictionary<string, double> PropRoleBias,
    string Description);

/// <summary>
/// Build-plan Phase 8 task 1: a small registry of the architecture §10 style presets. Deliberately data,
/// not code -- adding a style is adding one more <see cref="StylePreset"/> value to <see cref="All"/>,
/// never touching <see cref="DirectorPromptBuilder"/>/<see cref="SectionScriptPromptBuilder"/>/
/// <see cref="XlightsAI.Studio.Refinement.Harness.SequenceMetricBattery"/>. Only four of architecture
/// §10's eight presets are implemented with real distinct values (chosen to stress different axes --
/// restrained/traditional, maximal/EDM, minimal/elegant, and the lyric-track-bound singing-face style
/// Phase 8 task 3 also needs); the rest are a trivial data addition for a later pass.
/// </summary>
public static class StylePresetCatalog
{
    public const string TraditionalClassicId = "Traditional / Classic";
    public const string HighEnergyEdmId = "High-Energy / EDM";
    public const string ElegantWarmWhiteId = "Elegant / Warm-White";
    public const string SingingFaceStorytellingId = "Singing-Face Storytelling";
    public const string SteadyWashAccentId = "Steady Wash / Accent";

    public static readonly StylePreset TraditionalClassic = new(
        TraditionalClassicId, TraditionalClassicId,
        new PaletteDiscipline(2, 3, "Warm white + red + green; never more than 3 active slots."),
        ["Garlands", "Marquee", "SingleStrand", "Twinkle", "Color Wash"],
        new StyleDensity(0.1, 0.5, 0.45, 0.90, "Low density, long effects, Normal blend."),
        new Dictionary<string, double>(StringComparer.Ordinal) { [MusicalRole.Bass] = 0.5, [MusicalRole.Melody] = 0.5, [MusicalRole.Highs] = 0.3 },
        "A restrained, classic holiday look: few colours, long-held effects, nothing flashy.");

    public static readonly StylePreset HighEnergyEdm = new(
        HighEnergyEdmId, HighEnergyEdmId,
        new PaletteDiscipline(4, 6, "4-6 saturated, high-contrast slots."),
        // User feedback ("increase the number of effects and layers throughout the show"): the original
        // 6-name pool left the AI's own Look choices (as opposed to the always-moving gap-filler's own
        // separate 6-state pool) with very little real variety -- widened to include a hold/overlay-
        // capable effect (Twinkle, Color Wash), a bar-synced chase option (Bars), and a second real
        // timing-track-bound effect (Ripple) so dense, beat-locked Looks have more than Shockwave/Strobe
        // to alternate between.
        ["Shockwave", "Strobe", "Morph", "Pinwheel", "Fireworks", "SingleStrand", "Twinkle", "Color Wash", "Bars", "Ripple"],
        // Change-rate floor/ceiling raised (was 1.5-4.0) for the same reason -- a direct, mechanical push
        // toward more effect instances per second throughout the show, not just more variety to pick from.
        new StyleDensity(2.5, 6.0, 0.60, 1.0, "High density, Additive/Max accents, fades under 0.2s."),
        new Dictionary<string, double>(StringComparer.Ordinal) { [MusicalRole.Bass] = 0.8, [MusicalRole.Melody] = 0.4, [MusicalRole.Highs] = 0.7 },
        "Maximal, beat-locked, high-contrast -- EDM/dubstep territory.");

    public static readonly StylePreset ElegantWarmWhite = new(
        ElegantWarmWhiteId, ElegantWarmWhiteId,
        new PaletteDiscipline(2, 2, "Exactly #FFFFFF + #FFC880 -- two slots, no more."),
        ["Color Wash", "On", "Fill", "Bars"],
        new StyleDensity(0.02, 0.2, 0.25, 0.55, "Very low density; 1-2 second fades."),
        new Dictionary<string, double>(StringComparer.Ordinal) { [MusicalRole.Bass] = 0.3, [MusicalRole.Melody] = 0.6, [MusicalRole.Highs] = 0.2 },
        "Quiet, upscale, two-colour warm-white elegance.");

    public static readonly StylePreset SingingFaceStorytelling = new(
        SingingFaceStorytellingId, SingingFaceStorytellingId,
        new PaletteDiscipline(2, 4, "White face + a coloured background palette."),
        ["Faces", "Text", "Pictures", "Color Wash"],
        new StyleDensity(0.2, 1.0, 0.4, 0.8, "Face always on; restrained background behind it."),
        new Dictionary<string, double>(StringComparer.Ordinal) { [MusicalRole.Bass] = 0.3, [MusicalRole.Melody] = 0.9, [MusicalRole.Highs] = 0.3 },
        "Lyric-driven storytelling carried by a singing face bound to the vocal track.");

    /// <summary>
    /// Added 2026-11-01 after directly comparing a real hand-sequenced reference
    /// ("Existence (VIP) - Shortened.xsq") against this app's own High-Energy/EDM-styled generation of
    /// the same song: the reference was dominated (326 of 462 effects, ~70%) by the plain "On" effect --
    /// a bold, saturated color held steady as a base layer per section -- with only a handful of accent
    /// effects (Wave, Pinwheel, Shockwave, Fire, Spirals, Curtain, Garlands) at specific moments. The
    /// generated version had zero "On" usage at all (not in High-Energy/EDM's effect pool) and a forced
    /// 2.5-6.0 changes/sec density floor, so it could never produce a calm, held moment the way the real
    /// reference does throughout. This preset is that measured "mostly steady, occasionally punchy" shape
    /// as a real, selectable style -- not a guess, a direct fit to a real show.
    /// </summary>
    public static readonly StylePreset SteadyWashAccent = new(
        SteadyWashAccentId, SteadyWashAccentId,
        new PaletteDiscipline(2, 4, "2-4 bold, saturated colors (not pastel/warm-white) -- a held color is the point, not a blend."),
        ["On", "Color Wash", "Wave", "Pinwheel", "Shockwave", "Fire", "Spirals", "Curtain", "Garlands", "SingleStrand"],
        new StyleDensity(0.1, 0.6, 0.55, 0.95, "Low density: long steady full-brightness holds as the base layer, interrupted by brief bold accent bursts only at key moments."),
        new Dictionary<string, double>(StringComparer.Ordinal) { [MusicalRole.Bass] = 0.6, [MusicalRole.Melody] = 0.5, [MusicalRole.Highs] = 0.3 },
        "Bold, saturated color held steady as the base look -- calm most of the time, with a handful of deliberate accent bursts reserved for key moments, not constant motion.");

    public static readonly IReadOnlyList<StylePreset> All = [TraditionalClassic, HighEnergyEdm, ElegantWarmWhite, SingingFaceStorytelling, SteadyWashAccent];

    /// <summary>
    /// Resolves <paramref name="styleNameOrId"/> against <see cref="All"/> by id or display name
    /// (case-insensitive), or returns a generic, unconstrained preset carrying <paramref name="styleNameOrId"/>
    /// as its own name/id when no match exists -- never throws, so an unrecognized style string degrades
    /// to "no additional constraint" rather than failing prompt construction.
    /// </summary>
    public static StylePreset Resolve(string styleNameOrId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleNameOrId);

        var match = All.FirstOrDefault(s =>
            string.Equals(s.Id, styleNameOrId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Name, styleNameOrId, StringComparison.OrdinalIgnoreCase));
        return match ?? Generic(styleNameOrId);

        static StylePreset Generic(string label) => new(
            label, label,
            new PaletteDiscipline(1, 8, "No style-specific palette discipline -- unconstrained."),
            [],
            new StyleDensity(0.0, double.MaxValue, 0.0, 1.0, "No style-specific density target -- unconstrained."),
            new Dictionary<string, double>(StringComparer.Ordinal),
            "No matching style preset was found; compiling with no additional style constraint.");
    }
}
