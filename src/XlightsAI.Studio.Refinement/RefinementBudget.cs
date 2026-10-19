namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 083's own heading, verbatim: "Add retry, time, compute and quality thresholds;
/// cancellation." <see cref="LoopRun.MaxAttempts"/> (Step 079) already realizes the "retry" threshold --
/// this record adds the three remaining named thresholds as a single additive, optional
/// <see cref="LoopRun.Budget"/> field. Every field is independently nullable: a <see langword="null"/>
/// field means that threshold does not bound this run at all (only <see cref="LoopRun.MaxAttempts"/>
/// does), and a <see langword="null"/> <see cref="LoopRun.Budget"/> itself means a Step-079/080/081/082-era
/// run (created before this step existed) still deserializes and executes unchanged -- bounded by
/// <see cref="LoopRun.MaxAttempts"/> alone, exactly as it already was.
/// </summary>
/// <param name="MaxElapsedDuration">The "time" threshold: the maximum wall-clock duration this run may spend actively generating/evaluating attempts, measured from <see cref="LoopRun.StartedAtUtc"/>. <see langword="null"/> means unbounded by time.</param>
/// <param name="MaxComputeUnits">The "compute" threshold: the maximum total <see cref="LoopRun.ComputeUnitsSpent"/> this run may consume. <see langword="null"/> means unbounded by compute cost.</param>
/// <param name="QualityTarget">The "quality" threshold: once the retained best candidate's own <see cref="BestCandidateSelector.AggregateScore"/> reaches this value, the run stops early as a success rather than continuing to spend further attempts/time/compute chasing a marginal improvement. Must be in <c>[0, 1]</c>, the same range <see cref="BestCandidateSelector.AggregateScore"/> itself returns. <see langword="null"/> means no early-success target is configured (the run always runs until some other threshold is hit).</param>
/// <param name="NoImprovementPatience">PHASE-12 Section 3's own architectural design notes, verbatim: "Stop at max attempts, no-improvement patience, cancellation or missing prerequisite." The maximum number of consecutive attempts (tracked by <see cref="LoopRun.ConsecutiveNoImprovementCount"/>) that may fail to improve the retained best candidate before the run gives up early. <see langword="null"/> means no patience limit is configured (only <see cref="LoopRun.MaxAttempts"/>/time/compute bound a non-improving run).</param>
public sealed record RefinementBudget(
    TimeSpan? MaxElapsedDuration = null,
    double? MaxComputeUnits = null,
    double? QualityTarget = null,
    int? NoImprovementPatience = null)
{
    /// <exception cref="RefinementRunInvalidException">
    /// Any set field is out of its own valid range: <see cref="MaxElapsedDuration"/> not strictly positive;
    /// <see cref="MaxComputeUnits"/> not strictly positive; <see cref="QualityTarget"/> outside <c>[0, 1]</c>;
    /// or <see cref="NoImprovementPatience"/> not strictly positive.
    /// </exception>
    public void Validate()
    {
        if (MaxElapsedDuration is { } duration && duration <= TimeSpan.Zero)
        {
            throw new RefinementRunInvalidException(
                $"RefinementBudget MaxElapsedDuration must be strictly positive when set (was {duration}).");
        }

        if (MaxComputeUnits is { } compute && compute <= 0.0)
        {
            throw new RefinementRunInvalidException(
                $"RefinementBudget MaxComputeUnits must be strictly positive when set (was {compute}).");
        }

        if (QualityTarget is { } target && (!double.IsFinite(target) || target < 0.0 || target > 1.0))
        {
            throw new RefinementRunInvalidException(
                $"RefinementBudget QualityTarget must be a finite number in [0,1] when set (was {target}).");
        }

        if (NoImprovementPatience is { } patience && patience <= 0)
        {
            throw new RefinementRunInvalidException(
                $"RefinementBudget NoImprovementPatience must be strictly positive when set (was {patience}).");
        }
    }
}
