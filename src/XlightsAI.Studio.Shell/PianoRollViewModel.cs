using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-07 Step 049 ("Implement editable piano roll and low-confidence markers") — SCREEN 10 PIANO
/// ROLL. Owns the bindable rendering of a <see cref="MelodyTrackRevisionState"/>'s own
/// <see cref="MelodyTrack.Notes"/> and routes user corrections through <see cref="INoteTrackEditor"/>.
/// Contains no Avalonia types, same convention as every other screen view model in this project
/// (<see cref="TimelineViewModel"/>/<see cref="AudioImportViewModel"/>). Mirrors
/// <see cref="TimelineViewModel"/>'s explicit loading/empty/error/loaded state machine: no track
/// loaded yet is <see cref="ScreenLoadState.Empty"/>, never a silently blank roll.
///
/// Every mutating action here (<see cref="BeginEditingAsync"/>/<see cref="CorrectSelectedNoteAsync"/>/
/// <see cref="ToggleLockSelectedNoteAsync"/>) catches <see cref="NoteCorrectionRejectedException"/> and
/// <see cref="MelodyTrackInvalidException"/>, sets <see cref="ActionErrorMessage"/>, and leaves
/// <see cref="Rows"/> completely unchanged on failure — the same "catch, set an error property, never
/// let an exception reach the UI, never a partial mutation" convention every other screen action in
/// this project already follows (e.g. <see cref="ShellViewModel"/>'s own scrub/loop actions).
/// </summary>
public sealed class PianoRollViewModel : ObservableObject
{
    /// <summary>A readable default zoom level for the time axis: 1 pixel == 500,000 ticks == 50 ms — finer than <see cref="TimelineViewModel.DefaultTicksPerPixel"/>, appropriate for individual note lengths rather than a whole-song ruler.</summary>
    public const double DefaultTicksPerPixel = 500_000d;

    /// <summary>Vertical pixel height of one MIDI pitch row.</summary>
    public const double RowHeightPixels = 14d;

    private const int MaxMidiNote = 127;

    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;
    private MelodyTrackRevisionState? _state;
    private Guid? _selectedNoteId;
    private string _pendingMidiNoteText = string.Empty;
    private string _pendingStartTicksText = string.Empty;
    private string _pendingEndTicksText = string.Empty;
    private double _ticksPerPixel = DefaultTicksPerPixel;
    private double _lowConfidenceThreshold = GestureClassificationSettings.Default.MinNoteConfidence;

