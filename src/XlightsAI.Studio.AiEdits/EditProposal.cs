namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11.md Section 3's own proposed signature, implemented verbatim: an untrusted-until-approved
/// idea (Section 1 cross-cutting table: "EditProposal — Untrusted/optional AI idea validated before
/// EditTransaction") carrying one or more <see cref="StructuredEditOperation"/>s. Step 073 only ever
/// produces single-operation proposals from a local, deterministic command grammar (no model, no
/// token usage); a later step's optional AI planner can produce multi-operation proposals through the
/// same shape without this record itself changing. Never applied directly to a
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> — Step 076's own later "dry-run proposals,
/// before/after diff and Accept/Reject" owns turning an <see cref="EditProposal"/> into a committed
/// <see cref="XlightsAI.Studio.Editing.EditTransaction"/>.
/// </summary>
/// <param name="Id">Stable identity for this one proposal.</param>
/// <param name="BaseRevision">The <see cref="XlightsAI.Studio.Xlights.SequenceDocument.Revision"/> this proposal was parsed against — any later apply attempt must revalidate against the document's then-current revision.</param>
/// <param name="Operations">One or more bounded, typed operations; never empty.</param>
/// <param name="Explanation">A short, human-readable summary of what this proposal does and why — shown before approval, never silently applied.</param>
public sealed record EditProposal(
    Guid Id,
    long BaseRevision,
    IReadOnlyList<StructuredEditOperation> Operations,
    string Explanation);
