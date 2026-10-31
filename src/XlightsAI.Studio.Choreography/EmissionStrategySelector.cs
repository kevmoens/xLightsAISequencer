namespace XlightsAI.Studio.Choreography;

/// <summary>Build-plan Phase 4 task 5: which emission strategy <see cref="LookCompiler"/> used for one layer.</summary>
public enum EmissionMode
{
    /// <summary>One long effect bound to a labelled timing track and/or carrying value curves (architecture §6: "idiomatic, compact, musically exact, and self-adjusting if the beat grid is corrected later").</summary>
    Bound,

    /// <summary>Per-event discrete effects -- the fallback for an effect type with no timing-track input and no declared value curve.</summary>
    Discrete,
}

/// <summary>Which real xLights property an effect reads a timing track/filter label from, for the seven timing-track-reading effects architecture §6 names verbatim.</summary>
/// <param name="TimingTrackPropertyId">The effect's own choice property that names the bound timing track.</param>
/// <param name="FilterLabelPropertyId">The effect's own text property restricting the binding to marks carrying one label, or <see langword="null"/> when that effect has none.</param>
/// <param name="EnableCheckboxPropertyId">A boolean gate that must also be set to enable the binding (Fireworks' <c>FIRETIMING</c>, Shape's <c>Shape_FireTiming</c>), or <see langword="null"/> when none is required.</param>
public sealed record TimingTrackBinding(string TimingTrackPropertyId, string? FilterLabelPropertyId, string? EnableCheckboxPropertyId);

/// <summary>
/// Build-plan Phase 4 task 5 ("prefer one long effect bound to a labelled timing track over discrete
/// per-event effects"). Architecture §6's own table of the seven real xLights effects that read a timing
/// track directly, verified against the real <c>C:\Program Files\xLights\effectmetadata</c> property ids
/// on this machine (not guessed/invented).
/// </summary>
public static class EmissionStrategySelector
{
    public static readonly IReadOnlyDictionary<string, TimingTrackBinding> TimingTrackBoundEffects = new Dictionary<string, TimingTrackBinding>(StringComparer.Ordinal)
    {
        ["Shockwave"] = new("Shockwave_TimingTrack", "Shockwave_FilterLabel", null),
        ["Ripple"] = new("Ripple_TimingTrack", "Ripple_FilterLabel", null),
        ["SingleStrand"] = new("SingleStrand_TimingTrack", null, null),
        ["Fireworks"] = new("FIRETIMINGTRACK", null, "FIRETIMING"),
        ["Shape"] = new("Shape_FireTimingTrack", "Shape_FilterLabel", "Shape_FireTiming"),
        ["VUMeter"] = new("VUMeter_TimingTrack", "Filter", null),
        ["Text"] = new("Text_LyricTrack", null, null),

        // Build-plan Phase 8 task 3 (singing-face support): Faces_MouthMovements is xLights' own
        // custom combo-plus-timing-track picker (see EffectVocabularyBuilder's documented hardcoded
        // exception); modeled here as a plain choice-property binding like every other entry in this
        // table. Faces_SuppressWhenNotSinging is a real property this build also sets whenever a
        // phoneme track is bound, so mouth movement stops cleanly during a rest phoneme/non-singing gap.
        ["Faces"] = new("Faces_MouthMovements", null, "Faces_SuppressWhenNotSinging"),
    };

    /// <summary>Whether <paramref name="xlightsEffectName"/> is one of the seven effects that can bind directly to a timing track.</summary>
    public static bool SupportsTimingTrackBinding(string xlightsEffectName) => TimingTrackBoundEffects.ContainsKey(xlightsEffectName);

    /// <summary>
    /// Chooses <see cref="EmissionMode.Bound"/> when <paramref name="xlightsEffectName"/> supports direct
    /// timing-track binding and <paramref name="layer"/> names a real track via its own
    /// <see cref="LookLayer.Timescale"/>, OR <paramref name="layer"/> declares at least one value curve
    /// (architecture §6's "Music"/"Timing Track Fade" families) -- <see cref="EmissionMode.Discrete"/>
    /// otherwise, the fallback "for hits, snaps, hard cuts and anything whose effect type has no
    /// timing-track input."
    /// </summary>
    public static EmissionMode SelectMode(string xlightsEffectName, LookLayer layer, IReadOnlySet<string> availableTimingTrackNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xlightsEffectName);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(availableTimingTrackNames);

        if (SupportsTimingTrackBinding(xlightsEffectName) &&
            layer.Timescale is { Length: > 0 } track &&
            availableTimingTrackNames.Contains(track))
        {
            return EmissionMode.Bound;
        }

        return layer.CurvesOrEmpty.Count > 0 ? EmissionMode.Bound : EmissionMode.Discrete;
    }
}
