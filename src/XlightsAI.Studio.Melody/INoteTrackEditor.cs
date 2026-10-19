namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 049 ("Implement editable piano roll and low-confidence markers"). Section 3 proposes
/// <c>Task&lt;MelodyTrack&gt; CorrectAsync(Guid trackId, NoteEvent corrected, CancellationToken ct)</c>
/// verbatim; this interface deliberately deviates from that bare signature (disclosed architecture
/// deviation, same category as Step 048's <c>IMidiTranscriptionBackend</c> vs. the proposed
/// <c>IMelodyAnalyzer</c>): this step's own required failure/edge case, verbatim, is "Out-of-scope,
/// locked or stale revision must be rejected without partial mutation" — none of "out-of-scope,"
/// "locked," or "stale revision" can be expressed, let alone enforced, by a bare
/// <c>(Guid, NoteEvent, CancellationToken)</c> tuple with no revision token and no scope. Every method
/// here instead takes a revision-aware command (<see cref="CorrectNoteCommand"/>/
/// <see cref="SetNoteLockedCommand"/>), the exact same relationship
/// <c>XlightsAI.Studio.Editing.IEditTransactionService.ApplyAsync(SequenceDocument, EditCommand, ...)</c>
/// already has to PHASE-03's own identically-worded cross-cutting rule.
/// </summary>
public interface INoteTrackEditor
{
    /// <summary>
    /// Begins editing <paramref name="analyzedTrack"/>: persists it as both
    /// <see cref="MelodyTrackRevisionState.Original"/> and <see cref="MelodyTrackRevisionState.Current"/>
    /// at <see cref="MelodyTrackRevisionState.Revision"/> zero. Throws
    /// <see cref="InvalidOperationException"/> if a revision state already exists for
    /// <paramref name="analyzedTrack"/>'s own <see cref="MelodyTrack.Id"/> — never silently overwrites
    /// an in-progress correction session (Section 4: "never lose original transcription").
    /// </summary>
    Task<MelodyTrackRevisionState> BeginEditingAsync(MelodyTrack analyzedTrack, GestureClassificationSettings classificationSettings, CancellationToken ct);

    /// <summary>Loads the current revision state for <paramref name="trackId"/>, or <see langword="null"/> if <see cref="BeginEditingAsync"/> was never called for it.</summary>
    Task<MelodyTrackRevisionState?> GetStateAsync(Guid trackId, CancellationToken ct);

    /// <summary>
    /// Applies <paramref name="command"/>: replaces the existing note (matched by
    /// <see cref="CorrectNoteCommand.Corrected"/>'s own <see cref="NoteEvent.Id"/>) with the corrected
    /// pitch/timing, marks it <see cref="NoteEvent.UserCorrected"/>, and reclassifies gestures
    /// ("regenerate only dependent phrases," this step's own required acceptance — see
    /// <see cref="NoteTrackEditor"/>'s own doc comment for why a full, deterministic
    /// re-classification pass already satisfies this without literally only recomputing a sub-range).
    /// </summary>
    /// <exception cref="NoteCorrectionRejectedException">Stale revision, unknown note id, out-of-scope, or locked — no mutation occurs.</exception>
    Task<MelodyTrackRevisionState> CorrectAsync(Guid trackId, CorrectNoteCommand command, CancellationToken ct);

    /// <summary>Toggles one note's own locked bit. Never itself rejected as <see cref="NoteEditRejectionReason.Locked"/> (same "the lock toggle is not gated by the lock it sets" precedent as <c>SetEffectLocked</c>); stale-revision and unknown-note-id are still enforced.</summary>
    /// <exception cref="NoteCorrectionRejectedException">Stale revision or unknown note id — no mutation occurs.</exception>
    Task<MelodyTrackRevisionState> SetNoteLockedAsync(Guid trackId, SetNoteLockedCommand command, CancellationToken ct);
}
