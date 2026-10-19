using XlightsAI.Studio.Composition;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-10 Step 069 ("Build sectional energy and color/brightness story") -- SCREEN 12 CHOREOGRAPHY's own
/// bindable sectional energy/color story editor view model. Owns the UI-facing projection of one song's
/// <see cref="VisualStory"/> -- planned via <see cref="ISectionVisualStoryPlanner"/>, loaded/saved through
/// <see cref="IVisualStoryCatalog"/>. Contains no Avalonia types, the same convention every other screen view
/// model in this project already follows (<see cref="VisualBudgetEditorViewModel"/>/<see cref="RoleRoutingEditorViewModel"/>).
///
/// <para>
/// Mirrors <see cref="VisualBudgetEditorViewModel"/>'s own explicit loading/empty/error/loaded state machine
/// and its own "catch the domain's typed invalid-input exception, set <see cref="ActionErrorMessage"/>, leave
/// bindable state completely unchanged on failure" convention for every mutating action, and its own
/// plan-then-save split: <see cref="Plan"/> computes a new story in memory without persisting it (so a user
/// can inspect the intensity curve before committing), <see cref="SaveAsync"/> persists whatever <see cref="Plan"/>
/// most recently computed.
/// </para>
///
/// <para>
/// Required acceptance, verbatim: "Verse/chorus intensity curve follows plan" -- proven directly at the
/// domain layer by <see cref="SectionVisualStoryPlanner"/>'s own acceptance test; this view model's own job
/// is only to surface that same plan as a bindable table (<see cref="Rows"/>) plus a persisted,
/// saved-state-verified story a real SCREEN 12 view would bind to.
/// </para>
/// </summary>
public sealed class VisualStoryEditorViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;
    private Guid? _songId;
    private int? _version;
    private VisualStory? _savedStory;
    private VisualStory? _pendingStory;

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

    /// <summary>Set whenever a plan/save action fails (invalid input); cleared on the next successful action. Bindable state is left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The song id currently open, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public Guid? SongId => _songId;

    /// <summary>The currently persisted version for <see cref="SongId"/>, or <see langword="null"/> while <see cref="IsEmpty"/> or before the first save.</summary>
    public int? Version => _version;

    /// <summary>
    /// The real, currently-active rows for the open song -- the most recently <see cref="Plan"/>ned story if
    /// one has not yet been saved, else the most recently persisted story; empty exactly when
    /// <see cref="IsLoaded"/> is <see langword="false"/> or nothing has ever been planned/saved.
    /// </summary>
    public IReadOnlyList<SectionVisualDirectionRowView> Rows
    {
        get
        {
            var current = _pendingStory ?? _savedStory;
            if (current is null)
            {
                return [];
            }

            return current.Sections
                .Select(s => new SectionVisualDirectionRowView(s.SectionId, s.Kind, s.PaletteId, s.Intensity, (int)Math.Round(s.Intensity * 100.0)))
                .ToList();
        }
    }

    /// <summary><see langword="true"/> exactly when <see cref="Plan"/> has computed a story that has not yet been <see cref="SaveAsync"/>d.</summary>
    public bool HasUnsavedPlan => _pendingStory is not null;

    /// <summary>
    /// Opens (or switches to) <paramref name="songId"/>'s own independently persisted story, loaded through
    /// <paramref name="catalog"/>. Calling this again with a different <paramref name="songId"/> at any time
    /// loads that song's own independently saved story and discards any unsaved <see cref="Plan"/> for the
    /// previous song.
    /// </summary>
    public async Task<bool> OpenSongAsync(IVisualStoryCatalog catalog, Guid songId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var record = await catalog.GetAsync(songId, ct).ConfigureAwait(true);
            _songId = songId;
            _version = record?.Version;
            _savedStory = record?.Story;
            _pendingStory = null;
            LoadState = ScreenLoadState.Loaded;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (Exception ex)
        {
            _songId = null;
            _version = null;
            _savedStory = null;
            _pendingStory = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>
    /// Plans a new story from <paramref name="sections"/> via <paramref name="planner"/> for the currently
    /// open song, publishing it through <see cref="Rows"/> without persisting it. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when no song is open or
    /// <paramref name="planner"/> rejects the inputs -- never a partial plan.
    /// </summary>
    public bool Plan(ISectionVisualStoryPlanner planner, IReadOnlyList<SectionEnergyInput> sections)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(sections);

        if (_songId is null)
        {
            ActionErrorMessage = "Open a song before planning a visual story.";
            return false;
        }

        try
        {
            _pendingStory = planner.Plan(sections);
            ActionErrorMessage = null;
            RaisePendingChanged();
            return true;
        }
        catch (CompositionInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Saves the most recently <see cref="Plan"/>ned story under the currently open song via
    /// <paramref name="catalog"/>. Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>)
    /// when no song is open, nothing has been planned yet, or the catalog rejects the story -- never a
    /// partial mutation of the persisted <see cref="Rows"/>.
    /// </summary>
    public async Task<bool> SaveAsync(IVisualStoryCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_songId is not { } songId)
        {
            ActionErrorMessage = "Open a song before saving a visual story.";
            return false;
        }

        if (_pendingStory is not { } pending)
        {
            ActionErrorMessage = "Plan a visual story before saving it.";
            return false;
        }

        try
        {
            var saved = await catalog.SaveAsync(songId, pending, ct).ConfigureAwait(true);
            _savedStory = saved.Story;
            _version = saved.Version;
            _pendingStory = null;
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (CompositionInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    private void RaiseStateDerivedChanged()
    {
        OnPropertyChanged(nameof(SongId));
        OnPropertyChanged(nameof(Version));
        RaisePendingChanged();
    }

    private void RaisePendingChanged()
    {
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(HasUnsavedPlan));
    }
}
