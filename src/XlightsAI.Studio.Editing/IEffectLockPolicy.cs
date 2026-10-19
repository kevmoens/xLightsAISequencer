using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Section 3 proposed contract: asserts that every effect an <see cref="EditScope"/>
/// actually reaches inside <paramref name="document"/> is unlocked, or throws. Kept as its own
/// interface — rather than inlined into <see cref="IEditTransactionService"/> — because Step 022
/// ("lock semantics for approved effects") is explicitly a separate numbered step that will extend
/// this same policy (e.g. persisted lock audit, UI lock toggle) without reshaping the command
/// pipeline that depends on it starting Step 019.
/// </summary>
public interface IEffectLockPolicy
{
    /// <exception cref="EditCommandRejectedException">
    /// <paramref name="scope"/>.<see cref="EditScope.RespectLocks"/> is <see langword="true"/> and at
    /// least one effect reached by <paramref name="scope"/> has <see cref="EffectInstance.Locked"/>
    /// set to <see langword="true"/>.
    /// </exception>
    void AssertEditable(SequenceDocument document, EditScope scope);
}
