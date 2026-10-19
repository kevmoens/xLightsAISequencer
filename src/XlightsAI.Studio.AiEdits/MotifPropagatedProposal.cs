using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 077's own output of <see cref="MotifPropagationExpander.Expand"/>: an <see cref="EditProposal"/>
/// whose <see cref="EditProposal.Operations"/> list has been widened (one clone per included occurrence, on
/// top of the original base operations) together with the exact <see cref="EditScope"/> each individual
/// operation is permitted to reach -- "edit-scope intersection validation" (this step's own heading, verbatim)
/// needs a per-operation scope, not one single blanket <see cref="SelectionContext.Scope"/>, because a
/// propagated operation's own permitted scope is a different occurrence's own time/model boundary, never the
/// base proposal's own selection scope.
/// </summary>
/// <param name="Proposal">The widened proposal: the original base operations plus one cloned operation per base operation per included occurrence.</param>
/// <param name="PermittedScopeByOperationId">Maps every operation in <see cref="Proposal"/>'s own <see cref="EditProposal.Operations"/> (by <see cref="StructuredEditOperation.OperationId"/>) to the exact <see cref="EditScope"/> it is permitted to reach -- the base proposal's own selection scope for the original operations, or the matching <see cref="MotifOccurrenceTarget.Scope"/> for a propagated clone.</param>
/// <param name="IncludedOccurrenceIds">Exactly the occurrence ids this proposal was actually expanded for -- echoes <see cref="MotifPropagationSelection.IncludedOccurrenceIds"/>, kept alongside the expanded proposal for display/audit (e.g. a preview showing "propagated to 2 other occurrence(s)").</param>
public sealed record MotifPropagatedProposal(
    EditProposal Proposal,
    IReadOnlyDictionary<string, EditScope> PermittedScopeByOperationId,
    IReadOnlyList<Guid> IncludedOccurrenceIds);
