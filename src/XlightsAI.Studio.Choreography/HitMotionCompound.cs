using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 30 step 30.2 ("hit-motion-compound"): the one confirmed, real movement-overlay shape
/// a <see cref="SectionCue"/> may opt into pairing with its own base hit effect (<see cref="SectionCue.Compound"/>)
/// -- a directional Shockwave ring expansion. "Shockwave" is a real, registered xLights effect (already
/// used by mined <c>MultiLayerTemplate</c>s and catalogued in <c>EffectVisualCharacterCatalog</c>);
/// <see cref="MovementValueCurveParameterKey"/> (xLights' own <c>Shockwave_End_Radius</c>, real property
/// label "Radius2") is confirmed value-curve-eligible (<c>"valueCurve": true</c>, real <c>min</c>/<c>max</c>
/// 0/750) directly against the installed <c>effectmetadata/Shockwave.json</c> schema (checked in,
/// byte-identical, at <c>tests/XlightsAI.Studio.Xlights.Tests/Fixtures/effectmetadata/Shockwave.json</c>
/// -- see <c>implementation-evidence/PHASE-30-hit-motion-compound.md</c>). <c>"Ramp"</c> is already a
/// confirmed, real, exported value-curve type -- the exact same family <see cref="LookCompiler"/>'s own
/// Mode A curve binding already emits and
/// <c>Phase04VocabularyAwareExportTests.ExportAsync_WithVocabulary_SerializesValueCurve_BoundToTimingTrack</c>
/// already confirms end to end. Never a second, independently-invented motion family -- this is the ONE
/// confirmed shape 30.2 ships; a future step may confirm and add more.
/// </summary>
public static class HitMotionCompound
{
    /// <summary>The one confirmed, real xLights effect used for the movement overlay.</summary>
    public const string MovementEffect = "Shockwave";

    /// <summary>
    /// The real, value-curve-eligible <c>SettingKey</c> for Shockwave's own "Radius2"/<c>Shockwave_End_Radius</c>
    /// property -- confirmed against the installed <c>effectmetadata/Shockwave.json</c> (<c>"valueCurve": true</c>).
    /// </summary>
    public const string MovementValueCurveParameterKey = "E_SLIDER_Shockwave_End_Radius";

    /// <summary>The real, already-confirmed/exported value-curve family used for the movement overlay's own ramp.</summary>
    public const string MovementCurveType = "Ramp";

    /// <summary>
    /// Builds the one real movement-overlay <see cref="EffectInstance"/> for a compound cue's resolved
    /// span -- the EXACT same <c>[start, end)</c> window as the base hit (never a different/shortened
    /// one, per the build plan's own "scoped to the same cue's span" wording), a distinct IR layer
    /// (<paramref name="baseLayerZ"/> negated, so it can never collide with any real base/cue layer --
    /// every one of those is always &gt;= 0; <see cref="XsqExporter"/>'s own per-target
    /// <c>GroupBy(e => e.Layer)</c> only needs distinctness, never contiguity) and a real "Ramp" value
    /// curve spanning Shockwave's own confirmed Min/Max radius range -- never the effect's own flat/static
    /// default (a plain, non-curved <c>Shockwave_End_Radius</c> setting). Returns <see langword="null"/>
    /// (skip, never invent) when "Shockwave" is not a registered recipe for the supplied
    /// <paramref name="settings"/>, or its own confirmed value-curve parameter is unavailable in the
    /// supplied vocabulary -- the same "resolve, or skip rather than invent" discipline every other
    /// Choreography compiler path already follows.
    /// </summary>
    public static EffectInstance? TryBuild(
        ModelId target, long start, long end, int baseLayerZ, LookCompilerSettings settings,
        string? sourceAnnotation, string? cueTraceId)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (end <= start)
        {
            return null;
        }

        var recipeId = LookCompiler.RecipeIdFor(MovementEffect, settings.RecipeCatalog);
        if (!settings.RecipeCatalog.TryGet(recipeId, out _))
        {
            return null; // "Shockwave" is not registered in this vocabulary/recipe set -- skip, never invented.
        }

        if (!settings.Vocabulary.TryGetProperty(MovementEffect, MovementValueCurveParameterKey, out var property) ||
            !property.SupportsValueCurve)
        {
            return null; // The confirmed value-curve parameter is not available for this vocabulary -- skip, never invented.
        }

        var min = property.Min ?? 0;
        var max = property.Max ?? 100;
        if (max <= min)
        {
            return null;
        }

        return settings.RecipeRegistry.CreateVerified(recipeId, target, start, end) with
        {
            Layer = -baseLayerZ,
            PaletteIndex = 0,
            SourceAnnotation = sourceAnnotation,
            CueTraceId = cueTraceId,
            ValueCurves = [new EffectValueCurve(MovementValueCurveParameterKey, MovementCurveType, min, max, P1: min, P2: max)],
        };
    }
}
