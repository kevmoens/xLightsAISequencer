using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Section 3's own proposed signature, verbatim: the declared, user-visible policy
/// <see cref="IMelodicGestureMapper.MapToEffects"/> uses to turn a <see cref="MelodicGesture"/> sequence
/// into concrete positions along an ordered prop path (Section 3's own architectural design note:
/// "Mapping of pitch to props is a declared policy... test repeated notes, long holds, leaps and
/// descents"). Never a silent default buried in code -- every produced <see cref="Xlights.EffectInstance"/>
/// traces back to one of these three explicit choices.
/// </summary>
/// <param name="PitchPolicy">
/// Which deterministic index-walk rule <see cref="MelodicGestureMapper"/> applies to
/// <see cref="PitchMotion.StepUp"/>/<see cref="PitchMotion.StepDown"/>/<see cref="PitchMotion.LeapUp"/>/
/// <see cref="PitchMotion.LeapDown"/>: <see cref="MelodicGestureMapper.RelativeStepClampPolicy"/> (the
/// walk never leaves the path's own bounds -- a leap past the last/first prop holds at that edge) or
/// <see cref="MelodicGestureMapper.RelativeStepWrapPolicy"/> (a leap past an edge continues from the
/// opposite end). Any other value is rejected -- this mapper never invents an unverified policy.
/// </param>
/// <param name="RecipeId">
/// The already-verified <see cref="IEffectRecipeRegistry"/> recipe id every produced effect is created
/// through (Section 3: "verified effect recipes") -- never a free-form xLights effect shape.
/// </param>
/// <param name="RespectRests">
/// When <see langword="true"/> (the default), a <see cref="PitchMotion.Rest"/> gesture that arrives
/// after at least one already-emitted effect produces no new effect at all (Section 12's own worked
/// example, verbatim: "a rest emits no new light"). The very first sounding note of the mapped window is
/// never suppressed by this flag even though <see cref="MelodicContourClassifier"/> also labels it
/// <see cref="PitchMotion.Rest"/> (there is no real prior light for it to be silent against -- see
/// <see cref="MelodicGestureMapper"/>'s own remarks). When <see langword="false"/>, every
/// <see cref="PitchMotion.Rest"/> gesture still sounds at the walk's current position, exactly like
/// <see cref="PitchMotion.Repeat"/>.
/// </param>
public sealed record GestureMapSettings(string PitchPolicy, string RecipeId, bool RespectRests)
{
    /// <summary>
    /// PHASE-07 Step 051's own additive fallback tunable (required acceptance, verbatim: "Fallback to
    /// contour or onsets when pitch unreliable"). A <see cref="MelodicGesture"/> whose own
    /// <see cref="MelodicGesture.Confidence"/> falls below this threshold (but at/above
    /// <see cref="OnsetOnlyConfidenceFloor"/>) is still mapped, but a <see cref="PitchMotion.LeapUp"/>/
    /// <see cref="PitchMotion.LeapDown"/> is downgraded to the matching
    /// <see cref="PitchMotion.StepUp"/>/<see cref="PitchMotion.StepDown"/> before the walk moves --
    /// "contour" fallback: the *direction* of an uncertain pitch estimate is trusted, the exact leap
    /// *magnitude* is not, so a falsely-detected large interval (e.g. an octave error) cannot fling the
    /// walk across the whole prop path on a guess (Section 3's own architectural design note, restated
    /// for this exact failure mode: "an octave error [must] not necessarily ruin a lighting phrase").
    /// Declared, inspectable policy, not a silent default -- an additive <c>init</c> property (not a
    /// positional parameter) so every already-committed Step 050 construction site/test keeps compiling
    /// unchanged; its default of <c>0.0</c> means "never downgrade" (<see cref="GestureMapSettings"/>
    /// values built before this step, or any caller not opting in, see no behavior change). Must be in
    /// <c>[0, 1]</c> and never smaller than <see cref="OnsetOnlyConfidenceFloor"/>.
    /// </summary>
    public double MinGestureConfidence { get; init; }

    /// <summary>
    /// PHASE-07 Step 051's own second, lower fallback tier. A gesture whose own
    /// <see cref="MelodicGesture.Confidence"/> falls below this floor is mapped as a plain onset pulse at
    /// the walk's current, unmoved position -- the same treatment as <see cref="PitchMotion.Repeat"/> --
    /// regardless of its classified <see cref="MelodicGesture.Motion"/> (except
    /// <see cref="PitchMotion.Hold"/>, whose duration-only semantics never depend on pitch confidence at
    /// all, and <see cref="PitchMotion.Rest"/>, whose silence is a timing-gap decision, not a pitch
    /// decision): "onset" fallback, the lowest rung of Section 3's own fallback ladder ("rhythm-only
    /// events") -- the note's own real timing still produces a real effect ("no invalid effects," this
    /// step's own required acceptance, verbatim), it simply is not trusted to move the walk at all.
    /// Additive <c>init</c> property, default <c>0.0</c> ("never fall back to onset-only"), for the same
    /// backward-compatibility reason as <see cref="MinGestureConfidence"/>. Must be in <c>[0, 1]</c> and
    /// never larger than <see cref="MinGestureConfidence"/>.
    /// </summary>
    public double OnsetOnlyConfidenceFloor { get; init; }

    /// <summary>
    /// <see cref="MelodicGestureMapper.RelativeStepClampPolicy"/>, <see cref="EffectRecipeRegistry.SimpleOnRecipeId"/>
    /// (the only recipe this build currently verifies), <see cref="RespectRests"/> <see langword="true"/>.
    /// Step 051's own fallback thresholds default to a musically reasonable, already-active pair mirroring
    /// <see cref="GestureClassificationSettings.Default"/>'s own <c>MinNoteConfidence</c> (<c>0.5</c>):
    /// below <c>0.5</c> confidence a leap downgrades to a step; below <c>0.25</c> confidence a gesture
    /// becomes a plain onset pulse regardless of direction.
    /// </summary>
    public static GestureMapSettings Default { get; } = new(
        PitchPolicy: MelodicGestureMapper.RelativeStepClampPolicy,
        RecipeId: EffectRecipeRegistry.SimpleOnRecipeId,
        RespectRests: true)
    {
        MinGestureConfidence = 0.5,
        OnsetOnlyConfidenceFloor = 0.25,
    };
}
