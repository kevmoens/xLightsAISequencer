using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// Default <see cref="IMelodicGestureMapper"/>. Walks <see cref="MelodyTrack.Notes"/> in order (already
/// guaranteed sorted/non-overlapping by <see cref="MelodyTrackValidator"/>), tracking one "current index"
/// into <paramref name="orderedPath"/>'s own position list -- starting at index <c>0</c> (Section 12's own
/// worked example, verbatim: a five-arch path's first position is "arch 1") -- and moving that index by a
/// fixed, declared step per <see cref="MelodicGesture.Motion"/>: <see cref="PitchMotion.StepUp"/>/
/// <see cref="PitchMotion.StepDown"/> move by <see cref="StepIndexDelta"/>; <see cref="PitchMotion.LeapUp"/>/
/// <see cref="PitchMotion.LeapDown"/> move by <see cref="LeapIndexDelta"/> (Section 3: "measure musical
/// interval not only note index" describes how the *classifier* derives up/down/step/leap from semitone
/// distance -- Section 12's own worked example then maps every step/leap onto the *same fixed index
/// distance* regardless of exact semitone count, so an octave error in pitch tracking does not fling the
/// walk across the whole path). <see cref="PitchMotion.Repeat"/> and a non-suppressed
/// <see cref="PitchMotion.Rest"/> both pulse the current index unchanged (Section 12: "a repeated pitch
/// pulses the current arch"). <see cref="PitchMotion.Hold"/> never starts a new effect -- it extends the
/// immediately preceding effect's own <see cref="EffectInstance.EndTicks"/> (Section 12: "a sustained note
/// holds its effect until the note-off event").
///
/// Verified directly against Section 12's own worked example: <c>C4,D4,E4,G4,E4,D4,C4</c> against a
/// 5-position path with <see cref="RelativeStepClampPolicy"/> produces the index sequence
/// <c>0,1,2,4,2,1,0</c> -- Section 12's own <c>1,2,3,5,3,2,1</c> (one-based).
///
/// <para>
/// <b>Disclosed architecture deviation from Section 3's proposed bare
/// <c>MapToEffects(MelodyTrack, IReadOnlyList&lt;ModelId&gt;, TimeWindow, GestureMapSettings)</c>:</b> an
/// <see cref="IEffectRecipeRegistry"/> parameter was added, mirroring
/// <see cref="IThreeEffectPlanFactory.CreatePlan"/>'s own identical relationship to the same interface --
/// Section 3's own architectural design note requires "verified effect recipes," and only the registry
/// itself can verify a recipe id/target/window triple before constructing an
/// <see cref="EffectInstance"/>; a bare four-argument signature has no way to obtain one.
/// </para>
/// <para>
/// <b>Disclosed design decision -- why the very first <see cref="PitchMotion.Rest"/> still sounds:</b>
/// <see cref="MelodicContourClassifier"/> always classifies a track's first note <see cref="PitchMotion.Rest"/>
/// (it has no confident predecessor -- "arrives out of implied silence," that step's own already-committed
/// doc comment). Section 12's own worked example proves this first note must still receive arch 1, not
/// silence, even though it is literally classified <see cref="PitchMotion.Rest"/> -- so
/// <see cref="GestureMapSettings.RespectRests"/> only suppresses a <see cref="PitchMotion.Rest"/> gesture
/// that arrives *after* at least one effect has already been emitted for this mapping (a genuine
/// mid-track silence, Section 3's own required failure/edge case: "Silence... must be handled
/// explicitly" -- handled here by producing no stray effect, not by crashing or guessing).
/// </para>
/// </summary>
public sealed class MelodicGestureMapper : IMelodicGestureMapper
{
    /// <summary>A leap/step past the path's own last or first position holds at that edge -- never wraps.</summary>
    public const string RelativeStepClampPolicy = "RelativeStepClamp";

    /// <summary>A leap/step past the path's own last or first position continues from the opposite end.</summary>
    public const string RelativeStepWrapPolicy = "RelativeStepWrap";

    /// <summary>Index distance <see cref="PitchMotion.StepUp"/>/<see cref="PitchMotion.StepDown"/> move the walk.</summary>
    public const int StepIndexDelta = 1;

    /// <summary>Index distance <see cref="PitchMotion.LeapUp"/>/<see cref="PitchMotion.LeapDown"/> move the walk (Section 12's own worked example: E4-to-G4, a minor-third leap, moves arch 3 to arch 5 -- two positions, not three).</summary>
    public const int LeapIndexDelta = 2;

