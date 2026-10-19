using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 070: one requested transition/build/silence/release/phrase-ending gesture, the input
/// <see cref="ITransitionRecipePlanner.Plan"/> turns into a deterministic <see cref="TransitionGesture"/>.
/// </summary>
/// <param name="Kind">Which reusable semantic recipe to apply.</param>
/// <param name="Window">The time span the resulting gesture should occupy.</param>
/// <param name="PermittedProps">Every prop currently permitted to be targeted (e.g. not excluded by an unrelated locked effect) -- never empty; an empty set is this step's own disclosed "clean machine" diagnostic case.</param>
public sealed record TransitionRecipeRequest(TransitionKind Kind, TimeWindow Window, IReadOnlyList<ModelId> PermittedProps);
