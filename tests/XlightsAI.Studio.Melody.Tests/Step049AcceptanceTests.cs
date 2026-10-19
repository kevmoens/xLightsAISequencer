using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// PHASE-07 Step 049 — "Implement editable piano roll and low-confidence markers." Required
/// acceptance (verbatim): "User corrections persist and regenerate only dependent phrases." Failure/
/// edge case (verbatim): "Out-of-scope, locked or stale revision must be rejected without partial
/// mutation."
/// </summary>
public sealed class Step049AcceptanceTests : IDisposable
{
    private const string Hash = "deadbeef";
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step049-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static NoteEvent Note(long startTicks, long endTicks, int midiNote, double confidence = 0.9) =>
        new(Guid.NewGuid(), startTicks, endTicks, midiNote, Velocity: 0.8, confidence, UserCorrected: false);

    private static MelodyTrack ClassifiedTrack(params NoteEvent[] notes)
    {
        var track = new MelodyTrack(Guid.NewGuid(), Hash, notes, []);
        return MelodicContourClassifier.Classify(track, GestureClassificationSettings.Default);
    }

    private NoteTrackEditor CreateEditor() =>
        new(new JsonMelodyTrackRevisionStore(_rootDirectory));

    private static NoteEditScope FullTrackScope(MelodyTrack track, bool respectLocks = true) =>
        new(new TimeWindow(0, track.Notes.Max(n => n.EndTicks) + 1), RespectLocks: respectLocks);

    [Fact]
    public async Task Step049_Acceptance_Passes_UserCorrectionsPersistAndRegenerateOnlyDependentPhrases()
    {
        // Section 12's own worked example: C4-D4-E4-G4-E4-D4-C4.
        var notes = new[]
        {
            Note(0L, 4_000_000L, 60),           // C4
            Note(4_200_000L, 7_900_000L, 62),   // D4
            Note(8_000_000L, 11_500_000L, 64),  // E4 -- this one will be corrected to F4 (65)
            Note(11_600_000L, 16_000_000L, 67), // G4
            Note(16_100_000L, 19_000_000L, 64), // E4
            Note(19_100_000L, 22_000_000L, 62), // D4
            Note(22_100_000L, 26_000_000L, 60), // C4
        };
        var analyzed = ClassifiedTrack(notes);
        var gesturesBeforeCorrection = analyzed.Gestures.ToList();

        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);
        Assert.Equal(0, begun.Revision.Value);
        // Note: compared field-by-field (Id/hash/Notes/Gestures), not via a whole-record
        // Assert.Equal(analyzed, begun.Original) -- a record's compiler-generated equality compares
        // its List<T> properties by reference, not by content, so two structurally identical but
        // distinct list instances (e.g. one re-built by MelodyTrackValidator.Validate's own
        // defensive re-validation) would otherwise report a false mismatch.
        Assert.Equal(analyzed.Id, begun.Original.Id);
        Assert.Equal(analyzed.Notes, begun.Original.Notes);
        Assert.Equal(analyzed.Gestures, begun.Original.Gestures);
        Assert.Equal(analyzed.Id, begun.Current.Id);
        Assert.Equal(analyzed.Notes, begun.Current.Notes);
        Assert.Equal(analyzed.Gestures, begun.Current.Gestures);

        var corrected = notes[2] with { MidiNote = 65 }; // E4 -> F4, same timing
        var command = new CorrectNoteCommand(begun.Revision, FullTrackScope(analyzed), corrected);
        var afterCorrection = await editor.CorrectAsync(begun.TrackId, command, CancellationToken.None);

        // Persists (required acceptance, verbatim: "User corrections persist"): revision advanced,
        // the corrected note's new pitch is reflected, and it is now flagged UserCorrected/confidence 1.0.
        Assert.Equal(1, afterCorrection.Revision.Value);
        var persistedNote = afterCorrection.Current.Notes.Single(n => n.Id == notes[2].Id);
        Assert.Equal(65, persistedNote.MidiNote);
        Assert.True(persistedNote.UserCorrected);
        Assert.Equal(1.0, persistedNote.Confidence);

        // "never lose original transcription": Original is untouched (still has the analyzer's own E4).
        var originalNote = afterCorrection.Original.Notes.Single(n => n.Id == notes[2].Id);
        Assert.Equal(64, originalNote.MidiNote);
        Assert.False(originalNote.UserCorrected);

