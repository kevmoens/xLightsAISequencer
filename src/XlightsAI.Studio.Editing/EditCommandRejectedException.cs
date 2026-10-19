namespace XlightsAI.Studio.Editing;

/// <summary>
/// Thrown by <see cref="IEditTransactionService.ApplyAsync"/> when an <see cref="EditCommand"/> fails
/// any validation gate (<see cref="EditRejectionReason"/>). Always thrown before any
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> mutation is constructed or persisted — the
/// identical "no partial mutation" rule <c>EffectPlanInvalidException</c> already enforces for Step
/// 012's own factory, applied here to Phase 03's command pipeline.
/// </summary>
public sealed class EditCommandRejectedException : Exception
{
    public EditCommandRejectedException(EditRejectionReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public EditRejectionReason Reason { get; }
}
