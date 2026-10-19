namespace XlightsAI.Studio.Shell;

/// <summary>
/// Read-only Settings screen (SCREEN 17 SETTINGS): displays the currently persisted
/// <see cref="AppSettings"/>. There is no settings-editing UI in this step — per the phase's
/// "do not present empty future functionality as implemented" guidance, this screen shows real
/// current state only. Always resolves to Loaded or Error (settings always have a value, even if
/// it's <see cref="AppSettings.Default"/>), so there is no meaningful Empty state here.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Loading;
    private string? _errorMessage;
    private AppSettings? _settings;

    public ScreenLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (SetField(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsLoading));
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

    public AppSettings? Settings
    {
        get => _settings;
        private set => SetField(ref _settings, value);
    }

    public bool IsLoading => LoadState == ScreenLoadState.Loading;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>
    /// Loads settings from <paramref name="settingsSource"/>. A thrown exception becomes an
    /// explicit Error state with a user-readable message rather than a blank or stale screen.
    /// </summary>
    public void Load(Func<AppSettings> settingsSource)
    {
        ArgumentNullException.ThrowIfNull(settingsSource);

        LoadState = ScreenLoadState.Loading;
        ErrorMessage = null;

        try
        {
            Settings = settingsSource();
            LoadState = ScreenLoadState.Loaded;
        }
        catch (Exception ex)
        {
            Settings = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
        }
    }
}
