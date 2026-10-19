namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 083's own required acceptance, verbatim: "Unproductive loop stops deterministically with
/// reason." Every value here is one concrete, named reason <see cref="RefinementStopEvaluator"/> can
/// return; <see cref="IRefinementOrchestrator"/> turns one into <see cref="LoopRun.StopReason"/>'s own
/// human-readable text (see <see cref="RefinementStopEvaluator.Describe"/>) and the terminal/awaiting
/// <see cref="RefinementStatus"/> a stopped run lands on.
/// </summary>
public enum RefinementStopReason
{
    /// <summary>The "retry" threshold: <see cref="LoopRun.AttemptsMade"/> reached <see cref="LoopRun.MaxAttempts"/>.</summary>
    Exhausted,

    /// <summary>The "time" threshold: elapsed wall-clock duration since <see cref="LoopRun.StartedAtUtc"/> reached <see cref="RefinementBudget.MaxElapsedDuration"/>.</summary>
    TimeBudgetExceeded,

    /// <summary>The "compute" threshold: <see cref="LoopRun.ComputeUnitsSpent"/> reached <see cref="RefinementBudget.MaxComputeUnits"/>.</summary>
    ComputeBudgetExceeded,

    /// <summary>The "quality" threshold: the retained best candidate's own aggregate score reached <see cref="RefinementBudget.QualityTarget"/> -- an early, successful stop.</summary>
    QualityTargetReached,

    /// <summary>PHASE-12 Section 3's own named stop criterion, verbatim: "no-improvement patience." <see cref="LoopRun.ConsecutiveNoImprovementCount"/> reached <see cref="RefinementBudget.NoImprovementPatience"/>.</summary>
    NoImprovementPatienceExceeded,

    /// <summary>PHASE-12 Section 3's own named stop criterion, verbatim: "cancellation." <see cref="LoopRun.CancellationRequested"/> was observed true, or the caller's own <see cref="System.Threading.CancellationToken"/> was already cancelled, before another attempt began.</summary>
    Cancelled,
}
