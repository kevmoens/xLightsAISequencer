using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 18 §6 (Phase E): converts a real Phase D <see cref="EffectVisualCharacterizationManifest"/>
/// sweep into the shippable, committed <see cref="EffectVisualCharacterCatalog"/> -- applying the deliberate
/// hardware-effect exclusion and the documented-behavior enrichment for effects whose isolated synthetic
/// fixture came back dark for a known, real, disclosed reason.
/// </summary>
public static class EffectVisualCharacterCatalogBuilder
{
    /// <summary>
    /// DMX/Servo/Moving Head are real xLights effects that control physical DMX universes/servo motors, not
    /// pixel colour -- tested on a plain RGB pixel string fixture (the only kind this phase's synthetic
    /// rig produces) they have nothing to actually drive, so they render completely dark almost by
    /// construction. Persisting a "dark, no visible motion" entry for them would wrongly teach the
    /// Director/Choreographer that these effects do nothing on real hardware, where in fact they visibly
    /// do a great deal -- so they are excluded from the catalog entirely, via this explicit, named,
    /// commented set (never a silent filter), so a future re-run/review can find and reconsider it
    /// deliberately.
    /// </summary>
    public static readonly IReadOnlyCollection<string> HardwareOnlyEffectNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "DMX", "Servo", "Moving Head",
    };

    /// <summary>Verbatim manual.xlights.org quotes for effects whose dark Phase D result has a real, documented explanation -- fixed, already-verified text, never re-fetched at tool runtime.</summary>
    private static readonly IReadOnlyDictionary<string, (string Behavior, string Source)> DocumentedBehaviorByEffectName =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            // Needs external input this pipeline's isolated synthetic fixture never supplies.
            ["Duplicate"] = (
                "Copy Effect Data from another model. Each Individual Layer has to be \"duplicated\" to reproduce all the effects across two models.",
                "https://manual.xlights.org/xlights/effects/off/duplicate.md"),
            ["Guitar"] = (
                "The Guitar effect turns MIDI note data into an animated stringed-instrument visualization. Using note data from a MIDI timing track it lights up strings and fret positions in time with the music, and can be styled as a guitar, bass guitar, banjo or violin.",
                "https://manual.xlights.org/xlights/effects/off/guitar.md"),
            ["Glediator"] = (
                "The Glediator effect enables the import and use of an effect that has been created using the Glediator software into xLights.",
                "https://manual.xlights.org/xlights/effects/off/glediator.md"),
            ["Sketch"] = (
                "The Sketch effect lets you trace out a path (a \"sketch\") and have it progressively drawn onto your model over the duration of the effect.",
                "https://manual.xlights.org/xlights/effects/off/sketch.md"),
            ["State"] = (
                "The State effect is similar to the Faces effect, but enables you to have similar functionality for props that are not standard 'Faces' - such as Reindeer Coro faces, a seven segment FM 'Tune to sign' with a colon and a dot etc.",
                "https://manual.xlights.org/xlights/effects/off/state.md"),

            // Audio/behavior-driven (or otherwise expected-active) effects that came back dark -- genuinely
            // unresolved, kept as a known, documented limitation rather than silently dropped.
            ["Fireworks"] = (
                "Creates a fireworks type of effect of exploding lights in a burst. Used most effectively on models such as a mega tree or a matrix.",
                "https://manual.xlights.org/xlights/effects/off/fireworks.md"),
            ["Music"] = (
                "The Music effect analyses and displays a representation of the frequency breakdown of the song from the waveform. Select at least two colors.",
                "https://manual.xlights.org/xlights/effects/off/music.md"),
            ["VU Meter"] = (
                "The VU Meter effect displays a volume meter type of effects that responds to the sound level of an audio track. Each bar represents a note on the scale and the height of each bar represents the intensity.",
                "https://manual.xlights.org/xlights/effects/off/vu-meter.md"),
            ["SingleStrand"] = (
                "An effect that is used to implement chases and other such staggered effects on models that are single dimensional in nature such as house outlines, arches as well as AC models.",
                "https://manual.xlights.org/xlights/effects/off/single-strand.md"),

            // Not an external-input or audio-driven effect -- EffectFixturePlanner's own BuildParamOverrides
            // doc comment records that real parameter tweaks (StartSize, etc.) were tried and confirmed NOT
            // to fix this; a genuinely unresolved rendering gap, same treatment as SingleStrand above.
            ["Shape"] = (
                "The shape effect displays different geometric shapes. They are predefined shapes or System Font can be used.",
                "https://manual.xlights.org/xlights/effects/off/shape.md"),
        };

    /// <summary>
    /// VU Meter's dark result is tier-specific (build-plan §2c: "High tier is already fixed by the
    /// synthetic-audio work -- Low tier wasn't"), so its documented-behavior entry only applies to the Low
    /// tier; a real High-tier Phase D description (if characterized) passes through as-is, undecorated.
    /// </summary>
    private static readonly IReadOnlyCollection<string> DocumentedBehaviorAppliesOnlyToLowTier = new HashSet<string>(StringComparer.Ordinal)
    {
        "VU Meter",
    };

    public static EffectVisualCharacterCatalog Build(EffectVisualCharacterizationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var eligible = new List<EffectVisualCharacterizationOutcome>();
        foreach (var outcome in manifest.Outcomes)
        {
            if (HardwareOnlyEffectNames.Contains(outcome.EffectName))
            {
                continue;
            }

            if (outcome.Status != EffectVisualCharacterizationStatus.Characterized || outcome.Description is null)
            {
                continue;
            }

            if (outcome.MeanBrightness is null || outcome.PeakBrightness is null)
            {
                throw new InvalidOperationException(
                    $"'{outcome.EffectName}' ({outcome.ResolutionTier}) is Characterized but carries no measured brightness -- " +
                    "EffectVisualCharacterizer must measure brightness from the same frames it characterized before this phase ever accepts it.");
            }

            eligible.Add(outcome);
        }

        if (eligible.Count == 0)
        {
            return new EffectVisualCharacterCatalog(manifest.GeneratedAtUtc, []);
        }

        // Build-plan Phase 21 §2b: two real cut points, derived from this exact catalog's own
        // MeanBrightness distribution (terciles -- the lower third is "Off", the middle third "Dim", the
        // upper third "Bright"), never an arbitrary absolute number. See
        // <see cref="ComputeBrightnessCutPoints"/> for the exact interpolated-percentile method.
        var (cut1, cut2) = ComputeBrightnessCutPoints(eligible.Select(o => o.MeanBrightness!.Value).ToList());

        var characters = new List<EffectVisualCharacter>(eligible.Count);
        foreach (var outcome in eligible)
        {
            (string Behavior, string Source)? documented = null;
            if (DocumentedBehaviorByEffectName.TryGetValue(outcome.EffectName, out var entry)
                && (!DocumentedBehaviorAppliesOnlyToLowTier.Contains(outcome.EffectName) || outcome.ResolutionTier == EffectFixtureResolutionTier.Low))
            {
                documented = entry;
            }

            var meanBrightness = outcome.MeanBrightness!.Value;
            var tier = meanBrightness <= cut1 ? BrightnessTier.Off : meanBrightness <= cut2 ? BrightnessTier.Dim : BrightnessTier.Bright;

            characters.Add(new EffectVisualCharacter(
                outcome.EffectName,
                outcome.ResolutionTier.ToString(),
                outcome.Description!.Summary,
                manifest.GeneratedAtUtc,
                documented?.Behavior,
                documented?.Source,
                meanBrightness,
                outcome.PeakBrightness!.Value,
                tier));
        }

        return new EffectVisualCharacterCatalog(manifest.GeneratedAtUtc, characters);
    }

    /// <summary>
    /// Build-plan Phase 21 §2b's own disclosed tercile approach: the 33rd and 66th percentiles of
    /// <paramref name="meanBrightnessValues"/>, linearly interpolated between the two nearest ranked samples
    /// (the standard "exclusive"/<c>PERCENTILE.INC</c>-style method) -- real, reproducible cut points computed
    /// from this app's own real effect vocabulary, never a guessed absolute brightness number.
    /// </summary>
    public static (double Cut1, double Cut2) ComputeBrightnessCutPoints(IReadOnlyList<double> meanBrightnessValues)
    {
        ArgumentNullException.ThrowIfNull(meanBrightnessValues);
        if (meanBrightnessValues.Count == 0)
        {
            throw new ArgumentException("At least one measured value is required to derive brightness cut points.", nameof(meanBrightnessValues));
        }

        var sorted = meanBrightnessValues.OrderBy(v => v).ToList();
        return (Percentile(sorted, 1.0 / 3.0), Percentile(sorted, 2.0 / 3.0));
    }

    private static double Percentile(IReadOnlyList<double> sortedAscending, double fraction)
    {
        if (sortedAscending.Count == 1)
        {
            return sortedAscending[0];
        }

        var rank = fraction * (sortedAscending.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = Math.Min(lowerIndex + 1, sortedAscending.Count - 1);
        var fractionalPart = rank - lowerIndex;
        return sortedAscending[lowerIndex] + ((sortedAscending[upperIndex] - sortedAscending[lowerIndex]) * fractionalPart);
    }
}
