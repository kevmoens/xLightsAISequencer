using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 077 ("Implement explicit motif propagation and edit-scope intersection validation").
/// Implementation guidance, verbatim: "Apply motif propagation only after preview of all affected
/// occurrences and each target locks/scope permissions; support instance-only choice." This type owns only
/// the first half -- deterministically widening a base <see cref="EditProposal"/> into a
/// <see cref="MotifPropagatedProposal"/> covering every <see cref="MotifPropagationSelection.IncludedOccurrenceIds"/>
/// occurrence, each clone's own <see cref="StructuredEditOperation.Range"/>/<see cref="StructuredEditOperation.Targets"/>
/// set to that occurrence's own <see cref="MotifOccurrenceTarget.Scope"/> exactly (never a wider or narrower
/// window) -- so that "preview of all affected occurrences" (via <see cref="EditProposalDryRunService"/>-style
/// diffing over the widened proposal) and "each target['s] locks/scope permissions" (via
/// <see cref="MotifPropagationValidator"/>) are both provably checking the exact same widened operation set.
/// Never mutates any <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> -- purely a structural
/// transformation over proposals/scopes.
/// </summary>
public static class MotifPropagationExpander
{
    /// <summary>
    /// Widens <paramref name="baseProposal"/> for every occurrence named in
    /// <paramref name="selection"/>'s own <see cref="MotifPropagationSelection.IncludedOccurrenceIds"/>.
    /// An empty selection ("instance-only," this step's own required support) returns a
    /// <see cref="MotifPropagatedProposal"/> whose <see cref="MotifPropagatedProposal.Proposal"/> is exactly
    /// <paramref name="baseProposal"/>'s own operations, unexpanded.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="occurrences"/> contains a duplicate <see cref="MotifOccurrenceTarget.OccurrenceId"/>,
    /// or <paramref name="selection"/> names an occurrence id not present in <paramref name="occurrences"/>
    /// -- an internally inconsistent caller-assembled candidate set, rejected before any operation is cloned.
    /// </exception>
    public static MotifPropagatedProposal Expand(
        EditProposal baseProposal,
        EditScope baseScope,
        IReadOnlyList<MotifOccurrenceTarget> occurrences,
        MotifPropagationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(baseProposal);
        ArgumentNullException.ThrowIfNull(baseScope);
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(selection);

        var occurrenceById = new Dictionary<Guid, MotifOccurrenceTarget>();
        foreach (var occurrence in occurrences)
        {
            if (!occurrenceById.TryAdd(occurrence.OccurrenceId, occurrence))
            {
                throw new ArgumentException(
                    $"Duplicate occurrence id '{occurrence.OccurrenceId}' in the supplied candidate set.",
                    nameof(occurrences));
            }
        }

        foreach (var includedId in selection.IncludedOccurrenceIds)
        {
            if (!occurrenceById.ContainsKey(includedId))
            {
                throw new ArgumentException(
                    $"Selection names occurrence '{includedId}', which is not present in the supplied candidate set.",
                    nameof(selection));
            }
        }

        var operations = new List<StructuredEditOperation>(baseProposal.Operations);
        var scopeByOperationId = new Dictionary<string, EditScope>(StringComparer.Ordinal);
        foreach (var operation in baseProposal.Operations)
        {
            scopeByOperationId[operation.OperationId] = baseScope;
        }

        // Deterministic order -- the same candidate set and selection always produce the same widened
        // operation list/ordering, independent of IncludedOccurrenceIds' own HashSet enumeration order.
        var includedOccurrences = occurrences
            .Where(o => selection.IncludedOccurrenceIds.Contains(o.OccurrenceId))
            .OrderBy(o => o.OccurrenceId)
            .ToList();

        foreach (var occurrence in includedOccurrences)
        {
            foreach (var operation in baseProposal.Operations)
            {
                var propagatedId = $"{operation.OperationId}::occurrence:{occurrence.OccurrenceId}";
                var propagated = operation with
                {
                    OperationId = propagatedId,
                    Range = occurrence.Scope.Time,
                    Targets = occurrence.Scope.Models.OrderBy(m => m.Value, StringComparer.Ordinal).ToList(),
                };
                operations.Add(propagated);
                scopeByOperationId[propagatedId] = occurrence.Scope;
            }
        }

        var explanation = includedOccurrences.Count == 0
            ? baseProposal.Explanation
            : $"{baseProposal.Explanation} (propagated to {includedOccurrences.Count} other occurrence(s))";

        var widenedProposal = baseProposal with
        {
            Id = Guid.NewGuid(),
            Operations = operations,
            Explanation = explanation,
        };

        return new MotifPropagatedProposal(
            widenedProposal,
            scopeByOperationId,
            includedOccurrences.Select(o => o.OccurrenceId).ToList());
    }
}
