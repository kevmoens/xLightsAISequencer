using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 070: one planned transition/build/silence/release/phrase-ending gesture over
/// <see cref="Window"/>, targeting every prop permitted at plan time. Deliberately a flat intensity ramp
/// (<see cref="StartIntensity"/> -&gt; <see cref="EndIntensity"/>) rather than PHASE-10.md Section 3's own
/// proposed <c>PlannedGesture</c> (<c>GestureType</c>/<c>RecipeId</c> string fields) -- disclosed as an
/// architecture decision in <c>implementation-evidence/PHASE-10/STEP-070.md</c>: concrete xLights effect
/// recipe/layer compilation remains Step 071's own later, disclosed scope ("Compose and resolve overlapping
/// effects with layer/priority strategies").
/// </summary>
/// <param name="Id">A fresh identity minted by <see cref="TransitionRecipePlanner"/> for this one gesture.</param>
/// <param name="Kind">Which reusable semantic recipe produced this gesture.</param>
/// <param name="Window">The time span this gesture occupies.</param>
/// <param name="TargetProps">Every prop this gesture targets -- for <see cref="TransitionKind.Blackout"/> this is always every permitted prop supplied to the planner (this step's own required acceptance, verbatim: "Musical break can intentionally darken all permitted props").</param>
/// <param name="StartIntensity">The intensity (in <c>[0,1]</c>) at <see cref="TimeWindow.StartTicks"/>.</param>
/// <param name="EndIntensity">The intensity (in <c>[0,1]</c>) at <see cref="TimeWindow.EndTicks"/>.</param>
public sealed record TransitionGesture(
    Guid Id,
    TransitionKind Kind,
    TimeWindow Window,
    IReadOnlyList<ModelId> TargetProps,
    double StartIntensity,
    double EndIntensity)
{
    /// <summary>
    /// Validates this gesture's own invariants -- this step's own required failure/edge case realized as a
    /// defensive check against direct record construction (not only against <see cref="TransitionRecipePlanner"/>'s
    /// own output): "Clean machine and unsupported runtime must produce user-readable diagnostics."
    /// </summary>
    /// <exception cref="CompositionInvalidException">
    /// <see cref="Window"/> is not <c>StartTicks &lt; EndTicks</c>, <see cref="TargetProps"/> is empty or
    /// contains a duplicate, <see cref="StartIntensity"/>/<see cref="EndIntensity"/> is non-finite or outside
    /// <c>[0,1]</c>, <see cref="Kind"/> is not a defined <see cref="TransitionKind"/>, or <see cref="Kind"/> is
    /// <see cref="TransitionKind.Blackout"/> with a non-zero <see cref="StartIntensity"/>/<see cref="EndIntensity"/>.
    /// </exception>
    public void Validate()
    {
        if (!Enum.IsDefined(Kind))
        {
            throw new CompositionInvalidException($"Unsupported transition kind '{Kind}'.");
        }

        if (Window.StartTicks >= Window.EndTicks)
        {
            throw new CompositionInvalidException(
                $"Transition gesture window must satisfy StartTicks < EndTicks (was {Window.StartTicks}..{Window.EndTicks}).");
        }

        if (TargetProps.Count == 0)
        {
            throw new CompositionInvalidException(
                "A transition gesture must target at least one permitted prop (none were available -- a clean machine with no layout imported cannot be planned).");
        }

        if (TargetProps.Distinct().Count() != TargetProps.Count)
        {
            throw new CompositionInvalidException("A transition gesture's TargetProps must not contain a duplicate.");
        }

        ValidateUnitFraction(StartIntensity, nameof(StartIntensity));
        ValidateUnitFraction(EndIntensity, nameof(EndIntensity));

        if (Kind == TransitionKind.Blackout && (StartIntensity != 0.0 || EndIntensity != 0.0))
        {
            throw new CompositionInvalidException(
                $"A {nameof(TransitionKind.Blackout)} gesture must darken every target prop to intensity 0 (was {StartIntensity}..{EndIntensity}).");
        }
    }

    private static void ValidateUnitFraction(double value, string fieldName)
    {
        if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
        {
            throw new CompositionInvalidException($"{fieldName} must be a finite number in [0,1] (was {value}).");
        }
    }
}
