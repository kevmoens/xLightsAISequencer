namespace XlightsAI.Studio.Shell;

/// <summary>
/// Diagnostics screen: shows recent structured diagnostic events (including crash/startup
/// failures and injected errors), with explicit Loading/Empty/Error/Loaded states so a healthy
/// "no problems recorded" app never looks the same as a broken "couldn't read the log" app.
/// </summary>
public sealed class DiagnosticsViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Loading;
    private string? _errorMessage;
    private IReadOnlyList<DiagnosticEvent> _events = Array.Empty<DiagnosticEvent>();

    public ScreenLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (SetField(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsLoading));
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

    public IReadOnlyList<DiagnosticEvent> Events
    {
        get => _events;
        private set => SetField(ref _events, value);
    }

    public bool IsLoading => LoadState == ScreenLoadState.Loading;

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>
    /// Loads events from <paramref name="eventsSource"/>. A thrown exception becomes an explicit
    /// Error state with a user-readable message rather than a blank or stale list — diagnostics
    /// reporting on itself must not silently fail.
    /// </summary>
    public void Load(Func<IReadOnlyList<DiagnosticEvent>> eventsSource)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);

        LoadState = ScreenLoadState.Loading;
        ErrorMessage = null;

        try
        {
            var events = eventsSource();
            Events = events;
            LoadState = events.Count == 0 ? ScreenLoadState.Empty : ScreenLoadState.Loaded;
        }
        catch (Exception ex)
        {
            Events = Array.Empty<DiagnosticEvent>();
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
        }
    }
}
