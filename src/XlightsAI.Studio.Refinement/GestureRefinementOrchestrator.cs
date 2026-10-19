namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 083's own real <see cref="IRefinementOrchestrator"/>: wires the already-accepted
/// <see cref="ISeededCandidateGenerator"/> (Step 080), <see cref="IChoreographyQualityEvaluator"/> (Step
/// 081), <see cref="ITargetedRepairStrategy"/>/<see cref="BestCandidateSelector"/> (Step 082) and
/// <see cref="IRefinementRunStore"/> (Step 079) into one bounded attempt loop, driven end-to-end by the
/// real <see cref="RefinementStatusTransitions"/> state graph (Generating -&gt; Validating -&gt; PreviewReady,
/// repeated) those prior steps already modeled but never exercised with a real caller.
/// <list type="bullet">
/// <item><description><b>Repair before fresh generation (Step 082's own disclosed gap, resolved):</b> after the first attempt, every subsequent attempt first asks <see cref="ITargetedRepairStrategy"/> to target the most recent attempt's own weakest reported dimension. Only when the repair strategy reports nothing left to target (<see cref="RepairOutcome.TargetedMetricName"/> is <see langword="null"/>) does this orchestrator fall back to a fresh seeded variant via <see cref="ISeededCandidateGenerator"/> (seeded from the next ordinal, so it is never a byte-identical repeat) -- "creativity lives in bounded repair loops" (PHASE-12 Section 3), realized as: exploit a known weakness deterministically first, explore a new seed only once there is no known weakness left to exploit.</description></item>
/// <item><description><b>Bounded by every Step 083 threshold:</b> <see cref="RefinementStopEvaluator.Evaluate"/> is consulted before every attempt; the first reason it reports ends the loop -- this step's own required acceptance, verbatim: "Unproductive loop stops deterministically with reason."</description></item>
/// <item><description><b>Never reaches <see cref="RefinementStatus.Accepted"/>:</b> a stopped run with a retained best candidate lands on <see cref="RefinementStatus.AwaitingApproval"/> (with its own claim released, so a later human-approval action can claim it); a stopped run with no valid best lands on <see cref="RefinementStatus.Blocked"/> (terminal); an explicit cancellation lands on <see cref="RefinementStatus.Cancelled"/> (terminal). This realizes PHASE-12 Section 12's own pseudocode, verbatim: "<c>MarkAwaitingHumanApprovalAsync</c>... not a commit to <c>SequenceDocument</c>."</description></item>
/// </list>
/// </summary>
public sealed class GestureRefinementOrchestrator : IRefinementOrchestrator
{
    /// <summary>
    /// This orchestrator's own disclosed, deliberately simple cost model: every attempt (whether a fresh
    /// seeded generation or a targeted repair) consumes exactly one compute unit. No real AI/model call is
    /// made anywhere in this phase yet (<see cref="DeterministicGestureCandidateGenerator"/> is a pure, local
    /// function) -- a uniform per-attempt cost is the smallest model that makes
    /// <see cref="RefinementBudget.MaxComputeUnits"/> a real, independently-configurable threshold distinct
    /// from <see cref="LoopRun.MaxAttempts"/> (e.g. a budget tighter than the attempt cap) without fabricating
    /// telemetry for a cost this phase does not yet actually incur.
    /// </summary>
    private const double CostPerAttempt = 1.0;

    private static readonly TimeSpan DefaultStaleClaimAfter = TimeSpan.FromMinutes(5);

    private readonly IRefinementRunStore _store;
    private readonly ISeededCandidateGenerator _generator;
    private readonly ITargetedRepairStrategy _repair;
    private readonly IChoreographyQualityEvaluator _evaluator;
    private readonly ChoreographyQualityContext _context;
    private readonly TimeSpan _staleClaimAfter;

    public GestureRefinementOrchestrator(
        IRefinementRunStore store,
        ISeededCandidateGenerator generator,
        ITargetedRepairStrategy repair,
        IChoreographyQualityEvaluator evaluator,
        ChoreographyQualityContext context,
        TimeSpan? staleClaimAfter = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _repair = repair ?? throw new ArgumentNullException(nameof(repair));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _staleClaimAfter = staleClaimAfter ?? DefaultStaleClaimAfter;
    }

