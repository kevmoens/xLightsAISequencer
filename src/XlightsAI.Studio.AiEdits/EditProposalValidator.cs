using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>Default <see cref="IEditProposalValidator"/>. See interface doc for ownership/scope.</summary>
public sealed class EditProposalValidator : IEditProposalValidator
{
    private readonly IEffectLockPolicy _lockPolicy;

    public EditProposalValidator(IEffectLockPolicy lockPolicy)
    {
        ArgumentNullException.ThrowIfNull(lockPolicy);
        _lockPolicy = lockPolicy;
    }

    public ProposalValidation Validate(EditProposal proposal, SelectionContext context, SequenceDocument document)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();
        var warnings = new List<string>();

        if (proposal.Operations.Count == 0)
        {
            errors.Add("Proposal contains no operations; there is nothing to apply.");
        }

        if (proposal.BaseRevision != document.Revision)
        {
            errors.Add($"Proposal's base revision {proposal.BaseRevision} does not match the document's current revision {document.Revision}.");
        }

        if (context.BaseDocumentRevision.Value != document.Revision)
        {
            errors.Add($"Selection context's captured revision {context.BaseDocumentRevision.Value} does not match the document's current revision {document.Revision}.");
        }

        foreach (var operation in proposal.Operations)
        {
            if (!ProposalApplier.KnownOperationKinds.Contains(operation.Kind))
            {
                errors.Add($"Operation '{operation.OperationId}' names an unknown kind '{operation.Kind}'.");
                continue;
            }

            var outOfScopeTargets = operation.Targets.Where(t => !context.Scope.Models.Contains(t)).ToArray();
            if (outOfScopeTargets.Length > 0)
            {
                errors.Add($"Operation '{operation.OperationId}' targets {string.Join(", ", outOfScopeTargets.Select(t => t.Value))}, which {(outOfScopeTargets.Length == 1 ? "is" : "are")} outside the selected scope's prop set.");
            }

            if (operation.Range.StartTicks < context.Scope.Time.StartTicks || operation.Range.EndTicks > context.Scope.Time.EndTicks)
            {
                errors.Add($"Operation '{operation.OperationId}' time window [{operation.Range.StartTicks}, {operation.Range.EndTicks}) extends outside the selected scope's time window [{context.Scope.Time.StartTicks}, {context.Scope.Time.EndTicks}).");
            }

            try
            {
                var operationScope = new EditScope(operation.Range, operation.Targets.ToHashSet(), RespectLocks: context.Scope.RespectLocks);
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
