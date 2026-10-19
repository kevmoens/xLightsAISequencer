using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3's own proposed signature, verbatim: one geometry-resolved musical gesture, bound to a
/// real <see cref="DisplayNode"/> target but deliberately independent of any xLights effect recipe (Step
/// 064's own implementation guidance: "separate mapping from effect recipe rendering") -- unlike Phase 07's
/// <see cref="Melody.IMelodicGestureMapper.MapToEffects"/>, which produces an already-recipe-bound
/// <c>Xlights.EffectInstance</c> directly, this type carries only target/timing/kind/intensity so the same
/// resolved walk can later be rendered through any verified recipe a consuming phase chooses.
/// </summary>
/// <param name="Target">The display node this gesture lands on, after any <see cref="GestureBoundaryPolicy"/> adjustment.</param>
/// <param name="StartTicks">First tick (inclusive) this gesture applies from, half-open -- always the same ticks as the source <see cref="Melody.MelodicGesture"/>/<see cref="Melody.NoteEvent"/> it was resolved from (or a sub-slice of them, for a <see cref="PathTraversalKind.Sweep"/> entry), never a wall-clock timestamp.</param>
/// <param name="EndTicks">One past the last tick this gesture applies to; always <c>&gt; StartTicks</c>.</param>
/// <param name="GestureKind">One of <see cref="PathTraversalKind"/>'s named values.</param>
/// <param name="Intensity">Normalized <c>[0, 1]</c> magnitude of this gesture's own movement -- <c>|IntervalSemitones| / 12</c> clamped to <c>[0, 1]</c> for <see cref="PathTraversalKind.Step"/>/<see cref="PathTraversalKind.Leap"/>/<see cref="PathTraversalKind.Sweep"/>/<see cref="PathTraversalKind.Reverse"/>, or <see cref="SpatialGestureResolver.HoldIntensity"/> for <see cref="PathTraversalKind.Hold"/> (see <see cref="SpatialGestureResolver"/> remarks for the disclosed rationale).</param>
public sealed record ResolvedGesture(ModelId Target, long StartTicks, long EndTicks, string GestureKind, double Intensity);
