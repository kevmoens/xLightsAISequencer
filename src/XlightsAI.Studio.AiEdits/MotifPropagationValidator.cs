using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Default <see cref="IMotifPropagationValidator"/>. See interface doc for ownership/scope.
///
/// <para>
/// Required acceptance (Step 077, verbatim): "Locked/out-of-scope effects cannot change even with
/// malicious proposal." Every operation -- original or propagated -- is checked against its own
/// <see cref="MotifPropagatedProposal.PermittedScopeByOperationId"/> entry only; a propagated clone can
/// never be rescued by widening it against the base proposal's own scope, because
/// <see cref="MotifPropagationExpander.Expand"/> never records that mapping for a propagated operation in
/// the first place. All-or-nothing: any single failing gate makes <see cref="ProposalValidation.Allowed"/>
/// <see langword="false"/> for the whole widened proposal, the same "reject... without partial mutation"
/// rule (Step 077's own required failure/edge case) every PHASE-03/PHASE-11 gate in this repository already
/// enforces -- a caller never applies "the occurrences that passed" while silently dropping the ones that
/// did not.
/// </para>
/// </summary>
public sealed class MotifPropagationValidator : IMotifPropagationValidator
{
    private readonly IEffectLockPolicy _lockPolicy;

    public MotifPropagationValidator(IEffectLockPolicy lockPolicy)
    {
        ArgumentNullException.ThrowIfNull(lockPolicy);
        _lockPolicy = lockPolicy;
    }

    public ProposalValidation Validate(MotifPropagatedProposal propagated, SequenceDocument document)
    {
        ArgumentNullException.ThrowIfNull(propagated);
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();
        var warnings = new List<string>();
        var proposal = propagated.Proposal;

        if (proposal.Operations.Count == 0)
        {
            errors.Add("Proposal contains no operations; there is nothing to apply.");
        }

        if (proposal.BaseRevision != document.Revision)
        {
            errors.Add($"Proposal's base revision {proposal.BaseRevision} does not match the document's current revision {document.Revision}.");
        }

        foreach (var operation in proposal.Operations)
        {
            if (!ProposalApplier.KnownOperationKinds.Contains(operation.Kind))
            {
                errors.Add($"Operation '{operation.OperationId}' names an unknown kind '{operation.Kind}'.");
                continue;
            }

            if (!propagated.PermittedScopeByOperationId.TryGetValue(operation.OperationId, out var permittedScope))
            {
                errors.Add($"Operation '{operation.OperationId}' has no recorded permitted scope; it cannot be validated and is rejected.");
                continue;
            }

            // "Edit-scope intersection validation" -- every operation's own Range/Targets must be a
            // subset of its own recorded permitted scope exactly (PHASE-11 Section 2: an operation can
            // never widen the time window or model set a user actually selected/approved for that one
            // occurrence).
            var outOfScopeTargets = operation.Targets.Where(t => !permittedScope.Models.Contains(t)).ToArray();
            if (outOfScopeTargets.Length > 0)
            {
                errors.Add($"Operation '{operation.OperationId}' targets {string.Join(", ", outOfScopeTargets.Select(t => t.Value))}, which {(outOfScopeTargets.Length == 1 ? "is" : "are")} outside its own permitted occurrence scope's prop set.");
            }

            if (operation.Range.StartTicks < permittedScope.Time.StartTicks || operation.Range.EndTicks > permittedScope.Time.EndTicks)
            {
                errors.Add($"Operation '{operation.OperationId}' time window [{operation.Range.StartTicks}, {operation.Range.EndTicks}) extends outside its own permitted occurrence scope's time window [{permittedScope.Time.StartTicks}, {permittedScope.Time.EndTicks}).");
            }

            try
            {
                var operationScope = new EditScope(operation.Range, operation.Targets.ToHashSet(), RespectLocks: permittedScope.RespectLocks);
                _lockPolicy.AssertEditable(document, operationScope);
            }
            catch (EditCommandRejectedException ex) when (ex.Reason == EditRejectionReason.Locked)
            {
                errors.Add($"Operation '{operation.OperationId}' reaches a locked effect: {ex.Message}");
            }

            if (!document.Effects.Any(e => operation.Targets.Contains(e.Target) && e.StartTicks < operation.Range.EndTicks && e.EndTicks > operation.Range.StartTicks))
            {
                warnings.Add($"Operation '{operation.OperationId}' currently overlaps no effect in the document; it would be a no-op.");
            }
        }

        return new ProposalValidation(errors.Count == 0, errors, warnings);
    }
}
