namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.10-11; architecture §8b): one promoted, mechanically-verified craft rule —
/// the Hypotheses-to-Theories recipe's own output shape: a human-readable statement, the coverage/
/// confidence numbers that justified promotion, and, where the rule is expressible as code, a stable id
/// a future deterministic validator can dispatch on by name (architecture §8b: "Survivors get promoted
/// twice — into prompt content, and into a deterministic validator"). This type only carries the
/// *shape*; wiring a real validator that calls back into <see cref="CraftRuleInducer"/>'s predicates is
/// explicitly Phase 4's own scope (build plan), not implemented here.
/// </summary>
/// <param name="Id">Stable identifier a future validator dispatches on (e.g. <c>"two-two-four-phrase-pattern"</c>).</param>
/// <param name="Statement">Human-readable prose — what goes into prompt content for the Director.</param>
/// <param name="ApplicableInstanceCount">How many real instances in the corpus this rule's predicate was even evaluable against (its denominator).</param>
/// <param name="TrueInstanceCount">How many of those instances the predicate actually held for.</param>
/// <param name="SongCount">How many distinct corpus songs contributed at least one applicable instance — the promotion threshold requires a minimum spread across songs, not just raw instance count from one outlier song.</param>
/// <param name="HasCodePredicate">Whether this rule was checked by a real, callable predicate (<see cref="CraftRuleInducer"/>'s own internal delegate) rather than evaluated only narratively — every rule in this builder's output is <see langword="true"/> here; the field exists so a future hand-authored, non-mechanically-checked rule can still use this same shape honestly.</param>
public sealed record CraftRule(
    string Id,
    string Statement,
    int ApplicableInstanceCount,
    int TrueInstanceCount,
    int SongCount,
    bool HasCodePredicate)
{
    public double Confidence => ApplicableInstanceCount == 0 ? 0 : TrueInstanceCount / (double)ApplicableInstanceCount;
}