    public async Task<LoopRun> ExecuteAsync(Guid runId, CancellationToken ct)
    {
        var claim = await _store.TryClaimAsync(runId, _staleClaimAfter, ct).ConfigureAwait(false)
            ?? throw new RefinementRunInvalidException(
                $"Run '{runId}' could not be claimed: it does not exist, is already terminal, or is actively held by another live claim.");

        var run = claim.Run;
        var claimToken = claim.Token;

        var (best, lastAttempt, lastReport) = await ReconstructProgressAsync(run, ct).ConfigureAwait(false);

        while (true)
        {
            if (ct.IsCancellationRequested || run.CancellationRequested)
            {
                return await StopAsync(run, claimToken, RefinementStopReason.Cancelled, best).ConfigureAwait(false);
            }

            var reloaded = await _store.LoadRunAsync(runId, CancellationToken.None).ConfigureAwait(false);
            if (reloaded is { CancellationRequested: true })
            {
                return await StopAsync(run, claimToken, RefinementStopReason.Cancelled, best).ConfigureAwait(false);
            }

            var elapsed = run.StartedAtUtc is { } started ? DateTimeOffset.UtcNow - started : TimeSpan.Zero;
            var bestScore = best is { } b ? BestCandidateSelector.AggregateScore(b.Report) : (double?)null;
            var stopReason = RefinementStopEvaluator.Evaluate(run, elapsed, bestScore);
            if (stopReason is { } reason)
            {
                return await StopAsync(run, claimToken, reason, best).ConfigureAwait(false);
            }

            if (run.Status != RefinementStatus.Validating)
            {
                run = await _store.SaveRunStateAsync(
                    run with { Status = RefinementStatus.Generating, StartedAtUtc = run.StartedAtUtc ?? DateTimeOffset.UtcNow },
                    claimToken,
                    ct).ConfigureAwait(false);
            }

            CandidateAttempt attempt;
            try
            {
                attempt = lastAttempt is null
                    ? await _generator.GenerateAsync(run, null, ct).ConfigureAwait(false)
                    : await NextAttemptAsync(run, lastAttempt, lastReport!, ct).ConfigureAwait(false);
            }
            catch (RefinementRunInvalidException ex)
            {
                // A missing-prerequisite precondition (e.g. LoopRun.GestureFamily never selected) -- PHASE-12
                // Section 3's own named stop criterion, verbatim: "missing prerequisite." Nothing was
                // generated, so there is nothing to score/save; the run is blocked, not merely exhausted.
                var blocked = run with { Status = RefinementStatus.Blocked, StopReason = $"MissingPrerequisite: {ex.Reason}" };
                blocked = await _store.SaveRunStateAsync(blocked, claimToken, CancellationToken.None).ConfigureAwait(false);
                return blocked;
            }

            run = await _store.SaveRunStateAsync(run with { Status = RefinementStatus.Validating }, claimToken, ct).ConfigureAwait(false);

            var report = await _evaluator.EvaluateAsync(run, attempt, _context, ct).ConfigureAwait(false);
            var finalizedAttempt = attempt with
            {
                Status = report.Violations.Count == 0 ? RefinementStatus.PreviewReady : RefinementStatus.Rejected,
            };
            await _store.SaveAttemptAsync(runId, claimToken, finalizedAttempt, ct).ConfigureAwait(false);

            var updatedBest = BestCandidateSelector.SelectBest(best, finalizedAttempt, report);
            var improved = updatedBest is { } candidateBest && (best is null || candidateBest.Attempt.Id != best.Value.Attempt.Id);

            run = run with
            {
                Status = RefinementStatus.PreviewReady,
                AttemptsMade = run.AttemptsMade + 1,
                ComputeUnitsSpent = run.ComputeUnitsSpent + CostPerAttempt,
                ConsecutiveNoImprovementCount = improved ? 0 : run.ConsecutiveNoImprovementCount + 1,
                BestCandidateId = updatedBest?.Attempt.Id ?? run.BestCandidateId,
            };
            run = await _store.SaveRunStateAsync(run, claimToken, ct).ConfigureAwait(false);

            best = updatedBest;
            lastAttempt = finalizedAttempt;
            lastReport = report;
        }
    }

    public Task CancelAsync(Guid runId, CancellationToken ct) => _store.RequestCancellationAsync(runId, ct);

