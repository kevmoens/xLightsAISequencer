namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 070's own application-orchestration boundary: validates, persists and versions one phrase's
/// <see cref="TransitionPlan"/>. Rejects an invalid plan (<see cref="TransitionPlan.Validate"/> failure)
/// <em>before</em> loading or writing anything -- this step's own required failure/edge case, verbatim:
/// "Clean machine and unsupported runtime must produce user-readable diagnostics."
/// </summary>
public interface ITransitionPlanCatalog
{
    /// <summary>Loads a previously saved plan for <paramref name="phraseId"/>, or <see langword="null"/> if none has ever been saved.</summary>
    Task<TransitionPlanRecord?> GetAsync(Guid phraseId, CancellationToken ct);

    /// <summary>
    /// Saves <paramref name="plan"/> under <paramref name="phraseId"/>, replacing any prior plan for the same
    /// phrase and incrementing its <see cref="TransitionPlanRecord.Version"/>.
    /// </summary>
    /// <exception cref="CompositionInvalidException"><paramref name="plan"/> fails <see cref="TransitionPlan.Validate"/>.</exception>
    Task<TransitionPlanRecord> SaveAsync(Guid phraseId, TransitionPlan plan, CancellationToken ct);
}
