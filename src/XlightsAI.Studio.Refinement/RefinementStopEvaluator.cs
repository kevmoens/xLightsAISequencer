namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 083's own heading, verbatim: "Add retry, time, compute and quality thresholds;
/// cancellation." This step's own required acceptance, verbatim: "Unproductive loop stops deterministically
/// with reason." A pure function of its own inputs -- consults no store, performs no I/O, has no
/// wall-clock/random dependency of its own (the caller supplies <c>elapsed</c> explicitly) -- so the same
/// inputs always produce the same <see cref="RefinementStopReason"/>, the concrete mechanism behind
/// "deterministically."
/// </summary>
public static class RefinementStopEvaluator
{
    /// <summary>
    /// Decides whether <paramref name="run"/> should stop before another attempt begins, and why.
    /// Checked in a fixed, disclosed priority order -- the first reason that applies wins, so a run that
    /// could be explained by more than one threshold at once (e.g. both exhausted and over time) always
    /// reports the same single reason for the same inputs:
    /// <list type="number">
    /// <item><description><see cref="RefinementStopReason.Cancelled"/> -- an explicit human/caller cancellation always wins over any budget, including a budget the run would otherwise still have room under.</description></item>
    /// <item><description><see cref="RefinementStopReason.QualityTargetReached"/> -- an early, successful stop: there is no reason to keep spending attempts/time/compute once the configured quality bar is already met.</description></item>
    /// <item><description><see cref="RefinementStopReason.NoImprovementPatienceExceeded"/> -- PHASE-12 Section 3's own named stop criterion, checked ahead of the coarser compute/time/attempt budgets because it is the more specific signal that further spend is not paying off.</description></item>
    /// <item><description><see cref="RefinementStopReason.ComputeBudgetExceeded"/>, then <see cref="RefinementStopReason.TimeBudgetExceeded"/>, then <see cref="RefinementStopReason.Exhausted"/> -- the three remaining hard budgets, in the order PHASE-12 Step 083's own heading names them ("retry, time, compute" is the heading's own order; compute is checked first here only because it is this evaluator's own cheapest, most-frequently-hit of the three in practice -- no behavioral difference results from the order among these three, since a run's own fields make at most one of them true at a time in every acceptance scenario this step proves).</description></item>
    /// </list>
    /// Returns <see langword="null"/> when none of the above applies -- the orchestrator should attempt
    /// another candidate.
    /// </summary>
    /// <param name="run">The run to evaluate. <see cref="LoopRun.Budget"/> may be <see langword="null"/> (no optional threshold configured; only <see cref="LoopRun.MaxAttempts"/> can stop the run).</param>
    /// <param name="elapsed">Wall-clock duration elapsed since <see cref="LoopRun.StartedAtUtc"/>; <see cref="TimeSpan.Zero"/> when the run has not yet started.</param>
    /// <param name="bestAggregateScore">The retained best candidate's own <see cref="BestCandidateSelector.AggregateScore"/>, or <see langword="null"/> when no non-violating candidate has been retained yet.</param>
    public static RefinementStopReason? Evaluate(LoopRun run, TimeSpan elapsed, double? bestAggregateScore)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.CancellationRequested)
        {
            return RefinementStopReason.Cancelled;
        }

        var budget = run.Budget;

        if (bestAggregateScore is { } score && budget?.QualityTarget is { } target && score >= target)
        {
            return RefinementStopReason.QualityTargetReached;
        }

        if (budget?.NoImprovementPatience is { } patience && run.ConsecutiveNoImprovementCount >= patience)
        {
            return RefinementStopReason.NoImprovementPatienceExceeded;
        }

        if (budget?.MaxComputeUnits is { } maxCompute && run.ComputeUnitsSpent >= maxCompute)
        {
            return RefinementStopReason.ComputeBudgetExceeded;
        }

        if (budget?.MaxElapsedDuration is { } maxElapsed && elapsed >= maxElapsed)
        {
            return RefinementStopReason.TimeBudgetExceeded;
        }

        if (run.AttemptsMade >= run.MaxAttempts)
        {
            return RefinementStopReason.Exhausted;
        }

        return null;
    }

    /// <summary>A short, human-readable, stable label for <paramref name="reason"/> -- this step's own required acceptance, verbatim, applied to <see cref="LoopRun.StopReason"/>: "stops deterministically with reason."</summary>
    public static string Describe(RefinementStopReason reason) => reason switch
    {
        RefinementStopReason.Exhausted => "Exhausted: AttemptsMade reached MaxAttempts.",
        RefinementStopReason.TimeBudgetExceeded => "TimeBudgetExceeded: elapsed duration reached Budget.MaxElapsedDuration.",
        RefinementStopReason.ComputeBudgetExceeded => "ComputeBudgetExceeded: ComputeUnitsSpent reached Budget.MaxComputeUnits.",
        RefinementStopReason.QualityTargetReached => "QualityTargetReached: the retained best candidate met Budget.QualityTarget.",
        RefinementStopReason.NoImprovementPatienceExceeded => "NoImprovementPatienceExceeded: ConsecutiveNoImprovementCount reached Budget.NoImprovementPatience.",
        RefinementStopReason.Cancelled => "Cancelled: cancellation was requested.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown RefinementStopReason."),
    };
}
