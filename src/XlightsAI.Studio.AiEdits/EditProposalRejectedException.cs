namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Thrown by <see cref="IEditProposalDryRunService.Preview"/> and <see cref="IEditApprovalService.Accept"/>
/// when <see cref="IEditProposalValidator.Validate"/> reports <see cref="ProposalValidation.Allowed"/> as
/// <see langword="false"/>. <see cref="Errors"/> carries every independent reason at once -- Step 076's own
/// required failure/edge case, verbatim: "Clean machine and unsupported runtime must produce user-readable
/// diagnostics." Always thrown before any candidate document is constructed or committed; the caller's own
/// document is left byte-equivalent.
/// </summary>
public sealed class EditProposalRejectedException : Exception
{
    public EditProposalRejectedException(IReadOnlyList<string> errors)
        : base(errors.Count == 0 ? "Proposal was rejected." : string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
