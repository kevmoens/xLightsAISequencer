using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 072's own proposed boundary: builds a validated <see cref="SelectionContext"/> from a
/// live <see cref="SequenceDocument"/> and a caller-proposed <see cref="EditScope"/>, or rejects the
/// attempt entirely. Kept as its own interface -- not folded into <see cref="ISelectionContextStore"/> --
/// because capture is a pure, revision/lock-aware domain decision (no I/O), while the store is a
/// separate, later persistence step, the same separation <c>IChoreographyCompiler</c> (pure) and
/// <c>ISequenceDocumentStore</c> (I/O) already established in PHASE-10 Step 071.
/// </summary>
public interface ISelectionContextCaptureService
{
    /// <summary>
    /// Validates <paramref name="scope"/> against <paramref name="document"/> and
    /// <paramref name="expectedRevision"/>, then returns a new, immutable <see cref="SelectionContext"/>
    /// snapshot. No partial/half-valid <see cref="SelectionContext"/> is ever constructed or returned --
    /// every gate below runs to completion before any snapshot is built.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="document"/>, <paramref name="scope"/>, or any supplied list is <see langword="null"/>.</exception>
    /// <exception cref="EditCommandRejectedException">
    /// <see cref="EditRejectionReason.StaleRevision"/> when <paramref name="expectedRevision"/> does not
    /// match <paramref name="document"/>'s current revision; <see cref="EditRejectionReason.InvalidDuration"/>
    /// when <paramref name="scope"/>'s own time window is empty or inverted;
    /// <see cref="EditRejectionReason.OutOfScope"/> when <paramref name="scope"/> names no model, or
    /// names an explicit effect id that either does not exist in <paramref name="document"/> or targets a
    /// model outside <paramref name="scope"/>'s own model set; <see cref="EditRejectionReason.Locked"/>
    /// when <paramref name="scope"/> reaches a locked effect and <see cref="EditScope.RespectLocks"/> is
    /// <see langword="true"/>.
    /// </exception>
    SelectionContext Capture(
        SequenceDocument document,
        DocumentRevision expectedRevision,
        EditScope scope,
        IReadOnlyList<string> instrumentTrackIds,
        IReadOnlyList<Guid> phraseIds,
        IReadOnlyList<Guid> spatialPathIds,
        IReadOnlyList<Guid> noteIds);
}
