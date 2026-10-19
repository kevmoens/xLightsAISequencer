namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 068 ("Create configurable visual density and focus budget"): a measurable visibility
/// budget, declared exactly as PHASE-10.md Section 3's own proposed signature names it, verbatim, field for
/// field. Implementation guidance, verbatim: "Build measurable visibility budget (concurrent focal count,
/// event density, brightness proxy, rest fraction)." Each field is this step's own direct realization of one
/// of those four named measurements.
/// </summary>
/// <param name="MaxConcurrentFocalProps">The maximum number of simultaneously active focal-role gestures permitted at any one instant. <c>0</c> is a valid (deliberate full-blackout) budget; negative is invalid.</param>
/// <param name="MaxMeanBrightness">The maximum arithmetic-mean brightness proxy (in <c>[0,1]</c>) permitted across every admitted gesture.</param>
/// <param name="MaxEventDensityPerSecond">The maximum average admitted-event rate (admitted event count divided by the evaluation window's own duration in seconds) permitted.</param>
/// <param name="DarknessFractionTarget">The target fraction (in <c>[0,1]</c>) of the evaluation window that should remain dark (no admitted gesture covering it) -- a goal <see cref="IVisualBudgetResolver"/> reports against, not itself an admission cap.</param>
public sealed record VisualBudget(
    int MaxConcurrentFocalProps,
    double MaxMeanBrightness,
    double MaxEventDensityPerSecond,
    double DarknessFractionTarget)
{
    /// <summary>
    /// Validates every field before any resolution/persistence is attempted -- this step's own required
    /// failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not corrupt
    /// the project."
    /// </summary>
    /// <exception cref="CompositionInvalidException">Any field is negative, not finite, or a fractional field falls outside <c>[0,1]</c>.</exception>
    public void Validate()
    {
        if (MaxConcurrentFocalProps < 0)
        {
            throw new CompositionInvalidException($"MaxConcurrentFocalProps must not be negative (was {MaxConcurrentFocalProps}).");
        }

        ValidateUnitFraction(MaxMeanBrightness, nameof(MaxMeanBrightness));

        if (!double.IsFinite(MaxEventDensityPerSecond) || MaxEventDensityPerSecond < 0)
        {
            throw new CompositionInvalidException($"MaxEventDensityPerSecond must be a non-negative finite number (was {MaxEventDensityPerSecond}).");
        }

        ValidateUnitFraction(DarknessFractionTarget, nameof(DarknessFractionTarget));
    }

    private static void ValidateUnitFraction(double value, string fieldName)
    {
        if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
        {
            throw new CompositionInvalidException($"{fieldName} must be a finite number in [0,1] (was {value}).");
        }
    }
}
