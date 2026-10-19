namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 071's own layer/priority resolution outcome for one <see cref="ChoreographyCandidate"/>,
/// the direct realization of this step's own implementation guidance: "Combine overlapping role plans
/// into a single scoped candidate; validate collisions/layers/locks and demonstrate purposeful
/// coordination."
/// </summary>
public enum ChoreographyResolutionDecision
{
    /// <summary>Admitted at its natural default layer (0) -- no competing effect occupied that layer over an overlapping window on the same target.</summary>
    Accepted,

    /// <summary>Admitted, but moved to a higher, still-free layer because a higher-precedence effect already occupied layer 0 (or lower layers) over an overlapping window on the same target -- the "layer strategy" named in this step's own heading.</summary>
    Reassigned,

    /// <summary>Not admitted at all -- every layer up to <see cref="ChoreographyCompiler.MaxLayers"/> was already occupied by a higher-precedence effect over an overlapping window on the same target.</summary>
    Dropped,
}
