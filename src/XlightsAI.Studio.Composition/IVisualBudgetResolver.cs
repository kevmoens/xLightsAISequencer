using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 068 ("Create configurable visual density and focus budget"). Required acceptance,
/// verbatim: "Clutter fixture is reduced while preserving focal musical event." Pure and deterministic --
/// given the same candidates (in any input order) and the same budget/window, <see cref="Resolve"/> always
/// returns the same output order and the same decisions. Resolves no actual effect-layer/recipe compilation
/// itself (that remains Step 071's own, later scope: "Compose and resolve overlapping effects with
/// layer/priority strategies") -- this step only decides which candidate gestures fit inside a measurable
/// visibility budget before any later step compiles them into concrete effects.
/// </summary>
public interface IVisualBudgetResolver
{
    /// <summary>
    /// Resolves every candidate's own admission outcome against <paramref name="budget"/> over
    /// <paramref name="evaluationWindow"/>. Candidates are considered in <see cref="IRolePrecedenceResolver"/>
    /// order (highest precedence first) -- a lower-precedence candidate is the one trimmed or dropped when
    /// two or more compete for the same budget headroom, never the other way around. The core process
    /// flowchart's own named emergency step, verbatim: "Drop/shorten lower priority support."
    /// </summary>
    /// <exception cref="CompositionInvalidException">
    /// <paramref name="budget"/> fails <see cref="VisualBudget.Validate"/>, <paramref name="evaluationWindow"/>'s
    /// own <c>StartTicks</c> is not strictly less than its <c>EndTicks</c>, any candidate's own <see cref="VisualBudgetCandidate.Window"/>
    /// is similarly invalid, any candidate's own <see cref="VisualBudgetCandidate.Brightness"/> falls outside
    /// <c>[0,1]</c>, or any candidate's own <see cref="MusicalRoleAssignment.PrecedenceTier"/> is unrecognized.
    /// </exception>
    VisualBudgetResolution Resolve(
        IReadOnlyList<VisualBudgetCandidate> candidates,
        VisualBudget budget,
        TimeWindow evaluationWindow);
}
