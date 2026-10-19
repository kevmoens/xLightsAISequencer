namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real implementation of <see cref="IModelFeasibilityEvaluator"/>. Pure and synchronous — no file I/O,
/// no ambient clock — the same precedent <c>BeatGridQualityGate</c> already established in
/// <c>XlightsAI.Studio.Rhythm</c> for a phase-local decision gate.
/// </summary>
public sealed class ModelFeasibilityEvaluator : IModelFeasibilityEvaluator
{
    public ModelFeasibilityReport Evaluate(
        Guid id,
        long createdAtTicks,
        IReadOnlyList<ModelFeasibilityCandidate> candidates,
        string decision,
        string? blockerReason)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(decision);

        if (candidates.Count == 0)
        {
            throw new ModelFeasibilityReportInvalidException(
                "A feasibility report requires at least one inspected candidate.");
        }

        foreach (var candidate in candidates)
        {
            ValidateCandidate(candidate);
        }

        var feasibleCount = candidates.Count(c => c.Verdict == ModelFeasibilityVerdict.Feasible);
        var hasExplicitBlocker = !string.IsNullOrWhiteSpace(blockerReason);

        if (feasibleCount < 2 && !hasExplicitBlocker)
        {
            throw new ModelFeasibilityReportInvalidException(
                $"Required acceptance not met: found only {feasibleCount} feasible candidate(s) and no " +
                "explicit blocker reason was supplied. Decision document must include at least two " +
                "feasible alternatives or an explicit blocker (PHASE-06 Step 038, required acceptance).");
        }

        var isBlocked = feasibleCount < 2;

        return new ModelFeasibilityReport(
            id,
            createdAtTicks,
            candidates,
            decision,
            isBlocked,
            isBlocked ? blockerReason : null);
    }

    private static void ValidateCandidate(ModelFeasibilityCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (string.IsNullOrWhiteSpace(candidate.ModelId))
        {
            throw new ModelFeasibilityCandidateInvalidException("A candidate requires a non-empty ModelId.");
        }

        if (string.IsNullOrWhiteSpace(candidate.Backend))
        {
            throw new ModelFeasibilityCandidateInvalidException(
                $"Candidate '{candidate.ModelId}' requires a concrete, named inference backend " +
                "(never a generic 'ONNX' claim).");
        }

        if (candidate.SampleRateHz <= 0)
        {
            throw new ModelFeasibilityCandidateInvalidException(
                $"Candidate '{candidate.ModelId}' has a non-positive SampleRateHz.");
        }

        if (candidate.EstimatedCpuRamMegabytes <= 0)
        {
            throw new ModelFeasibilityCandidateInvalidException(
                $"Candidate '{candidate.ModelId}' has a non-positive EstimatedCpuRamMegabytes.");
        }

        if (candidate.EstimatedCpuSecondsPerSongMinute < 0)
        {
            throw new ModelFeasibilityCandidateInvalidException(
                $"Candidate '{candidate.ModelId}' has a negative EstimatedCpuSecondsPerSongMinute.");
        }

        if (candidate.Citations.Count == 0)
        {
            throw new ModelFeasibilityCandidateInvalidException(
                $"Candidate '{candidate.ModelId}' has no citations; every claim must be inspectable " +
                "(Section 2's 'Verification' rule — do not fabricate a working claim).");
        }

        if (candidate.Verdict == ModelFeasibilityVerdict.Feasible &&
            !(candidate.VerifiedExecutableUnderDotNet && candidate.LicenseRedistributable))
        {
            throw new ModelFeasibilityCandidateInvalidException(
                $"Candidate '{candidate.ModelId}' is marked Feasible but is not both proven executable " +
                "under .NET and license-redistributable — do not assert feasibility beyond what is " +
                "proven (Section 3: 'do not assert that a given model runs under ONNX merely because " +
                "ONNX Runtime has C# bindings').");
        }
    }
}