    public ScreenLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (SetField(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(IsLoaded));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>Set whenever a correction/lock-toggle action fails (out-of-scope, locked, stale revision, or an invalid corrected note); cleared on the next successful action. <see cref="Rows"/> is left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>Bindable time-axis zoom; a presentation concern only, never persisted.</summary>
    public double TicksPerPixel
    {
        get => _ticksPerPixel;
        set
        {
            if (SetField(ref _ticksPerPixel, value))
            {
                OnPropertyChanged(nameof(Rows));
            }
        }
    }

    /// <summary>A note whose <see cref="NoteEvent.Confidence"/> falls below this value renders with <see cref="PianoRollNoteRowView.IsLowConfidence"/> set — this step's own named "low-confidence markers." Defaults to <see cref="GestureClassificationSettings.Default"/>'s own <c>MinNoteConfidence</c>, the same threshold Step 047's own noise filtering already uses, so "low confidence" means the same thing on this screen as it does during analysis.</summary>
    public double LowConfidenceThreshold
    {
        get => _lowConfidenceThreshold;
        set
        {
            if (SetField(ref _lowConfidenceThreshold, value))
            {
                OnPropertyChanged(nameof(Rows));
            }
        }
    }

    /// <summary>The track identity currently loaded, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public Guid? TrackId => _state?.TrackId;

    /// <summary>The current optimistic-concurrency revision of the loaded track, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public long? Revision => _state?.Revision.Value;

    /// <summary>The original, never-mutated transcription's own note count (Section 4: "never lose original transcription") — compared against <see cref="CurrentNoteCount"/> to show a correction never dropped/added a note.</summary>
    public int? OriginalNoteCount => _state?.Original.Notes.Count;

    public int? CurrentNoteCount => _state?.Current.Notes.Count;

    public Guid? SelectedNoteId
    {
        get => _selectedNoteId;
        private set
        {
            if (SetField(ref _selectedNoteId, value))
            {
                OnPropertyChanged(nameof(Rows));
            }
        }
    }

    /// <summary>
    /// The real, currently-rendered note rows — empty exactly when <see cref="IsLoaded"/> is
    /// <see langword="false"/>, never a stale leftover from a previously loaded track.
    /// </summary>
    public IReadOnlyList<PianoRollNoteRowView> Rows
    {
        get
        {
            if (_state is not { } state)
            {
                return [];
            }

            var views = new List<PianoRollNoteRowView>(state.Current.Notes.Count);
            foreach (var note in state.Current.Notes)
            {
                var noteId = note.Id;
                views.Add(new PianoRollNoteRowView(
                    noteId,
                    note.MidiNote,
                    X: note.StartTicks / TicksPerPixel,
                    Width: Math.Max(1d, (note.EndTicks - note.StartTicks) / TicksPerPixel),
                    Y: (MaxMidiNote - note.MidiNote) * RowHeightPixels,
                    IsLowConfidence: note.Confidence < LowConfidenceThreshold,
                    IsUserCorrected: note.UserCorrected,
                    IsLocked: state.LockedNoteIds.Contains(noteId),
                    IsSelected: noteId == SelectedNoteId,
                    SelectCommand: new DelegateCommand(() => SelectNote(noteId))));
            }

            return views;
        }
    }

    /// <summary>
    /// Bindable pending-correction text for <see cref="CorrectSelectedNoteAsync"/> — same "text field
    /// + button, parsed on explicit action" convention as
    /// <see cref="DisplayPathEditorViewModel.PendingPositionXText"/>. Pre-filled with the selected
    /// note's current pitch/timing by <see cref="SelectNote"/>.
    /// </summary>
    public string PendingMidiNoteText
    {
        get => _pendingMidiNoteText;
        set => SetField(ref _pendingMidiNoteText, value);
    }

    public string PendingStartTicksText
    {
        get => _pendingStartTicksText;
        set => SetField(ref _pendingStartTicksText, value);
    }

    public string PendingEndTicksText
    {
        get => _pendingEndTicksText;
        set => SetField(ref _pendingEndTicksText, value);
    }

    /// <summary>Selects <paramref name="noteId"/> — a no-op (not a crash, not a dangling selection) when no track is loaded or <paramref name="noteId"/> does not exist in it.</summary>
    public void SelectNote(Guid noteId)
    {
        if (_state?.Current.Notes.FirstOrDefault(n => n.Id == noteId) is not { } note)
        {
            return;
        }

        SelectedNoteId = noteId;
        PendingMidiNoteText = note.MidiNote.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PendingStartTicksText = note.StartTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PendingEndTicksText = note.EndTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Clears the current selection; a no-op when nothing is selected.</summary>
    public void ClearSelection()
    {
        SelectedNoteId = null;
        PendingMidiNoteText = string.Empty;
        PendingStartTicksText = string.Empty;
        PendingEndTicksText = string.Empty;
    }

    /// <summary>
    /// Begins a new correction session for <paramref name="analyzedTrack"/> (Step 045/046/047/048's
    /// own already-produced, gesture-classified analysis output). Fails explicitly (sets
    /// <see cref="ErrorMessage"/>, stays/returns to <see cref="ScreenLoadState.Error"/>) rather than
    /// throwing when a revision state already exists for this track id, or the track itself is
    /// invalid ("missing media must be handled explicitly").
    /// </summary>
    public async Task<bool> BeginEditingAsync(
        INoteTrackEditor editor, MelodyTrack analyzedTrack, GestureClassificationSettings? classificationSettings = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(analyzedTrack);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var state = await editor.BeginEditingAsync(
                analyzedTrack, classificationSettings ?? GestureClassificationSettings.Default, ct).ConfigureAwait(true);
            ApplyLoadedState(state);
            return true;
        }
        catch (Exception ex)
        {
            _state = null;
            SelectedNoteId = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>Opens an already-begun correction session for <paramref name="trackId"/>. Sets <see cref="ScreenLoadState.Empty"/> (not <see cref="ScreenLoadState.Error"/>) when no session has ever been started for it — "missing media" is an expected empty state here, not a failure.</summary>
    public async Task<bool> OpenTrackAsync(INoteTrackEditor editor, Guid trackId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(editor);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var state = await editor.GetStateAsync(trackId, ct).ConfigureAwait(true);
            if (state is null)
            {
                _state = null;
                SelectedNoteId = null;
                LoadState = ScreenLoadState.Empty;
                RaiseStateDerivedChanged();
                return false;
            }

            ApplyLoadedState(state);
            return true;
        }
        catch (Exception ex)
        {
            _state = null;
            SelectedNoteId = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>
    /// Corrects <see cref="SelectedNoteId"/>'s own pitch/timing ("drag/resize," implementation
    /// guidance) against the entire loaded track's own content bounds as the permitted edit scope — a
    /// caller that needs a narrower scope (e.g. "only this phrase") should build
    /// <see cref="CorrectNoteCommand"/> directly against <paramref name="editor"/> instead of using
    /// this convenience overload. Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>)
    /// when nothing is selected, no track is loaded, or <paramref name="editor"/> rejects the
    /// correction (out-of-scope, locked, stale revision, or an invalid corrected note) — never a
    /// partial mutation of <see cref="Rows"/>.
    /// </summary>
    public async Task<bool> CorrectSelectedNoteAsync(
        INoteTrackEditor editor, int newMidiNote, long newStartTicks, long newEndTicks, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (_state is not { } state || SelectedNoteId is not { } selectedId)
        {
            ActionErrorMessage = "Select a note before correcting it.";
            return false;
        }

        var existing = state.Current.Notes.FirstOrDefault(n => n.Id == selectedId);
        if (existing is null)
        {
            ActionErrorMessage = "The selected note no longer exists in the current revision.";
            return false;
        }

        var corrected = existing with { MidiNote = newMidiNote, StartTicks = newStartTicks, EndTicks = newEndTicks };

        // A caller-agnostic default scope: the whole loaded track's own content bounds, with locks
        // respected (the default every real call site should want; a caller needing to bypass a
        // specific lock calls editor.CorrectAsync directly with its own NoteEditScope).
        var minTicks = state.Current.Notes.Count == 0 ? 0L : state.Current.Notes.Min(n => n.StartTicks);
        var maxTicks = state.Current.Notes.Count == 0 ? 0L : state.Current.Notes.Max(n => n.EndTicks);
        var scope = new NoteEditScope(new TimeWindow(
            Math.Min(minTicks, newStartTicks),
            Math.Max(maxTicks, newEndTicks)));

        var command = new CorrectNoteCommand(state.Revision, scope, corrected);

        try
        {
            var newState = await editor.CorrectAsync(state.TrackId, command, ct).ConfigureAwait(true);
            ApplyLoadedState(newState);
            ActionErrorMessage = null;
            return true;
        }
        catch (Exception ex) when (ex is NoteCorrectionRejectedException or MelodyTrackInvalidException)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Toggles <see cref="SelectedNoteId"/>'s own locked bit. Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when nothing is selected, no track is loaded, or the revision is stale.</summary>
    public async Task<bool> ToggleLockSelectedNoteAsync(INoteTrackEditor editor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (_state is not { } state || SelectedNoteId is not { } selectedId)
        {
            ActionErrorMessage = "Select a note before locking/unlocking it.";
            return false;
        }

        var currentlyLocked = state.LockedNoteIds.Contains(selectedId);
        var command = new SetNoteLockedCommand(state.Revision, selectedId, !currentlyLocked);

        try
        {
            var newState = await editor.SetNoteLockedAsync(state.TrackId, command, ct).ConfigureAwait(true);
            ApplyLoadedState(newState);
            ActionErrorMessage = null;
            return true;
        }
        catch (NoteCorrectionRejectedException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    private void ApplyLoadedState(MelodyTrackRevisionState state)
    {
        _state = state;
        if (SelectedNoteId is { } selected && !state.Current.Notes.Any(n => n.Id == selected))
        {
            // A stale selection from before this reload (e.g. the correction renumbered/replaced the
            // underlying list) never dangles.
            _selectedNoteId = null;
        }

        LoadState = ScreenLoadState.Loaded;
        RaiseStateDerivedChanged();
    }

    private void RaiseStateDerivedChanged()
    {
        OnPropertyChanged(nameof(TrackId));
        OnPropertyChanged(nameof(Revision));
        OnPropertyChanged(nameof(OriginalNoteCount));
        OnPropertyChanged(nameof(CurrentNoteCount));
        OnPropertyChanged(nameof(SelectedNoteId));
        OnPropertyChanged(nameof(Rows));
    }
}
