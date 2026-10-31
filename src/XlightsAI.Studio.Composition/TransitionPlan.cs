namespace XlightsAI.Studio.Composition;

/// <summary>
/// SUPERSEDED, kept only as reference material — see <see cref="IChoreographyCompiler"/>'s own doc comment
/// for the full "why" (build-plan Phase 10 §3g).
///
/// Historical summary — PHASE-10 Step 070: the full set of planned transition/build/silence/release/phrase-ending gestures for one
/// planning call, mirroring <see cref="VisualStory"/>'s own versioned-wrapper-plus-<c>Validate</c> shape.
/// </summary>
/// <param name="VersionId">A fresh identity minted by <see cref="TransitionRecipePlanner.Plan"/> every time a plan is (re)computed -- never reused across two independently planned plans, even over identical inputs.</param>
/// <param name="Gestures">Every planned gesture, in the same order <see cref="TransitionRecipePlanner.Plan"/> received its own requests.</param>
public sealed record TransitionPlan(Guid VersionId, IReadOnlyList<TransitionGesture> Gestures)
{
    /// <summary>
    /// Validates every gesture before any persistence is attempted -- this step's own required failure/edge
    /// case, verbatim: "Clean machine and unsupported runtime must produce user-readable diagnostics."
    /// </summary>
    /// <exception cref="CompositionInvalidException"><see cref="Gestures"/> is empty, contains a duplicate <see cref="TransitionGesture.Id"/>, or any gesture fails <see cref="TransitionGesture.Validate"/>.</exception>
    public void Validate()
    {
        if (Gestures.Count == 0)
        {
            throw new CompositionInvalidException("A TransitionPlan must contain at least one gesture.");
        }

        var seen = new HashSet<Guid>();
        foreach (var gesture in Gestures)
        {
            if (!seen.Add(gesture.Id))
            {
                throw new CompositionInvalidException($"Duplicate gesture id '{gesture.Id}' in TransitionPlan.");
            }

            gesture.Validate();
        }
    }
}
