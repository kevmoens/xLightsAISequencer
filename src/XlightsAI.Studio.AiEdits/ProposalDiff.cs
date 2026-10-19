using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 076 ("Implement dry-run proposals, before/after diff and Accept/Reject"), implementation
/// guidance verbatim: "Diff proposals against immutable document and show changed effect count/models/
/// time." Computed by <see cref="IEditProposalDryRunService.Preview"/> against a sandbox candidate
/// document; never mutates the document it was computed from.
/// </summary>
/// <param name="ChangedEffectCount">The number of distinct effects this proposal would change, across every operation.</param>
/// <param name="AffectedModels">Every distinct model targeted by a changed effect, ordered deterministically.</param>
/// <param name="AffectedTime">
/// The tightest half-open window spanning every changed effect's own current <c>[StartTicks, EndTicks)</c>;
/// falls back to the union of every operation's own requested <see cref="StructuredEditOperation.Range"/>
/// when <see cref="ChangedEffectCount"/> is zero (a validator-permitted but currently-no-op proposal still
/// reports a meaningful time window for the preview UI).
/// </param>
public sealed record ProposalDiff(
    int ChangedEffectCount,
    IReadOnlyList<ModelId> AffectedModels,
    TimeWindow AffectedTime);
