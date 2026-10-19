namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 080 ("Build seeded candidate generator constrained to selected gesture family"),
/// narrowing PHASE-12.md Section 3's own proposed
/// <c>ICandidateGenerator.GenerateAsync(LoopRun run, Candidate? prior, CancellationToken ct)</c>: the
/// spec's own proposed <c>Candidate</c> parameter/return type carries a <c>ChoreographyPlan Plan</c>
/// field that, per Step 079's own disclosed ADR (<c>implementation-evidence/PHASE-12/STEP-079.md</c>
/// Section 3.1), does not exist anywhere in this repository -- <see cref="CandidateAttempt"/> is this
/// repository's own narrower, already-accepted stand-in. This interface keeps the spec's own proposed
/// shape (run, optional prior attempt, cancellation token in; one new attempt out) unchanged, substituting
/// only the concrete type name.
/// </summary>
public interface ISeededCandidateGenerator
{
    /// <summary>
    /// Deterministically produces the next <see cref="CandidateAttempt"/> for <paramref name="run"/>:
    /// this step's own required acceptance, verbatim, "Same seed + inputs yields same structured
    /// candidate" -- calling this twice with an unchanged <paramref name="run"/> and
    /// <paramref name="prior"/> must return two attempts whose own <see cref="CandidateAttempt.Seed"/>,
    /// <see cref="CandidateAttempt.SnapshotHash"/> and <see cref="CandidateAttempt.Gestures"/> are
    /// identical. Performs no I/O and consults no store -- persisting the result is the caller's own
    /// responsibility via <see cref="IRefinementRunStore.SaveAttemptAsync"/>.
    /// </summary>
    /// <param name="run">
    /// The owning run. <see cref="LoopRun.GestureFamily"/> must be set -- this step's own heading,
    /// verbatim: "constrained to selected gesture family."
    /// </param>
    /// <param name="prior">
    /// The previously generated attempt for this run, or <see langword="null"/> for the first attempt.
    /// The returned attempt's own <see cref="CandidateAttempt.Attempt"/> ordinal is always
    /// <c>(prior?.Attempt ?? -1) + 1</c> -- this step's own implementation guidance, verbatim: "preserve
    /// lineage links."
    /// </param>
    /// <exception cref="RefinementRunInvalidException"><paramref name="run"/>'s own <see cref="LoopRun.GestureFamily"/> is <see langword="null"/> -- a generation-time precondition, not a persisted-shape defect (see <see cref="LoopRun.GestureFamily"/>'s own remarks).</exception>
    Task<CandidateAttempt> GenerateAsync(LoopRun run, CandidateAttempt? prior, CancellationToken ct);
}
