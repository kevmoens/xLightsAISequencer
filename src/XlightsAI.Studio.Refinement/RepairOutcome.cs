namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 082 ("Implement targeted repair strategy and preserve best-so-far candidate"): the
/// result of one <see cref="ITargetedRepairStrategy"/> call. Carries both the repaired attempt and which
/// single reported dimension (if any) the repair actually targeted -- this step's own required
/// acceptance, verbatim: "Repair only addresses reported dimension and respects locks." A caller (or a
/// test) can inspect <see cref="TargetedMetricName"/> to prove that exactly one dimension was addressed,
/// rather than inferring it indirectly from which fields happened to change.
/// </summary>
/// <param name="Repaired">
/// The repaired <see cref="CandidateAttempt"/>. When no targeted mutation was possible (an empty/malformed
/// source candidate, or a dimension this strategy does not know how to mutate), this is the same
/// <see cref="CandidateAttempt"/> instance that was passed in -- a repair attempt never fabricates gesture
/// content, and never mutates the caller's own candidate in place (this step's own implementation
/// guidance, verbatim: "compare without corrupting prior candidate").
/// </param>
/// <param name="TargetedMetricName">
/// The single <see cref="MetricValue.Name"/> this repair addressed, or <see langword="null"/> when no
/// repair was applied (see <see cref="Explanation"/> for why).
/// </param>
/// <param name="Explanation">A human-readable account of what was (or could not be) repaired, and why -- never blank.</param>
public sealed record RepairOutcome(CandidateAttempt Repaired, string? TargetedMetricName, string Explanation);
