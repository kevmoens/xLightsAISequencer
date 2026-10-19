using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 091 ("Assemble accepted phrase candidates with transition-aware overlap/gap handling")
/// own realization of PHASE-14.md Section 3's proposed
/// <c>IWholeSongAssembler.Assemble(WholeSongPlan plan, SequenceDocument baseDocument)</c> -- narrowed to
/// take an explicit, caller-ordered phrase list plus one explicit <see cref="PhraseSeamRequest"/> per
/// consecutive pair, rather than a <c>WholeSongPlan</c> (which, per Step 090's own disclosed ADR, does not
/// yet carry phrase-plan ids or transitions). See <see cref="PhraseAssemblyExhaustedException"/>'s own doc
/// comment for why.
/// </summary>
public interface IPhraseSeamAssembler
{
    /// <summary>
    /// Assembles <paramref name="phrasesInOrder"/> into <paramref name="baseDocument"/>, resolving each
    /// consecutive pair's own seam per <paramref name="seamRequests"/>. Required acceptance, verbatim:
    /// "No unintended timing holes, conflicts or truncated effects" -- every gap/overlap this method
    /// leaves unresolved is always disclosed via <see cref="PhraseSeamPlan.Warnings"/>, never silent.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="baseDocument"/>, <paramref name="phrasesInOrder"/>, <paramref name="seamRequests"/> or <paramref name="allowedScope"/> is <see langword="null"/>, or any element is <see langword="null"/>.</exception>
    /// <exception cref="PhraseAssemblyInvalidException">
    /// <paramref name="phrasesInOrder"/> is empty; any candidate fails its own <see cref="PhraseAssemblyCandidate.Validate"/>;
    /// two candidates share the same <see cref="PhraseAssemblyCandidate.PhraseId"/>; the candidates' own
    /// <see cref="PhraseAssemblyCandidate.Window"/>s are not strictly ascending and non-overlapping in the
    /// supplied order; any effect's target or time window falls outside <paramref name="allowedScope"/>;
    /// <paramref name="seamRequests"/> does not contain exactly one entry per consecutive phrase pair in
    /// the supplied order; or a <see cref="SeamStrategy.Hold"/>/<see cref="SeamStrategy.Bridge"/> request
    /// is made against a seam where an effect actually bleeds across the phrase boundary.
    /// </exception>
    /// <exception cref="EditCommandRejectedException">
    /// <paramref name="allowedScope"/> reaches an already-locked effect in <paramref name="baseDocument"/>
    /// -- the entire assembly is rejected before any candidate is resolved.
    /// </exception>
    /// <exception cref="PhraseAssemblyExhaustedException">
    /// An assembled effect still collides with another effect on the same target/layer after every
    /// available layer has been tried. No document is committed.
    /// </exception>
    PhraseAssemblyResult Assemble(
        SequenceDocument baseDocument,
        IReadOnlyList<PhraseAssemblyCandidate> phrasesInOrder,
        IReadOnlyList<PhraseSeamRequest> seamRequests,
        EditScope allowedScope);
}
