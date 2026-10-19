using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// Default <see cref="INoteTrackEditor"/>. See the interface's own doc comment for the disclosed
/// signature deviation from Section 3's proposed bare <c>CorrectAsync(Guid, NoteEvent, CancellationToken)</c>.
///
/// "Regenerate only dependent phrases" (this step's own required acceptance, verbatim) is satisfied by
/// re-running the existing, deterministic, forward-only <see cref="MelodicContourClassifier"/> over the
/// corrected note list rather than hand-rolling a separate "dirty range" recomputation: that classifier
/// (Step 047) derives each note's own <see cref="MelodicGesture"/> strictly from notes at or before it
/// (<c>MelodicContourClassifier.ClassifyNotes</c> walks left-to-right, tracking only the last confident
/// predecessor) — so correcting note N can only ever change the gestures for note N and notes after it;
/// every gesture for a note strictly before N is provably recomputed to the exact same value it already
/// had (see <c>Step049AcceptanceTests.CorrectAsync_...DoesNotChangeGesturesBeforeTheCorrectedNote</c>),
/// never reclassified from a different, speculative algorithm.
///
/// "Never lose original transcription" (implementation guidance, verbatim) is enforced structurally:
/// <see cref="MelodyTrackRevisionState.Original"/> is set exactly once, by <see cref="BeginEditingAsync"/>,
/// and no method on this type ever writes to it again.
///
/// "Locked" notes (this step's own failure/edge case) are tracked via
/// <see cref="MelodyTrackRevisionState.LockedNoteIds"/>, not a new field on the already-committed,
/// immutable Step 045 <see cref="NoteEvent"/> contract — disclosed architecture decision, same shape as
/// Step 048's "no new field on an already-accepted record" precedent for
/// <see cref="MidiImportSettings"/> vs. <see cref="NoteEvent"/>.
/// </summary>
public sealed class NoteTrackEditor : INoteTrackEditor
{
    private readonly IMelodyTrackRevisionStore _store;

    public NoteTrackEditor(IMelodyTrackRevisionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<MelodyTrackRevisionState> BeginEditingAsync(
        MelodyTrack analyzedTrack, GestureClassificationSettings classificationSettings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(analyzedTrack);
        ArgumentNullException.ThrowIfNull(classificationSettings);

        ct.ThrowIfCancellationRequested();

        var existing = await _store.LoadAsync(analyzedTrack.Id, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"A revision state already exists for track '{analyzedTrack.Id}'; BeginEditingAsync must not be called twice for the same track.");
        }

        // Re-validates defensively (same "never persist without re-checking" precedent as
        // JsonMelodyTrackStore.SaveAsync) -- never persists an Original that is itself invalid.
        var validated = MelodyTrackValidator.Validate(analyzedTrack);

        var state = new MelodyTrackRevisionState(
            validated.Id,
            Original: validated,
            Current: validated,
            Revision: new DocumentRevision(0),
            // A List<Guid> here, not Array.Empty<Guid>(): System.Text.Json always deserializes an
            // IReadOnlyList<T> JSON array back into a List<T>, and xunit's structural Assert.Equal
            // compares collection elements only when both sides share the same concrete runtime
            // collection type -- keeping both sides List<Guid> from the start (confirmed by a real
            // test failure during this step's own implementation: an initial Array.Empty<Guid>() here
            // made a freshly-begun in-memory state compare unequal to the exact same state reloaded
            // from disk) avoids a spurious "equal content, different container type" mismatch.
            LockedNoteIds: new List<Guid>(),
            ClassificationSettings: classificationSettings);

        await _store.SaveAsync(state, ct).ConfigureAwait(false);
        return state;
    }

    public Task<MelodyTrackRevisionState?> GetStateAsync(Guid trackId, CancellationToken ct) =>
        _store.LoadAsync(trackId, ct);

