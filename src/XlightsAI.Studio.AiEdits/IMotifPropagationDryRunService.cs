using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 077's own implementation guidance, verbatim: "Apply motif propagation only after preview
/// of all affected occurrences..." Mirrors <see cref="IEditProposalDryRunService"/>'s own sandbox-preview
/// contract exactly, but over a <see cref="MotifPropagatedProposal"/> (one or more occurrences) instead of
/// a single-scope <see cref="EditProposal"/> -- the returned <see cref="EditProposalDryRun.Diff"/> shows the
/// changed effect count/models/time across <em>every</em> included occurrence at once, so a caller can show
/// the user exactly what propagating would affect before any commit.
/// </summary>
public interface IMotifPropagationDryRunService
{
    /// <exception cref="ArgumentNullException"><paramref name="propagated"/> or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="EditProposalRejectedException">
    /// <see cref="IMotifPropagationValidator.Validate"/> reports one or more errors (unknown operation
    /// kind, an operation reaching outside its own occurrence's permitted scope, a locked effect, or a
    /// stale <see cref="EditProposal.BaseRevision"/>) -- no candidate document is ever constructed in this
    /// case.
    /// </exception>
    EditProposalDryRun Preview(MotifPropagatedProposal propagated, SequenceDocument document);
}
