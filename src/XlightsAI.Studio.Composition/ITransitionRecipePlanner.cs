namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 070's own planning boundary ("Implement build, silence, transitions, release and
/// phrase-ending recipes"): turns requested transition kinds/windows/permitted-prop sets into a deterministic
/// <see cref="TransitionPlan"/>. Pure and deterministic -- the same requests always produce the same gestures
/// (excluding identities, which are intentionally fresh every call).
/// </summary>
public interface ITransitionRecipePlanner
{
    /// <summary>
    /// Plans a <see cref="TransitionPlan"/> from <paramref name="requests"/>. Required acceptance, verbatim:
    /// "Musical break can intentionally darken all permitted props" -- every <see cref="TransitionKind.Blackout"/>
    /// request's own resulting gesture targets every prop in that request's own <see cref="TransitionRecipeRequest.PermittedProps"/>,
    /// at intensity <c>0</c>. Implementation guidance, verbatim: "Add transition gestures, builds, blackouts
    /// and resolutions as reusable semantics with deterministic recipe strategies."
    /// </summary>
    /// <exception cref="CompositionInvalidException">
    /// <paramref name="requests"/> is empty, a request's <see cref="TransitionRecipeRequest.Kind"/> is not a
    /// defined <see cref="TransitionKind"/>, its <see cref="TransitionRecipeRequest.PermittedProps"/> is empty
    /// or contains a duplicate, or its <see cref="TransitionRecipeRequest.Window"/> does not satisfy
    /// <c>StartTicks &lt; EndTicks</c> -- thrown before any gesture is produced. This step's own required
    /// failure/edge case, verbatim: "Clean machine and unsupported runtime must produce user-readable
    /// diagnostics."
    /// </exception>
    TransitionPlan Plan(IReadOnlyList<TransitionRecipeRequest> requests);
}
