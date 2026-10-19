using XlightsAI.Studio.Composition;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Section 3's own proposed <c>Candidate</c> record, narrowed for this step's own scope.
/// <b>Architecture deviation (disclosed, additive-by-later-step):</b> the spec's own proposed signature
/// is <c>Candidate(Guid Id, Guid RunId, int Attempt, int Seed, string SnapshotHash, ChoreographyPlan
/// Plan, QualityReport? Report)</c>. Neither <c>ChoreographyPlan</c> nor <c>QualityReport</c> exists
/// anywhere in this repository yet -- PHASE-10's own Steps 069-071 already disclosed (STEP-071.md) that
/// the spec's own proposed <c>ChoreographyPlan</c> was narrowed to the concrete
/// <see cref="XlightsAI.Studio.Composition.ChoreographyCandidate"/> shape that step actually needed, and
/// <c>IChoreographyQualityEvaluator</c>/<c>QualityReport</c> are Step 081's own named scope, not this
/// step's. Rather than declare two dead fields this step can never populate (a <c>Plan</c> field with no
/// generator yet to fill it, and a <c>Report</c> field with no evaluator yet to fill it) --
/// <c>CLAUDE.md</c>'s own "no half-finished implementations" rule -- this step's own
/// <see cref="CandidateAttempt"/> carries exactly the fields its own required acceptance text needs
/// ("hashes, seeds and state transitions"): <see cref="SnapshotHash"/> stands in for the plan's own
/// identity/reproducibility proof (Section 7 cross-cutting test #1: "Same seed + inputs yields
/// reproducible candidate"), and <see cref="Violations"/> stands in for a hard-gate rejection outcome
/// (this step's own required failure/edge case: "Malformed proposal... must not auto-commit") without
/// inventing a <c>QualityReport</c> shape Step 081 has not yet designed. Step 080 (seeded generator) and
/// Step 081 (quality evaluator) are expected to extend this record additively, the same way Step 078
/// extended Steps 072-077's own services with new optional constructors rather than breaking them.
/// </summary>
/// <param name="Id">This attempt's own stable identity.</param>
/// <param name="RunId">The owning <see cref="LoopRun.Id"/>.</param>
/// <param name="Attempt">This attempt's 0-based ordinal within its run -- unique per <see cref="RunId"/>; re-saving the same <see cref="RunId"/>/<see cref="Attempt"/> pair overwrites (idempotent resume), it never appends a duplicate.</param>
/// <param name="Seed">The deterministic seed this specific attempt was generated from (PHASE-12 Section 7 cross-cutting test #1).</param>
/// <param name="SnapshotHash">A stable hash of this attempt's own generated content -- two attempts built from the same <see cref="Seed"/> and the same run inputs must produce the same <see cref="SnapshotHash"/>.</param>
/// <param name="Status">This attempt's own lifecycle state at the moment it was last saved.</param>
/// <param name="Violations">Hard-gate violation messages, if any. A non-empty list means this attempt must never become <see cref="LoopRun.BestCandidateId"/> and must never contribute to a committed document -- this step's own required failure/edge case, verbatim: "Malformed proposal... must not auto-commit."</param>
/// <param name="RecordedAtUtc">When this attempt was last (re)saved.</param>
/// <param name="Gestures">
/// PHASE-12 Step 080 ("Build seeded candidate generator constrained to selected gesture family"): the
/// actual structured content <see cref="SnapshotHash"/> is a hash of -- this step's own required
/// acceptance, verbatim: "Same seed + inputs yields same structured candidate." Added additively,
/// defaulted to <see langword="null"/>, after Step 079's own already-accepted <c>CandidateAttempt</c>
/// shape (every Step-079-era call site that never set this field keeps compiling and keeps meaning
/// exactly what it meant before -- "no generator result carried yet," not a defect). A violating attempt
/// (non-empty <see cref="Violations"/>) typically carries an empty list here (nothing was safe to
/// generate); a clean attempt with no violations is expected to carry at least one gesture, but that
/// cross-field rule is <see cref="DeterministicGestureCandidateGenerator"/>'s own output contract, not
/// enforced by this record's own <see cref="Validate"/> -- doing so here would retroactively invalidate
/// every already-accepted Step 079 fixture/test that constructs a clean, non-violating attempt with no
/// <see cref="Gestures"/> at all (Step 079's own scope predates this field's existence).
/// </param>
public sealed record CandidateAttempt(
    Guid Id,
    Guid RunId,
    int Attempt,
    int Seed,
    string SnapshotHash,
    RefinementStatus Status,
    IReadOnlyList<string> Violations,
    DateTimeOffset RecordedAtUtc,
    IReadOnlyList<TransitionGesture>? Gestures = null)
{
    /// <exception cref="RefinementRunInvalidException">
    /// <see cref="Id"/> or <see cref="RunId"/> is empty; <see cref="Attempt"/> is negative; or
    /// <see cref="SnapshotHash"/> is blank.
    /// </exception>
    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new RefinementRunInvalidException("A CandidateAttempt's Id must not be empty.");
        }

        if (RunId == Guid.Empty)
        {
            throw new RefinementRunInvalidException($"CandidateAttempt '{Id}' RunId must not be empty.");
        }

        if (Attempt < 0)
        {
            throw new RefinementRunInvalidException($"CandidateAttempt '{Id}' Attempt must not be negative (was {Attempt}).");
        }

        if (string.IsNullOrWhiteSpace(SnapshotHash))
        {
            throw new RefinementRunInvalidException($"CandidateAttempt '{Id}' SnapshotHash must not be blank.");
        }

        ArgumentNullException.ThrowIfNull(Violations);

        // Step 080's own additive field: when present, every gesture must independently satisfy its own
        // already-accepted PHASE-10 invariants (TransitionGesture.Validate()) -- defense in depth against
        // a malformed record constructed directly, not only against DeterministicGestureCandidateGenerator's
        // own output. Absent entirely (null), as every Step 079-era attempt already is, this is a no-op.
        if (Gestures is not null)
        {
            foreach (var gesture in Gestures)
            {
                ArgumentNullException.ThrowIfNull(gesture);
                gesture.Validate();
            }
        }
    }
}