    public async Task<MelodyTrackRevisionState> CorrectAsync(Guid trackId, CorrectNoteCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ct.ThrowIfCancellationRequested();

        var state = await LoadRequiredStateAsync(trackId, ct).ConfigureAwait(false);
        RequireCurrentRevision(state, command.ExpectedRevision, trackId);

        var existingIndex = IndexOfNote(state.Current, command.Corrected.Id);
        if (existingIndex < 0)
        {
            throw new NoteCorrectionRejectedException(
                NoteEditRejectionReason.NoteNotFound,
                $"No note with id '{command.Corrected.Id}' exists in track '{trackId}''s current revision; no edit was applied.");
        }

        var existingNote = state.Current.Notes[existingIndex];

        if (!IsReachedByScope(existingNote, command.Scope))
        {
            throw new NoteCorrectionRejectedException(
                NoteEditRejectionReason.OutOfScope,
                $"Note '{existingNote.Id}' is outside the permitted edit scope; no edit was applied.");
        }

        if (command.Scope.RespectLocks && state.LockedNoteIds.Contains(existingNote.Id))
        {
            throw new NoteCorrectionRejectedException(
                NoteEditRejectionReason.Locked,
                $"Note '{existingNote.Id}' is locked and cannot be corrected.");
        }

        // The corrected (new) boundaries must themselves stay within the permitted scope's own time
        // window -- same "the result must not leave the permitted scope" rule
        // EditTransactionService.ApplyMove/ApplyResize already enforce for a dragged/resized effect.
        if (command.Corrected.StartTicks < command.Scope.Time.StartTicks || command.Corrected.EndTicks > command.Scope.Time.EndTicks)
        {
            throw new NoteCorrectionRejectedException(
                NoteEditRejectionReason.OutOfScope,
                $"Correcting note '{existingNote.Id}' to [{command.Corrected.StartTicks}, {command.Corrected.EndTicks}) would leave the permitted edit scope's time window [{command.Scope.Time.StartTicks}, {command.Scope.Time.EndTicks}); no edit was applied.");
        }

        // NoteEvent's own doc comment, verbatim: "Always 1.0 for a trusted-MIDI or UserCorrected
        // note" -- enforced here, not merely trusted from the caller, so a corrected note's own
        // confidence can never under-report a human's explicit correction.
        var correctedNote = command.Corrected with { UserCorrected = true, Confidence = 1.0 };
        var updatedNotes = state.Current.Notes.ToList();
        updatedNotes[existingIndex] = correctedNote;

        // Pure computation only below this line, before any new state object replaces the existing
        // one and before any persistence. MelodyTrackValidator/MelodicContourClassifier throw
        // MelodyTrackInvalidException for a genuinely invalid corrected note (zero/negative
        // duration, out-of-range pitch/velocity/confidence) -- the previously persisted state is
        // still byte-for-byte untouched at that point ("no partial mutation" applies to domain-level
        // invalidity exactly the same as to a NoteCorrectionRejectedException above).
        var candidate = state.Current with { Notes = updatedNotes, Gestures = Array.Empty<MelodicGesture>() };
        var reclassified = MelodicContourClassifier.Classify(candidate, state.ClassificationSettings);

        var newState = state with
        {
            Current = reclassified,
            Revision = new DocumentRevision(state.Revision.Value + 1),
        };

        await _store.SaveAsync(newState, ct).ConfigureAwait(false);
        return newState;
    }

    public async Task<MelodyTrackRevisionState> SetNoteLockedAsync(Guid trackId, SetNoteLockedCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ct.ThrowIfCancellationRequested();

        var state = await LoadRequiredStateAsync(trackId, ct).ConfigureAwait(false);
        RequireCurrentRevision(state, command.ExpectedRevision, trackId);

        if (IndexOfNote(state.Current, command.NoteId) < 0)
        {
            throw new NoteCorrectionRejectedException(
                NoteEditRejectionReason.NoteNotFound,
                $"No note with id '{command.NoteId}' exists in track '{trackId}''s current revision; no edit was applied.");
        }

        var lockedIds = new HashSet<Guid>(state.LockedNoteIds);
        if (command.Locked)
        {
            lockedIds.Add(command.NoteId);
        }
        else
        {
            lockedIds.Remove(command.NoteId);
        }

        var newState = state with
        {
            LockedNoteIds = lockedIds.ToList(),
            Revision = new DocumentRevision(state.Revision.Value + 1),
        };

        await _store.SaveAsync(newState, ct).ConfigureAwait(false);
        return newState;
    }

    private async Task<MelodyTrackRevisionState> LoadRequiredStateAsync(Guid trackId, CancellationToken ct)
    {
        var state = await _store.LoadAsync(trackId, ct).ConfigureAwait(false);
        return state ?? throw new NoteCorrectionRejectedException(
            NoteEditRejectionReason.NoteNotFound,
            $"No revision state exists for track '{trackId}'; BeginEditingAsync must be called first.");
    }

    private static void RequireCurrentRevision(MelodyTrackRevisionState state, DocumentRevision expectedRevision, Guid trackId)
    {
        if (expectedRevision.Value != state.Revision.Value)
        {
            throw new NoteCorrectionRejectedException(
                NoteEditRejectionReason.StaleRevision,
                $"Expected revision {expectedRevision.Value} does not match track '{trackId}''s current revision {state.Revision.Value}; no edit was applied.");
        }
    }

    private static int IndexOfNote(MelodyTrack track, Guid noteId)
    {
        for (var i = 0; i < track.Notes.Count; i++)
        {
            if (track.Notes[i].Id == noteId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// A note is "reached" by <paramref name="scope"/> either by exact identity
    /// (<see cref="NoteEditScope.NoteIds"/>, when supplied) or by overlapping the scope's half-open
    /// time window -- the same relationship <c>EffectLockPolicy.IsReachedByScope</c> already
    /// establishes for an <c>EffectInstance</c>/<c>EditScope</c>.
    /// </summary>
    private static bool IsReachedByScope(NoteEvent note, NoteEditScope scope)
    {
        if (scope.NoteIds is not null)
        {
            return scope.NoteIds.Contains(note.Id);
        }

        return note.StartTicks < scope.Time.EndTicks && note.EndTicks > scope.Time.StartTicks;
    }
}
