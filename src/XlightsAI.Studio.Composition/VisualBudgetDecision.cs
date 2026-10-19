namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 068's own emergency over-budget resolution outcome for one <see cref="VisualBudgetCandidate"/>,
/// the direct realization of the core process flowchart's own "Drop/shorten lower priority support" step.
/// </summary>
public enum VisualBudgetDecision
{
    /// <summary>Admitted unmodified, at its original <see cref="VisualBudgetCandidate.Window"/>.</summary>
    Accepted,

    /// <summary>Admitted, but its window was shortened to a sub-interval so it no longer exceeds <see cref="VisualBudget.MaxConcurrentFocalProps"/>.</summary>
    Trimmed,

    /// <summary>Not admitted at all -- it would exceed <see cref="VisualBudget"/> in a way no trim could resolve, or admitting it would exceed the density/brightness caps.</summary>
    Dropped,
}
