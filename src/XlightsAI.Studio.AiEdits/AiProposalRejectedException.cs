namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Thrown by <see cref="HttpStructuredOutputAiPlanner.ProposeAsync"/> whenever an optional provider's
/// response fails any untrusted-input gate (<see cref="AiProposalRejectionReason"/>). Mirrors
/// <see cref="XlightsAI.Studio.Editing.EditCommandRejectedException"/>'s own shape exactly: always
/// thrown before any <see cref="EditProposal"/> is constructed or returned -- no partial/half-valid
/// proposal ever crosses this boundary, and no <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/>
/// is ever mutated by this failure (Step 075's own required failure/edge case, verbatim: "Invalid
/// input, cancellation or stale persisted state must not corrupt the project").
/// </summary>
public sealed class AiProposalRejectedException : Exception
{
    public AiProposalRejectedException(AiProposalRejectionReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public AiProposalRejectedException(AiProposalRejectionReason reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    public AiProposalRejectionReason Reason { get; }
}