    public IReadOnlyList<EffectInstance> MapToEffects(
        MelodyTrack track,
        IReadOnlyList<ModelId> orderedPath,
        TimeWindow window,
        GestureMapSettings settings,
        IEffectRecipeRegistry recipeRegistry)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(orderedPath);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recipeRegistry);

        if (orderedPath.Count == 0)
        {
            throw new EffectPlanInvalidException(
                "MapToEffects requires at least one ordered target prop; the supplied path was empty.");
        }

        if (window.EndTicks <= window.StartTicks)
        {
            throw new EffectPlanInvalidException(
                $"MapToEffects requires a valid, non-empty [start, end) window (received [{window.StartTicks}, {window.EndTicks})) -- an invalid timing boundary must be rejected explicitly.");
        }

        if (settings.PitchPolicy != RelativeStepClampPolicy && settings.PitchPolicy != RelativeStepWrapPolicy)
        {
            throw new EffectPlanInvalidException(
                $"Unknown gesture-mapping pitch policy '{settings.PitchPolicy}'. Known policies: {RelativeStepClampPolicy}, {RelativeStepWrapPolicy}.");
        }

        if (settings.MinGestureConfidence is < 0.0 or > 1.0)
        {
            throw new EffectPlanInvalidException(
                $"GestureMapSettings.MinGestureConfidence ({settings.MinGestureConfidence}) must be in [0, 1].");
        }

        if (settings.OnsetOnlyConfidenceFloor is < 0.0 or > 1.0)
        {
            throw new EffectPlanInvalidException(
                $"GestureMapSettings.OnsetOnlyConfidenceFloor ({settings.OnsetOnlyConfidenceFloor}) must be in [0, 1].");
        }

        if (settings.OnsetOnlyConfidenceFloor > settings.MinGestureConfidence)
        {
            throw new EffectPlanInvalidException(
                $"GestureMapSettings.OnsetOnlyConfidenceFloor ({settings.OnsetOnlyConfidenceFloor}) must not exceed MinGestureConfidence ({settings.MinGestureConfidence}).");
        }

        // recipeRegistry.CreateVerified itself rejects an unknown recipe id -- not duplicated here.

        var gesturesByNoteId = track.Gestures.ToDictionary(g => g.NoteId);

        var effects = new List<EffectInstance>();
        var currentIndex = 0;
        var hasEmittedAny = false;
        EffectInstance? lastEffect = null;

        foreach (var note in track.Notes)
        {
            if (note.StartTicks < window.StartTicks || note.StartTicks >= window.EndTicks)
            {
                continue; // outside the requested preview window -- silently excluded, not an error.
            }

            if (!gesturesByNoteId.TryGetValue(note.Id, out var gesture))
            {
                continue; // defensive: MelodyTrackValidator/MelodicContourClassifier already guarantee one gesture per note.
            }

            var effectiveMotion = ApplyConfidenceFallback(gesture, settings);

            switch (effectiveMotion)
            {
                case PitchMotion.Rest when hasEmittedAny && settings.RespectRests:
                    lastEffect = null; // the gap breaks any hold-continuation across it.
                    continue; // "a rest emits no new light" -- no stray effect.

                case PitchMotion.Hold when lastEffect is not null:
                    var extended = lastEffect with { EndTicks = note.EndTicks };
                    effects[^1] = extended;
                    lastEffect = extended;
                    continue;

                case PitchMotion.StepUp:
                    currentIndex = MoveIndex(currentIndex, StepIndexDelta, orderedPath.Count, settings.PitchPolicy);
                    break;

                case PitchMotion.StepDown:
                    currentIndex = MoveIndex(currentIndex, -StepIndexDelta, orderedPath.Count, settings.PitchPolicy);
                    break;

                case PitchMotion.LeapUp:
                    currentIndex = MoveIndex(currentIndex, LeapIndexDelta, orderedPath.Count, settings.PitchPolicy);
                    break;

                case PitchMotion.LeapDown:
                    currentIndex = MoveIndex(currentIndex, -LeapIndexDelta, orderedPath.Count, settings.PitchPolicy);
                    break;

                    // PitchMotion.Repeat, a non-suppressed PitchMotion.Rest, and a PitchMotion.Hold with no
                    // prior effect to extend (e.g. the window's own first note) all fall through unchanged:
                    // pulse/emit fresh at the walk's current, unmoved position.
            }

            var target = orderedPath[currentIndex];
            var effect = recipeRegistry.CreateVerified(settings.RecipeId, target, note.StartTicks, note.EndTicks);
            effects.Add(effect);
            lastEffect = effect;
            hasEmittedAny = true;
        }

        return effects;
    }

    /// <summary>
    /// PHASE-07 Step 051 — "Fallback to contour or onsets when pitch unreliable" (required acceptance,
    /// verbatim). Below <see cref="GestureMapSettings.OnsetOnlyConfidenceFloor"/>, any pitch-based motion
    /// (<see cref="PitchMotion.Repeat"/>/<see cref="PitchMotion.StepUp"/>/<see cref="PitchMotion.StepDown"/>/
    /// <see cref="PitchMotion.LeapUp"/>/<see cref="PitchMotion.LeapDown"/>) degrades to a plain onset pulse
    /// at the walk's current, unmoved position ("onset" fallback -- the note's own real timing still
    /// produces a real effect; only the walk's movement is distrusted). Between the floor and
    /// <see cref="GestureMapSettings.MinGestureConfidence"/>, a leap downgrades to the matching step
    /// ("contour" fallback -- direction trusted, exact interval not). <see cref="PitchMotion.Hold"/> and
    /// <see cref="PitchMotion.Rest"/> are never touched here: a hold's duration and a rest's silence are
    /// timing decisions, not pitch decisions, so neither depends on how confident the pitch estimate was.
    /// </summary>
    private static PitchMotion ApplyConfidenceFallback(MelodicGesture gesture, GestureMapSettings settings)
    {
        if (gesture.Motion is PitchMotion.Hold or PitchMotion.Rest)
        {
            return gesture.Motion;
        }

        if (gesture.Confidence < settings.OnsetOnlyConfidenceFloor)
        {
            return PitchMotion.Repeat;
        }

        if (gesture.Confidence < settings.MinGestureConfidence)
        {
            return gesture.Motion switch
            {
                PitchMotion.LeapUp => PitchMotion.StepUp,
                PitchMotion.LeapDown => PitchMotion.StepDown,
                _ => gesture.Motion,
            };
        }

        return gesture.Motion;
    }

    private static int MoveIndex(int current, int delta, int pathLength, string policy)
    {
        var next = current + delta;

        if (policy == RelativeStepWrapPolicy)
        {
            next %= pathLength;
            if (next < 0)
            {
                next += pathLength;
            }

            return next;
        }

        return Math.Clamp(next, 0, pathLength - 1);
    }
}
