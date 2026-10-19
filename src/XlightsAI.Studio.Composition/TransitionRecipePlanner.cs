namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="ITransitionRecipePlanner"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-070.md</c> for the full rationale.</summary>
public sealed class TransitionRecipePlanner : ITransitionRecipePlanner
{
    /// <summary>
    /// Deterministic (StartIntensity, EndIntensity) recipe table, one entry per <see cref="TransitionKind"/>.
    /// <see cref="TransitionKind.Blackout"/> is always <c>(0, 0)</c> -- this step's own required acceptance,
    /// verbatim: "Musical break can intentionally darken all permitted props." Every other kind stays
    /// strictly above <c>0</c> and strictly below <c>1</c>, so no non-blackout recipe is ever mistaken for an
    /// intentional silence or a literal maximum-brightness claim.
    /// </summary>
    private static readonly IReadOnlyDictionary<TransitionKind, (double Start, double End)> RecipeByKind =
        new Dictionary<TransitionKind, (double, double)>
        {
            [TransitionKind.Build] = (0.15, 0.90),
            [TransitionKind.Blackout] = (0.0, 0.0),
            [TransitionKind.Transition] = (0.40, 0.40),
            [TransitionKind.Release] = (0.85, 0.20),
            [TransitionKind.PhraseEnding] = (0.30, 0.05),
        };

    public TransitionPlan Plan(IReadOnlyList<TransitionRecipeRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            throw new CompositionInvalidException("At least one TransitionRecipeRequest is required to plan a TransitionPlan.");
        }

        var gestures = new List<TransitionGesture>(requests.Count);
        foreach (var request in requests)
        {
            gestures.Add(PlanOne(request));
        }

        return new TransitionPlan(Guid.NewGuid(), gestures);
    }

    private static TransitionGesture PlanOne(TransitionRecipeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.PermittedProps);

        if (!Enum.IsDefined(request.Kind))
        {
            throw new CompositionInvalidException($"Unsupported transition kind '{request.Kind}'.");
        }

        if (request.Window.StartTicks >= request.Window.EndTicks)
        {
            throw new CompositionInvalidException(
                $"Transition request window must satisfy StartTicks < EndTicks (was {request.Window.StartTicks}..{request.Window.EndTicks}).");
        }

        if (request.PermittedProps.Count == 0)
        {
            throw new CompositionInvalidException(
                "At least one permitted prop is required to plan a transition gesture -- a clean machine with " +
                "no layout imported yet has none, and must be reported clearly rather than planned as a silent no-op.");
        }

        if (request.PermittedProps.Distinct().Count() != request.PermittedProps.Count)
        {
            throw new CompositionInvalidException("A transition request's PermittedProps must not contain a duplicate.");
        }

        var (start, end) = RecipeByKind[request.Kind];
        return new TransitionGesture(Guid.NewGuid(), request.Kind, request.Window, request.PermittedProps, start, end);
    }
}