    /// <summary>
    /// "Repair before fresh generation" (see this class's own remarks): targets <paramref name="last"/>'s
    /// own weakest reported dimension when one exists; falls back to a fresh seeded variant (ordinal
    /// advanced from <paramref name="last"/>) only when nothing is left to repair.
    /// </summary>
    private async Task<CandidateAttempt> NextAttemptAsync(LoopRun run, CandidateAttempt last, QualityReport lastReport, CancellationToken ct)
    {
        var repairOutcome = await _repair.RepairAsync(run, last, lastReport, _context, ct).ConfigureAwait(false);
        return repairOutcome.TargetedMetricName is not null
            ? repairOutcome.Repaired
            : await _generator.GenerateAsync(run, last, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes an in-progress run's own in-memory best/last-attempt tracking from what is already persisted
    /// -- <see cref="LoopRun.BestCandidateId"/> and <see cref="LoopRun.AttemptsMade"/> survive a claim/resume
    /// (Step 079's own already-accepted mechanism); re-evaluating a persisted attempt's own
    /// <see cref="QualityReport"/> is cheap and deterministic (<see cref="GestureQualityEvaluator"/> is a
    /// pure function), so it is recomputed here rather than persisting a <c>Report</c> field onto
    /// <see cref="CandidateAttempt"/> itself (Step 082's own disclosed gap, STEP-082.md Section 7, left
    /// unresolved by design -- no later step's own acceptance text has yet required that exact persisted
    /// shape).
    /// </summary>
    private async Task<((CandidateAttempt Attempt, QualityReport Report)? Best, CandidateAttempt? LastAttempt, QualityReport? LastReport)>
        ReconstructProgressAsync(LoopRun run, CancellationToken ct)
    {
        if (run.AttemptsMade == 0)
        {
            return (null, null, null);
        }

        var attempts = await _store.LoadAttemptsAsync(run.Id, ct).ConfigureAwait(false);
        if (attempts.Count == 0)
        {
            return (null, null, null);
        }

        var mostRecent = attempts[^1];
        var mostRecentReport = await _evaluator.EvaluateAsync(run, mostRecent, _context, ct).ConfigureAwait(false);

        (CandidateAttempt Attempt, QualityReport Report)? best = null;
        if (run.BestCandidateId is { } bestId)
        {
            var bestAttempt = attempts.FirstOrDefault(a => a.Id == bestId);
            if (bestAttempt is not null)
            {
                var bestReport = bestAttempt.Id == mostRecent.Id
                    ? mostRecentReport
                    : await _evaluator.EvaluateAsync(run, bestAttempt, _context, ct).ConfigureAwait(false);
                best = (bestAttempt, bestReport);
            }
        }

        return (best, mostRecent, mostRecentReport);
    }

    /// <summary>
    /// Lands <paramref name="run"/> on its own terminal/awaiting stop state for <paramref name="reason"/> --
    /// always using <see cref="CancellationToken.None"/> for the persistence itself, since finalizing a stop
    /// (including a cancellation-triggered one) must not itself be abortable by the very token that triggered
    /// it. When the resulting status is not terminal (<see cref="RefinementStatus.AwaitingApproval"/>), the
    /// claim is explicitly released so a later human-approval action (a future step) can claim this run
    /// again -- <see cref="IRefinementRunStore.SaveRunStateAsync"/> only auto-releases on a terminal status.
    /// </summary>
    private async Task<LoopRun> StopAsync(
        LoopRun run,
        Guid claimToken,
        RefinementStopReason reason,
        (CandidateAttempt Attempt, QualityReport Report)? best)
    {
        var status = reason == RefinementStopReason.Cancelled
            ? RefinementStatus.Cancelled
            : best is not null ? RefinementStatus.AwaitingApproval : RefinementStatus.Blocked;

        var finalized = run with
        {
            Status = status,
            StopReason = RefinementStopEvaluator.Describe(reason),
            CancellationRequested = status == RefinementStatus.Cancelled,
        };

        finalized = await _store.SaveRunStateAsync(finalized, claimToken, CancellationToken.None).ConfigureAwait(false);

        if (!RefinementStatusTransitions.IsTerminal(finalized.Status))
        {
            await _store.ReleaseClaimAsync(finalized.Id, claimToken, CancellationToken.None).ConfigureAwait(false);
            finalized = await _store.LoadRunAsync(finalized.Id, CancellationToken.None).ConfigureAwait(false) ?? finalized;
        }

        return finalized;
    }
}