        // A fresh load from disk independently confirms the persisted state (not merely an in-memory
        // result) -- compared field-by-field for the same reason as above.
        var reloaded = await editor.GetStateAsync(begun.TrackId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(afterCorrection.TrackId, reloaded!.TrackId);
        Assert.Equal(afterCorrection.Revision, reloaded.Revision);
        Assert.Equal(afterCorrection.LockedNoteIds, reloaded.LockedNoteIds);
        Assert.Equal(afterCorrection.Current.Notes, reloaded.Current.Notes);
        Assert.Equal(afterCorrection.Current.Gestures, reloaded.Current.Gestures);
        Assert.Equal(afterCorrection.Original.Notes, reloaded.Original.Notes);

        // "regenerate only dependent phrases": every gesture for a note strictly BEFORE the corrected
        // note (index 0, 1) is byte-identical to its pre-correction value -- correcting note 2 never
        // regenerates gestures for notes that come before it in the forward-only classifier.
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(gesturesBeforeCorrection[i], afterCorrection.Current.Gestures[i]);
        }

        // Everything from the corrected note onward is freshly regenerated against the new pitch:
        // the gesture immediately after the correction (G4, interval now measured from F4=65, not
        // E4=64) is different from its pre-correction value.
        Assert.NotEqual(gesturesBeforeCorrection[3], afterCorrection.Current.Gestures[3]);
    }

    [Fact]
    public async Task CorrectAsync_StaleRevision_RejectsWithoutPartialMutation_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60), Note(1_100_000L, 2_000_000L, 62) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        var staleRevision = new DocumentRevision(begun.Revision.Value + 1);
        var command = new CorrectNoteCommand(staleRevision, FullTrackScope(analyzed), notes[0] with { MidiNote = 61 });

        var path = new JsonMelodyTrackRevisionStore(_rootDirectory).GetPath(begun.TrackId);
        var bytesBefore = await File.ReadAllBytesAsync(path);

        var ex = await Assert.ThrowsAsync<NoteCorrectionRejectedException>(
            () => editor.CorrectAsync(begun.TrackId, command, CancellationToken.None));
        Assert.Equal(NoteEditRejectionReason.StaleRevision, ex.Reason);

        var bytesAfter = await File.ReadAllBytesAsync(path);
        Assert.Equal(bytesBefore, bytesAfter);
    }

    [Fact]
    public async Task CorrectAsync_OutOfScope_RejectsWithoutPartialMutation_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60), Note(1_100_000L, 2_000_000L, 62) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        // A scope that covers only the second note's own time window -- correcting the first note
        // must be rejected as out-of-scope.
        var narrowScope = new NoteEditScope(new TimeWindow(1_100_000L, 2_000_000L));
        var command = new CorrectNoteCommand(begun.Revision, narrowScope, notes[0] with { MidiNote = 61 });

        var path = new JsonMelodyTrackRevisionStore(_rootDirectory).GetPath(begun.TrackId);
        var bytesBefore = await File.ReadAllBytesAsync(path);

        var ex = await Assert.ThrowsAsync<NoteCorrectionRejectedException>(
            () => editor.CorrectAsync(begun.TrackId, command, CancellationToken.None));
        Assert.Equal(NoteEditRejectionReason.OutOfScope, ex.Reason);

        var bytesAfter = await File.ReadAllBytesAsync(path);
        Assert.Equal(bytesBefore, bytesAfter);
    }

    [Fact]
    public async Task CorrectAsync_CorrectionMovesOutsideScopeWindow_RejectsWithoutPartialMutation_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60), Note(1_100_000L, 2_000_000L, 62) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        var scope = FullTrackScope(analyzed); // [0, 2,000,001)
        var movedFarOutside = notes[0] with { StartTicks = 5_000_000L, EndTicks = 6_000_000L };
        var command = new CorrectNoteCommand(begun.Revision, scope, movedFarOutside);

        var ex = await Assert.ThrowsAsync<NoteCorrectionRejectedException>(
            () => editor.CorrectAsync(begun.TrackId, command, CancellationToken.None));
        Assert.Equal(NoteEditRejectionReason.OutOfScope, ex.Reason);

        var reloaded = await editor.GetStateAsync(begun.TrackId, CancellationToken.None);
        Assert.Equal(begun.Revision, reloaded!.Revision);
        Assert.Equal(begun.Current.Notes, reloaded.Current.Notes);
        Assert.Equal(begun.LockedNoteIds, reloaded.LockedNoteIds);
    }

    [Fact]
    public async Task CorrectAsync_LockedNote_RejectsWithoutPartialMutation_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60), Note(1_100_000L, 2_000_000L, 62) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        var lockCommand = new SetNoteLockedCommand(begun.Revision, notes[0].Id, Locked: true);
        var locked = await editor.SetNoteLockedAsync(begun.TrackId, lockCommand, CancellationToken.None);
        Assert.Contains(notes[0].Id, locked.LockedNoteIds);

        var path = new JsonMelodyTrackRevisionStore(_rootDirectory).GetPath(begun.TrackId);
        var bytesBefore = await File.ReadAllBytesAsync(path);

        var command = new CorrectNoteCommand(locked.Revision, FullTrackScope(analyzed), notes[0] with { MidiNote = 61 });
        var ex = await Assert.ThrowsAsync<NoteCorrectionRejectedException>(
            () => editor.CorrectAsync(begun.TrackId, command, CancellationToken.None));
        Assert.Equal(NoteEditRejectionReason.Locked, ex.Reason);

        var bytesAfter = await File.ReadAllBytesAsync(path);
        Assert.Equal(bytesBefore, bytesAfter);
    }

    [Fact]
    public async Task CorrectAsync_LockedNote_WithRespectLocksFalse_IsPermitted()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60), Note(1_100_000L, 2_000_000L, 62) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        var lockCommand = new SetNoteLockedCommand(begun.Revision, notes[0].Id, Locked: true);
        var locked = await editor.SetNoteLockedAsync(begun.TrackId, lockCommand, CancellationToken.None);

        var bypassScope = FullTrackScope(analyzed, respectLocks: false);
        var command = new CorrectNoteCommand(locked.Revision, bypassScope, notes[0] with { MidiNote = 61 });
        var result = await editor.CorrectAsync(begun.TrackId, command, CancellationToken.None);

        Assert.Equal(61, result.Current.Notes.Single(n => n.Id == notes[0].Id).MidiNote);
    }

    [Fact]
    public async Task CorrectAsync_UnknownNoteId_ThrowsNoteNotFound_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        var unknown = Note(0L, 1_000_000L, 61);
        var command = new CorrectNoteCommand(begun.Revision, FullTrackScope(analyzed), unknown);

        var ex = await Assert.ThrowsAsync<NoteCorrectionRejectedException>(
            () => editor.CorrectAsync(begun.TrackId, command, CancellationToken.None));
        Assert.Equal(NoteEditRejectionReason.NoteNotFound, ex.Reason);
    }

    [Fact]
    public async Task CorrectAsync_InvalidCorrectedNote_ThrowsMelodyTrackInvalidException_WithoutPartialMutation_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60), Note(1_100_000L, 2_000_000L, 62) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        // Zero-length duration: EndTicks <= StartTicks is this phase's own named "invalid timing boundaries."
        var invalid = notes[0] with { EndTicks = notes[0].StartTicks };
        var command = new CorrectNoteCommand(begun.Revision, FullTrackScope(analyzed), invalid);

        var path = new JsonMelodyTrackRevisionStore(_rootDirectory).GetPath(begun.TrackId);
        var bytesBefore = await File.ReadAllBytesAsync(path);

        await Assert.ThrowsAsync<MelodyTrackInvalidException>(
            () => editor.CorrectAsync(begun.TrackId, command, CancellationToken.None));

        var bytesAfter = await File.ReadAllBytesAsync(path);
        Assert.Equal(bytesBefore, bytesAfter);
    }

    [Fact]
    public async Task SetNoteLockedAsync_StaleRevision_RejectsWithoutPartialMutation_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        var stale = new DocumentRevision(begun.Revision.Value + 5);
        var command = new SetNoteLockedCommand(stale, notes[0].Id, Locked: true);

        var ex = await Assert.ThrowsAsync<NoteCorrectionRejectedException>(
            () => editor.SetNoteLockedAsync(begun.TrackId, command, CancellationToken.None));
        Assert.Equal(NoteEditRejectionReason.StaleRevision, ex.Reason);

        var reloaded = await editor.GetStateAsync(begun.TrackId, CancellationToken.None);
        Assert.Empty(reloaded!.LockedNoteIds);
    }

    [Fact]
    public async Task BeginEditingAsync_CalledTwiceForSameTrack_Throws_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None));
    }

    [Fact]
    public async Task CorrectAsync_CancelledBeforeApplying_ThrowsWithoutProducingAChange_CancellationNegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60) };
        var analyzed = ClassifiedTrack(notes);
        var editor = CreateEditor();
        var begun = await editor.BeginEditingAsync(analyzed, GestureClassificationSettings.Default, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var command = new CorrectNoteCommand(begun.Revision, FullTrackScope(analyzed), notes[0] with { MidiNote = 61 });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => editor.CorrectAsync(begun.TrackId, command, cts.Token));

        var reloaded = await editor.GetStateAsync(begun.TrackId, CancellationToken.None);
        Assert.Equal(60, reloaded!.Current.Notes.Single().MidiNote);
    }

    [Fact]
    public async Task CorrectAsync_NoRevisionStateExists_ThrowsNoteNotFound_NegativePath()
    {
        var editor = CreateEditor();
        var randomTrackId = Guid.NewGuid();
        var scope = new NoteEditScope(new TimeWindow(0, 1_000_000L));
        var command = new CorrectNoteCommand(new DocumentRevision(0), scope, new NoteEvent(Guid.NewGuid(), 0, 1_000_000L, 60, 0.8, 0.9, false));

        var ex = await Assert.ThrowsAsync<NoteCorrectionRejectedException>(
            () => editor.CorrectAsync(randomTrackId, command, CancellationToken.None));
        Assert.Equal(NoteEditRejectionReason.NoteNotFound, ex.Reason);
    }
}
