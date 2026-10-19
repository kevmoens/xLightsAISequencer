namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Section 3's own proposed state machine, verbatim: "Queued -> Generating -> Validating ->
/// PreviewReady/Blocked -> AwaitingApproval -> Accepted/Rejected." Shared by both <see cref="LoopRun"/>
/// (the whole bounded run) and <see cref="CandidateAttempt"/> (one attempt within that run) -- the spec's
/// own proposed C# signatures declare exactly one <c>RefinementStatus</c> enum for both, not two parallel
/// enums, and this step does not narrow that. See <see cref="RefinementStatusTransitions"/> for which
/// transitions a persisted <see cref="LoopRun"/> may actually make.
/// </summary>
public enum RefinementStatus
{
    Queued,
    Generating,
    Validating,
    PreviewReady,
    AwaitingApproval,
    Accepted,
    Rejected,
    Blocked,
    Cancelled,
}
