using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Section 3's proposed signature, verbatim: Step 036's own boundary — compiling an
/// already-built/corrected <see cref="BeatGrid"/> into concrete, already-verified
/// <see cref="EffectInstance"/> pulses on real selected props ("Create beat-pop and
/// alternating-chase recipe to real selected props"). A pure, synchronous, in-memory function over
/// values already in hand — the same deliberate narrowing precedent Steps 033/034/035 already set
/// relative to their own Section 3 proposals: it never loads/saves a <see cref="BeatGrid"/> or a
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> itself (that remains the caller's explicit,
/// revision-aware command-boundary responsibility, Section 4), and it only ever calls an
/// already-injected <see cref="IEffectRecipeRegistry"/> to construct each pulse — it never invents an
/// xLights effect shape this registry does not already know (Section 2: "do not invent xLights effect
/// parameters").
/// </summary>
public interface IRhythmGesturePlanner
{
    /// <summary>
    /// Builds one pulse per <paramref name="grid"/> beat inside the half-open <paramref name="range"/>
    /// window, alternating targets round-robin across <paramref name="targets"/> in a stable,
    /// deterministic order (the step's own "alternating-chase" requirement) — each pulse's duration is
    /// derived from that beat's own <see cref="BeatMarker.Confidence"/> ("beat-pop"), snapped to the
    /// real xLights frame grid, and clipped so it never extends past <paramref name="range"/>'s own end
    /// (quality-gate matrix row 7: "remain confined to time scope"). A grid with no beats inside
    /// <paramref name="range"/> — including true silence — returns an empty, non-exceptional list.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="targets"/> is <see langword="null"/>.</exception>
    /// <exception cref="RhythmGesturePlanInvalidException">
    /// <paramref name="targets"/> is empty, <paramref name="verifiedRecipeId"/> is blank, or
    /// <paramref name="range"/> is not a valid, non-negative, non-empty <c>[start, end)</c> window.
    /// </exception>
    /// <exception cref="EffectPlanInvalidException">
    /// <paramref name="verifiedRecipeId"/> is not a recipe the injected
    /// <see cref="IEffectRecipeRegistry"/> actually recognizes — surfaced by that registry's own
    /// <see cref="IEffectRecipeRegistry.CreateVerified"/>, not fabricated here.
    /// </exception>
    IReadOnlyList<EffectInstance> BuildPulses(
        BeatGrid grid,
        IReadOnlySet<ModelId> targets,
        string verifiedRecipeId,
        TimeWindow range);
}
