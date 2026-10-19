using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-11 Step 072 ("Create persisted selection context: interval, props, instruments, phrases,
/// locks") -- SCREEN 14 AI EDIT's own bindable selection-capture view model. Owns the UI-facing
/// projection over <see cref="ISelectionContextCaptureService"/>/<see cref="ISelectionContextStore"/>.
/// Contains no Avalonia types, the same convention every other screen view model in this project already
/// follows (<see cref="RoleRoutingEditorViewModel"/>/<see cref="DisplayPathEditorViewModel"/>).
///
/// <para>
/// Mirrors <see cref="RoleRoutingEditorViewModel"/>'s own explicit loading/empty/error/loaded state
/// machine (no selection captured yet is <see cref="ScreenLoadState.Empty"/>, never a silently blank
/// panel) and its own "catch the domain's typed invalid-input exception, set
/// <see cref="ActionErrorMessage"/>, leave bindable state completely unchanged on failure" convention.
/// </para>
///
/// <para>
/// Required acceptance, verbatim: "Serialization test preserves exact selection and scope" --
/// <see cref="CaptureAsync"/> captures and immediately persists a <see cref="SelectionContext"/>;
/// <see cref="ReloadAsync"/> independently re-loads the persisted file (proving a real save/reopen round
/// trip, the "saved-state verification" this step's own required user-visible outcome names, verbatim).
/// </para>
/// </summary>
public sealed class SelectionContextEditorViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;
    private SelectionContext? _current;
    private string? _savedPath;

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

    /// <summary>Set whenever a capture action fails (out-of-scope, locked or stale revision); cleared on the next successful capture. <see cref="Current"/> is left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The most recently captured (or reloaded) selection, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public SelectionContext? Current => _current;

    /// <summary>The path <see cref="Current"/> was last persisted to, or reloaded from.</summary>
    public string? SavedPath => _savedPath;

    /// <summary>
    /// Validates <paramref name="scope"/>/selection identities against <paramref name="document"/> via
    /// <paramref name="captureService"/>, then immediately persists the result via
    /// <paramref name="store"/>. Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>)
    /// on any rejection (out-of-scope, locked or stale revision) -- never a partial mutation of
    /// <see cref="Current"/>.
    /// </summary>
    public async Task<bool> CaptureAsync(
        ISelectionContextCaptureService captureService,
        ISelectionContextStore store,
        SequenceDocument document,
        DocumentRevision expectedRevision,
        EditScope scope,
        IReadOnlyList<string> instrumentTrackIds,
        IReadOnlyList<Guid> phraseIds,
        IReadOnlyList<Guid> spatialPathIds,
        IReadOnlyList<Guid> noteIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(captureService);
        ArgumentNullException.ThrowIfNull(store);

        try
        {
            var captured = captureService.Capture(
                document, expectedRevision, scope, instrumentTrackIds, phraseIds, spatialPathIds, noteIds);
            var path = await store.SaveAsync(captured, ct).ConfigureAwait(true);

            _current = captured;
            _savedPath = path;
            ActionErrorMessage = null;
            ErrorMessage = null;
            LoadState = ScreenLoadState.Loaded;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (EditCommandRejectedException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Independently re-loads the <see cref="SelectionContext"/> last written to <paramref name="path"/>
    /// through a caller-supplied (potentially brand-new) <paramref name="store"/> instance -- a real
    /// "restart" proof, the same convention <see cref="RoleRoutingEditorViewModel.OpenSectionAsync"/>
    /// already establishes.
    /// </summary>
    public async Task<bool> ReloadAsync(ISelectionContextStore store, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var reloaded = await store.LoadAsync(path, ct).ConfigureAwait(true);
            _current = reloaded;
            _savedPath = path;
            LoadState = ScreenLoadState.Loaded;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (Exception ex)
        {
            _current = null;
            _savedPath = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    private void RaiseStateDerivedChanged()
    {
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(SavedPath));
    }
}
