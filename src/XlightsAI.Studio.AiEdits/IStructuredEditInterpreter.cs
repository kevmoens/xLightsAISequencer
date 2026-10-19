using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11.md Section 3's own proposed boundary ("deterministic commands (brightness, reverse, follow
/// lead, restrict targets, replace recipe) without a model when possible"): parses one line of known,
/// offline instruction text into a validated <see cref="EditProposal"/> against an already-captured
/// <see cref="AiEdits.SelectionContext"/> — no network call, no optional AI planner, no token usage
/// (Step 073's own required acceptance, verbatim: "UI quick actions work offline and no token usage").
///
/// <para>
/// Declared against <see cref="AiEdits.SelectionContext"/> (not PHASE-11.md Section 3's own proposed
/// <c>EditContext</c>) per Step 072's own ADR, which narrowed that larger proposed shape down to the
/// selection-identity-only <see cref="AiEdits.SelectionContext"/> this project actually persists; this
/// interpreter only needs the already-validated scope/revision a <see cref="AiEdits.SelectionContext"/>
/// already carries, not a later step's resolved note/gesture read model.
/// </para>
/// </summary>
public interface IStructuredEditInterpreter
{
    /// <summary>
    /// Parses <paramref name="instruction"/> into a single-operation <see cref="EditProposal"/>, or
    /// rejects the attempt entirely. No partial/half-valid <see cref="EditProposal"/> is ever
    /// constructed or returned — every gate below runs to completion before any proposal is built, and
    /// no <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> is ever mutated by this method (Step
    /// 073's own required failure/edge case, verbatim: "Invalid input, cancellation or stale persisted
    /// state must not corrupt the project").
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="instruction"/>, <paramref name="context"/>, or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was already canceled.</exception>
    /// <exception cref="EditCommandRejectedException">
    /// <see cref="EditRejectionReason.StaleRevision"/> when <paramref name="context"/>'s own captured
    /// revision no longer matches <paramref name="document"/>'s current revision;
    /// <see cref="EditRejectionReason.UnrecognizedInstruction"/> when <paramref name="instruction"/> is
    /// blank, matches no known command grammar, or names a value outside that command's own supported
    /// vocabulary/range; <see cref="EditRejectionReason.Locked"/> when <paramref name="context"/>'s own
    /// scope reaches a locked effect.
    /// </exception>
    EditProposal ParseKnownCommand(
        string instruction,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default);
}
