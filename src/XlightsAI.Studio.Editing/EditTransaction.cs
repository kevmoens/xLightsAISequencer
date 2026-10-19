namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Section 3 proposed contract: the immutable audit record of one successfully applied
/// <see cref="EditCommand"/> — before/after revision numbers and serialized before/after patches
/// (Section 3 "Architectural design notes": "persist reversible before/after patches"). Step 019
/// produces exactly one of these per successful <see cref="MoveEffect"/> application; Step 022 owns
/// turning a sequence of these into real undo/redo. <see cref="SerializedBeforePatch"/>/
/// <see cref="SerializedAfterPatch"/> are JSON snapshots of the single affected
/// <see cref="XlightsAI.Studio.Xlights.EffectInstance"/>, not the whole document — the document's own
/// before/after state is always independently reconstructible from
/// <see cref="XlightsAI.Studio.Xlights.ISequenceDocumentStore"/> by <see cref="BeforeRevision"/>/
/// <see cref="AfterRevision"/>, so this record only needs to capture what actually changed.
/// </summary>
public sealed record EditTransaction(
    Guid Id,
    long BeforeRevision,
    long AfterRevision,
    string SerializedBeforePatch,
    string SerializedAfterPatch,
    DateTimeOffset AcceptedUtc);
