namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 081 ("Implement quality evaluators: timing, melodic alignment, density, motif reuse,
/// legal effects"), narrowing PHASE-12.md Section 3's own proposed
/// <c>IChoreographyQualityEvaluator.EvaluateAsync(Candidate candidate, CancellationToken ct)</c> the same
/// way Step 080's own <see cref="ISeededCandidateGenerator"/> already narrowed <c>ICandidateGenerator</c>
/// (disclosed ADR, STEP-080.md Section 3): substituting this repository's own <see cref="CandidateAttempt"/>
/// for the spec's own proposed <c>Candidate</c>/<c>ChoreographyPlan</c>, which do not exist here (Step
/// 079's own disclosed ADR). This step's own evaluator additionally needs the owning <see cref="LoopRun"/>
/// (for <see cref="LoopRun.Scope"/>'s permitted time/model boundary -- the "time/model scopes" hard gate
/// this step's own implementation guidance names, verbatim) and an optional
/// <see cref="ChoreographyQualityContext"/> (real analysis inputs for the soft metrics) -- neither of which
/// the spec's own proposed single-parameter signature carries; both are disclosed, additive narrowings,
/// not hidden ones.
/// </summary>
public interface IChoreographyQualityEvaluator
{
    /// <summary>
    /// Scores <paramref name="candidate"/> against <paramref name="run"/>'s own permitted scope and,
    /// where supplied, the real analysis inputs in <paramref name="context"/>. Performs no I/O and consults
    /// no store -- persisting the result is the caller's own responsibility.
    /// </summary>
    /// <param name="run">The owning run -- <see cref="LoopRun.Scope"/> is this evaluator's own source of truth for the "time/model scopes" hard gate.</param>
    /// <param name="candidate">The attempt to score. <see cref="CandidateAttempt.Gestures"/> may be <see langword="null"/> or empty (a malformed/rejected upstream generation) -- this step's own required failure/edge case, verbatim: "Silence... must be handled explicitly."</param>
    /// <param name="context">Optional real analysis inputs for the soft metrics. Pass <see cref="ChoreographyQualityContext.Empty"/> (never <see langword="null"/>) when none are available.</param>
    Task<QualityReport> EvaluateAsync(LoopRun run, CandidateAttempt candidate, ChoreographyQualityContext context, CancellationToken ct);
}
