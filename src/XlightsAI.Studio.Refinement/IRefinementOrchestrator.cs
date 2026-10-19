namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Section 3's own proposed signature, verbatim: <c>IRefinementOrchestrator { Task&lt;LoopRun&gt;
/// ExecuteAsync(Guid runId, CancellationToken ct); Task CancelAsync(Guid runId, CancellationToken ct); }</c>.
/// Realized in full by this step ("Add retry, time, compute and quality thresholds; cancellation") -- the
/// spec's own proposed shape needed no narrowing. Step 082's own disclosed gap (STEP-082.md Section 7):
/// "No <c>IRefinementOrchestrator</c> drives repair selection... or enforces <c>MaxAttempts</c>/time/cost
/// budgets automatically yet. That orchestration... [is] Step 083's own named scope" -- this interface, and
/// <see cref="GestureRefinementOrchestrator"/>, are that disclosed scope, realized.
/// </summary>
public interface IRefinementOrchestrator
{
    /// <summary>
    /// Runs <paramref name="runId"/>'s own bounded refinement loop (PHASE-12 Section 12's own
    /// bounded-refinement pseudocode, realized): claims the run, repeatedly produces and scores the next
    /// attempt (a targeted repair of the most recent attempt when one is possible, otherwise a fresh seeded
    /// candidate), retains the best non-violating candidate so far, and stops as soon as
    /// <see cref="RefinementStopEvaluator.Evaluate"/> reports a reason -- this step's own required
    /// acceptance, verbatim: "Unproductive loop stops deterministically with reason." Never transitions the
    /// run past <see cref="RefinementStatus.AwaitingApproval"/> itself -- reaching <see cref="RefinementStatus.Accepted"/>
    /// always requires a distinct, later, human-approval action (owned by a future step), never this method.
    /// </summary>
    /// <param name="runId">The already-created (<see cref="IRefinementRunStore.CreateAsync"/>) run to execute.</param>
    /// <param name="ct">
    /// Cooperative cancellation for this specific call. Checked once at the top of every attempt iteration
    /// (alongside the persisted <see cref="LoopRun.CancellationRequested"/> flag) -- cancelling between
    /// iterations stops the run cleanly (<see cref="RefinementStatus.Cancelled"/>, claim released); cancelling
    /// mid-iteration propagates as <see cref="OperationCanceledException"/> and leaves the run's claim intact
    /// but resumable once its heartbeat goes stale (PHASE-12 Section 7 cross-cutting negative case:
    /// "Cancellation: propagate <c>CancellationToken</c>... leave resumable or safely rolled-back job state").
    /// </param>
    /// <exception cref="RefinementRunInvalidException"><paramref name="runId"/> does not exist, is already terminal, or is actively held by another live claim.</exception>
    Task<LoopRun> ExecuteAsync(Guid runId, CancellationToken ct);

    /// <summary>
    /// Requests that <paramref name="runId"/> stop before its next attempt -- see
    /// <see cref="IRefinementRunStore.RequestCancellationAsync"/>. Does not itself wait for, or guarantee
    /// the timing of, the run actually stopping; a concurrently running <see cref="ExecuteAsync"/> call
    /// observes and honors this between attempts.
    /// </summary>
    Task CancelAsync(Guid runId, CancellationToken ct);
}
