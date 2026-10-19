using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-07 Step 049 — SCREEN 10 PIANO ROLL. Fast, deterministic tests for
/// <see cref="PianoRollViewModel"/>'s own bindable rendering/correction contract against a real
/// <see cref="NoteTrackEditor"/>/<see cref="JsonMelodyTrackRevisionStore"/> (not a mock) under a
/// temporary directory.
/// </summary>
public sealed class Step049PianoRollViewModelTests : IDisposable
{
    private const string Hash = "deadbeef";
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step049-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static NoteEvent Note(long startTicks, long endTicks, int midiNote, double confidence = 0.9) =>
        new(Guid.NewGuid(), startTicks, endTicks, midiNote, Velocity: 0.8, confidence, UserCorrected: false);

    private NoteTrackEditor CreateEditor() => new(new JsonMelodyTrackRevisionStore(_rootDirectory));

    [Fact]
    public async Task BeginEditingAsync_RendersRowsWithLowConfidenceMarkers()
    {
        var notes = new[]
        {
            Note(0L, 1_000_000L, 60, confidence: 0.9),
            Note(1_100_000L, 2_000_000L, 62, confidence: 0.2), // below default threshold (0.5)
        };
        var track = new MelodyTrack(Guid.NewGuid(), Hash, notes, []);
        var editor = CreateEditor();

        var vm = new PianoRollViewModel();
        Assert.True(vm.IsEmpty);

        var succeeded = await vm.BeginEditingAsync(editor, track);

        Assert.True(succeeded);
        Assert.True(vm.IsLoaded);
        Assert.Equal(2, vm.Rows.Count);
        Assert.False(vm.Rows[0].IsLowConfidence);
        Assert.True(vm.Rows[1].IsLowConfidence);
        Assert.Equal(0, vm.Revision);
        Assert.Equal(2, vm.OriginalNoteCount);
        Assert.Equal(2, vm.CurrentNoteCount);
    }

    [Fact]
    public async Task CorrectSelectedNoteAsync_PersistsCorrectionAndNeverLosesOriginalTranscription()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60), Note(1_100_000L, 2_000_000L, 62) };
        var track = new MelodyTrack(Guid.NewGuid(), Hash, notes, []);
        var editor = CreateEditor();

        var vm = new PianoRollViewModel();
        await vm.BeginEditingAsync(editor, track);

        vm.SelectNote(notes[0].Id);
        Assert.Equal(notes[0].Id, vm.SelectedNoteId);

        var succeeded = await vm.CorrectSelectedNoteAsync(editor, newMidiNote: 61, newStartTicks: 0L, newEndTicks: 1_000_000L);

        Assert.True(succeeded);
        Assert.Null(vm.ActionErrorMessage);
        Assert.Equal(1, vm.Revision);
        var row = vm.Rows.Single(r => r.NoteId == notes[0].Id);
        Assert.Equal(61, row.MidiNote);
        Assert.True(row.IsUserCorrected);

        // "never lose original transcription": re-opening the session from disk still has the
        // analyzer's own original note untouched, independent of PianoRollViewModel's in-memory state.
        var reopened = new PianoRollViewModel();
        await reopened.OpenTrackAsync(editor, track.Id);
        Assert.Equal(60, (await editor.GetStateAsync(track.Id, CancellationToken.None))!.Original.Notes.Single(n => n.Id == notes[0].Id).MidiNote);
    }

    [Fact]
    public async Task CorrectSelectedNoteAsync_NothingSelected_SetsActionErrorAndLeavesRowsUnchanged_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60) };
        var track = new MelodyTrack(Guid.NewGuid(), Hash, notes, []);
        var editor = CreateEditor();

        var vm = new PianoRollViewModel();
        await vm.BeginEditingAsync(editor, track);
        var rowsBefore = vm.Rows;

        var succeeded = await vm.CorrectSelectedNoteAsync(editor, newMidiNote: 61, newStartTicks: 0L, newEndTicks: 1_000_000L);

        Assert.False(succeeded);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(rowsBefore[0].MidiNote, vm.Rows[0].MidiNote);
        Assert.Equal(0, vm.Revision);
    }

    [Fact]
    public async Task ToggleLockSelectedNoteAsync_ThenCorrect_RejectsWithoutPartialMutation_NegativePath()
    {
        var notes = new[] { Note(0L, 1_000_000L, 60) };
        var track = new MelodyTrack(Guid.NewGuid(), Hash, notes, []);
        var editor = CreateEditor();

        var vm = new PianoRollViewModel();
        await vm.BeginEditingAsync(editor, track);
        vm.SelectNote(notes[0].Id);

        var lockSucceeded = await vm.ToggleLockSelectedNoteAsync(editor);
        Assert.True(lockSucceeded);
        Assert.True(vm.Rows.Single().IsLocked);

        var correctSucceeded = await vm.CorrectSelectedNoteAsync(editor, newMidiNote: 61, newStartTicks: 0L, newEndTicks: 1_000_000L);

        Assert.False(correctSucceeded);
        Assert.False(string.IsNullOrEmpty(vm.ActionErrorMessage));
        Assert.Equal(60, vm.Rows.Single().MidiNote); // untouched
    }

    [Fact]
    public async Task OpenTrackAsync_NoSessionEverStarted_IsEmptyNotError()
    {
        var editor = CreateEditor();
        var vm = new PianoRollViewModel();

        var succeeded = await vm.OpenTrackAsync(editor, Guid.NewGuid());

        Assert.False(succeeded);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void ShellViewModel_PianoRollScreen_NavigatesAndStartsEmpty()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();

        shell.NavigateToPianoRollCommand.Execute(null);

        Assert.True(shell.IsShowingPianoRoll);
        Assert.True(shell.PianoRoll.IsEmpty);
    }
}
