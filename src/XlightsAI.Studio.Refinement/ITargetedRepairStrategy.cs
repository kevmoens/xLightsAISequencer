namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 082's own heading, verbatim: "Implement targeted repair strategy and preserve best-so-far
/// candidate." Not named by PHASE-12.md Section 3's own proposed C# signatures (that section proposes
/// <c>ICandidateGenerator</c>/<c>IChoreographyQualityEvaluator</c>/<c>IRefinementOrchestrator</c> only) --
/// this interface is a disclosed, additive narrowing of PHASE-12 Section 3's own architectural design
/// notes, verbatim: "Use targeted repair strategy for weakest failing metric (timing, density, pitch
/// motion, rest, repeated motif)." A repair strategy consumes an already-scored <see cref="QualityReport"/>
/// (Step 081's own output) and produces exactly one new attempt whose own gesture content was mutated
/// along a single named dimension -- this step's own required acceptance, verbatim: "Repair only addresses
/// reported dimension and respects locks."
/// </summary>
public interface ITargetedRepairStrategy
{
    /// <summary>
    /// Selects the single weakest reported dimension in <paramref name="report"/> and applies a narrow,
    /// deterministic mutation addressing only that dimension to <paramref name="candidate"/>'s own
    /// gestures, producing a new <see cref="CandidateAttempt"/>. Performs no I/O and consults no store --
    /// persisting the result is the caller's own responsibility. Never mutates <paramref name="candidate"/>
    /// itself (this step's own implementation guidance, verbatim: "compare without corrupting prior
    /// candidate").
    /// </summary>
    /// <param name="run">The owning run -- <see cref="LoopRun.Scope"/> bounds every repaired gesture exactly as it bounds a freshly generated one.</param>
    /// <param name="candidate">
    /// The attempt to repair. <see cref="CandidateAttempt.Gestures"/> may be <see langword="null"/> or
    /// empty (a malformed/rejected upstream generation) -- this step's own required failure/edge case,
    /// verbatim: "Malformed proposal... must not auto-commit." A malformed candidate cannot be repaired
    /// (there is no gesture content to mutate) and is returned unchanged.
    /// </param>
    /// <param name="report"><paramref name="candidate"/>'s own already-computed <see cref="QualityReport"/> (<see cref="QualityReport.CandidateId"/> must equal <see cref="CandidateAttempt.Id"/>).</param>
    /// <param name="context">Real analysis inputs (locked effects, in particular) the repair must respect -- the same shape <see cref="IChoreographyQualityEvaluator"/> consumes.</param>
    Task<RepairOutcome> RepairAsync(LoopRun run, CandidateAttempt candidate, QualityReport report, ChoreographyQualityContext context, CancellationToken ct);
}
