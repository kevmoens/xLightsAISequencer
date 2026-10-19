using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>Default <see cref="ISelectionContextCaptureService"/>. See interface doc for the gate order/ownership.</summary>
public sealed class SelectionContextCaptureService : ISelectionContextCaptureService
{
    private readonly IEffectLockPolicy _lockPolicy;

    public SelectionContextCaptureService(IEffectLockPolicy lockPolicy)
    {
        ArgumentNullException.ThrowIfNull(lockPolicy);
        _lockPolicy = lockPolicy;
    }

    public SelectionContext Capture(
        SequenceDocument document,
        DocumentRevision expectedRevision,
        EditScope scope,
        IReadOnlyList<string> instrumentTrackIds,
        IReadOnlyList<Guid> phraseIds,
        IReadOnlyList<Guid> spatialPathIds,
        IReadOnlyList<Guid> noteIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(instrumentTrackIds);
        ArgumentNullException.ThrowIfNull(phraseIds);
        ArgumentNullException.ThrowIfNull(spatialPathIds);
        ArgumentNullException.ThrowIfNull(noteIds);

        // Gate 1: stale revision -- the selection was built against a document view that no longer
        // matches the document's current state (PHASE-11 Section 2: "reject... stale revision").
        if (expectedRevision.Value != document.Revision)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.StaleRevision,
                $"Expected revision {expectedRevision.Value} does not match the document's current revision {document.Revision}; no selection was captured.");
        }

        // Gate 2: a well-formed, non-empty half-open interval (PHASE-01 Section 2's canonical
        // [start,end) rule) -- an empty/inverted window can never actually reach anything.
        if (scope.Time.EndTicks <= scope.Time.StartTicks)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.InvalidDuration,
                $"Selection interval [{scope.Time.StartTicks}, {scope.Time.EndTicks}) is empty or inverted; no selection was captured.");
        }

        // Gate 3: at least one in-scope prop -- EditScope.Models' own doc comment: "An empty Models set
        // means 'no model is in scope' (reject everything)". A selection naming zero props is not a
        // meaningful selection to snapshot.
        if (scope.Models.Count == 0)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.OutOfScope,
                "Selection names no in-scope prop; no selection was captured.");
        }

        // Gate 4: every explicitly named effect id (when supplied) must both exist in the document and
        // target a model this same scope already permits -- an internally inconsistent scope (an effect
        // id pinned outside the scope's own model set) is rejected rather than silently narrowed.
        if (scope.EffectIds is not null)
        {
            foreach (var effectId in scope.EffectIds)
            {
                var effect = document.Effects.FirstOrDefault(e => e.Id == effectId);
                if (effect is null)
                {
                    throw new EditCommandRejectedException(
                        EditRejectionReason.OutOfScope,
                        $"Selection names effect '{effectId}', which does not exist in this document; no selection was captured.");
                }

                if (!scope.Models.Contains(effect.Target))
                {
                    throw new EditCommandRejectedException(
                        EditRejectionReason.OutOfScope,
                        $"Selection names effect '{effectId}', which targets '{effect.Target.Value}' outside the selection's own prop set; no selection was captured.");
                }
            }
        }

        // Gate 5: locks -- reuses the already-accepted PHASE-03 policy unmodified rather than
        // re-implementing lock enforcement.
        _lockPolicy.AssertEditable(document, scope);

        return new SelectionContext(
            Guid.NewGuid(),
            new DocumentRevision(document.Revision),
            scope,
            instrumentTrackIds.ToArray(),
            phraseIds.ToArray(),
            spatialPathIds.ToArray(),
            noteIds.ToArray(),
            DateTimeOffset.UtcNow);
    }
}
