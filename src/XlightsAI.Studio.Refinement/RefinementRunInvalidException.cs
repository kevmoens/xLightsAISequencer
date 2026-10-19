namespace XlightsAI.Studio.Refinement;

/// <summary>
/// Thrown when a <see cref="LoopRun"/>/<see cref="CandidateAttempt"/>'s own shape is invalid, an illegal
/// <see cref="RefinementStatus"/> transition is attempted (see <see cref="RefinementStatusTransitions"/>),
/// or a store operation would otherwise persist an inconsistent run (e.g. a
/// <see cref="LoopRun.BestCandidateId"/> pointing at an attempt that itself carries violations). Thrown
/// before any file is written -- this step's own required failure/edge case, verbatim: "Malformed
/// proposal, no-improvement and exhausted retry budget must not auto-commit."
/// </summary>
public sealed class RefinementRunInvalidException : Exception
{
    public RefinementRunInvalidException(string reason) : base(reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
