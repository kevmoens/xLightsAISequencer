namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 077's own implementation guidance, verbatim: "...support instance-only choice."
/// The user's explicit, per-occurrence decision about which other motif occurrences (beyond the one the
/// base proposal already targets) should receive the same edit. Never inferred from a similarity score or
/// defaulted to "all" -- Section 2's own "explicit human acceptance" rule applies to propagation exactly as
/// it applies to the base proposal itself.
/// </summary>
/// <param name="IncludedOccurrenceIds">
/// Exactly the <see cref="MotifOccurrenceTarget.OccurrenceId"/> values the user explicitly chose to
/// propagate to. Empty means "instance-only" -- apply only to the base proposal's own originally selected
/// occurrence, propagating to none of the other linked occurrences.
/// </param>
public sealed record MotifPropagationSelection(IReadOnlySet<Guid> IncludedOccurrenceIds)
{
    /// <summary>The "instance-only" choice, verbatim from this step's own required-failure heading: no other occurrence is included.</summary>
    public static MotifPropagationSelection InstanceOnly { get; } = new(new HashSet<Guid>());
}
