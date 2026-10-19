namespace XlightsAI.Studio.Editing;

/// <summary>
/// Why an <see cref="EditCommand"/> was rejected by <see cref="IEditTransactionService.ApplyAsync"/>.
/// Every numbered PHASE-03 step from 018 onward names the same three required-failure categories
/// verbatim ("Out-of-scope, locked, or stale revision must be rejected without partial mutation") —
/// this enum gives tests/callers a stable, non-string way to assert exactly which one fired, the same
/// role <c>XsqExportPolicy</c> plays for export outcomes.
/// </summary>
public enum EditRejectionReason
{
    StaleRevision,
    EffectNotFound,
    OutOfScope,
    Locked,

    /// <summary>
    /// PHASE-03 Step 020 ("Resize duration..."): a <see cref="ResizeEffect"/> whose requested end time
    /// would leave a zero-length or negative-length effect (Step 020 implementation guidance:
    /// "reject zero/negative length").
    /// </summary>
    InvalidDuration,

    /// <summary>
    /// PHASE-03 Step 020 ("...edit supported effect parameters"): a <see cref="SetEffectParameters"/>
    /// change names a parameter key or value the effect's own <see cref="XlightsAI.Studio.Xlights.IEffectRecipeRegistry"/>
    /// recipe does not recognize (Step 020 implementation guidance: "reject... unsupported parameters";
    /// Section 2: "do not invent xLights effect parameters").
    /// </summary>
    UnsupportedParameter,

    /// <summary>
    /// PHASE-03 Step 022 ("Undo/redo transactions..."): <see cref="IEditTransactionService.UndoAsync"/>
    /// was called for a document with an empty undo stack — no applied edit exists to undo. No
    /// document mutation occurs; the caller's document is returned unchanged by definition (there is
    /// nothing to apply).
    /// </summary>
    NothingToUndo,

    /// <summary>
    /// PHASE-03 Step 022: <see cref="IEditTransactionService.RedoAsync"/> was called for a document
    /// with an empty redo stack — either nothing has ever been undone, or a new edit was applied after
    /// the last undo, which invalidates (clears) any pending redo history, the same "stale" case every
    /// real undo/redo implementation must guard: redoing a transaction computed against a document
    /// state that the document has since diverged from would silently reapply a stale patch.
    /// </summary>
    NothingToRedo,

    /// <summary>
    /// PHASE-11 Step 073 ("Implement deterministic command AST for brightness/motion/direction/speed/
    /// replace"): the instruction text given to
    /// <see cref="XlightsAI.Studio.AiEdits.IStructuredEditInterpreter.ParseKnownCommand"/> does not match
    /// any known local command grammar (blank, unrecognized verb, or a recognized verb with a value
    /// outside that verb's own supported vocabulary/range). No <see cref="XlightsAI.Studio.AiEdits.EditProposal"/>
    /// is ever constructed for this reason — Section 3's own "Explain manual alternatives" path, made a
    /// stable, non-string reason the same way every other gate in this enum already is.
    /// </summary>
    UnrecognizedInstruction,
}
