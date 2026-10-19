using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11.md Section 3's own proposed boundary, implemented against the already-accepted
/// <see cref="SelectionContext"/> (the same narrowing every other PHASE-11 interface already made) rather
/// than the spec's proposed <c>EditContext</c>. Section 3's own flowchart step this interface owns:
/// "Validate schema, IDs, scope and locks" -- run against <em>any</em> <see cref="EditProposal"/>,
/// whether it was produced by <see cref="IStructuredEditInterpreter"/> (Step 073) or
/// <see cref="IOptionalAiPlanner"/> (Step 075). Never throws; always returns a complete
/// <see cref="ProposalValidation"/> so every failing gate can be shown at once.
/// </summary>
public interface IEditProposalValidator
{
    /// <exception cref="ArgumentNullException"><paramref name="proposal"/>, <paramref name="context"/>, or <paramref name="document"/> is <see langword="null"/>.</exception>
    ProposalValidation Validate(EditProposal proposal, SelectionContext context, SequenceDocument document);
}
