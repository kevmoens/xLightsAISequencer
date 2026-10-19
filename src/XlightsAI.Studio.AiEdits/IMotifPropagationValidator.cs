using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 077's own heading, verbatim: "...edit-scope intersection validation." Validates a
/// <see cref="MotifPropagatedProposal"/> operation-by-operation against its own recorded
/// <see cref="MotifPropagatedProposal.PermittedScopeByOperationId"/> -- unlike <see cref="IEditProposalValidator"/>,
/// which checks every operation against one single <see cref="SelectionContext.Scope"/>, a widened,
/// multi-occurrence proposal has a different permitted scope per operation (the base proposal's own
/// selection scope for its original operations, a different occurrence's own scope for each propagated
/// clone). Never throws; always returns a complete <see cref="ProposalValidation"/>, the same "show every
/// reason at once" convention <see cref="IEditProposalValidator"/> already established.
/// </summary>
public interface IMotifPropagationValidator
{
    /// <exception cref="ArgumentNullException"><paramref name="propagated"/> or <paramref name="document"/> is <see langword="null"/>.</exception>
    ProposalValidation Validate(MotifPropagatedProposal propagated, SequenceDocument document);
}
