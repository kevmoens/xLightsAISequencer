using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Build-plan Phase 17 §2e: browse every mined exemplar and enable/disable individual entries, or a
/// whole source song's worth of material in one action, persisted via <see cref="JsonExemplarOverrideStore"/>
/// (a file separate from <c>exemplar-index.json</c>, so re-running corpus mining never resets a user's
/// curation choices -- see that store's own doc comment). A first cut: one flat list per song group, no
/// filtering/search yet (see build-plan Phase 17 §2e's own note that this should be refined once a user
/// has tried it against the real ~600+-entry index).
/// </summary>
public sealed class ExemplarLibraryViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private IReadOnlyList<ExemplarLibrarySongGroupViewModel> _songGroups = [];
    private string? _knowledgeDirectory;
    private bool _isBulkUpdating;

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

    public IReadOnlyList<ExemplarLibrarySongGroupViewModel> SongGroups
    {
        get => _songGroups;
        private set => SetField(ref _songGroups, value);
    }

    public bool IsLoading => LoadState == ScreenLoadState.Loading;
    public bool IsEmpty => LoadState == ScreenLoadState.Empty;
    public bool IsError => LoadState == ScreenLoadState.Error;
    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    public int TotalEntryCount => SongGroups.Sum(g => g.Entries.Count);
    public int EnabledEntryCount => SongGroups.Sum(g => g.Entries.Count(e => e.IsEnabled));

    /// <summary>
    /// Loads every entry from <c>exemplar-index.json</c> under <paramref name="knowledgeDirectory"/>, and
    /// every curation flag from <c>exemplar-overrides.json</c>, grouped by source song. An exception
    /// (including the real <see cref="FileNotFoundException"/> when corpus mining has never run) becomes
    /// an explicit Error/Empty state, never a silently blank screen.
    /// </summary>
    public async Task LoadAsync(string knowledgeDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeDirectory);
        _knowledgeDirectory = knowledgeDirectory;

        LoadState = ScreenLoadState.Loading;
        ErrorMessage = null;

        try
        {
            var index = await new JsonExemplarIndexStore(knowledgeDirectory).LoadAsync(ct).ConfigureAwait(true);
            var disabledByKey = await new JsonExemplarOverrideStore(knowledgeDirectory).LoadAsync(ct).ConfigureAwait(true);

            var groups = index.Entries
                .GroupBy(e => e.Fingerprint.SongTitle, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var rows = g
                        .OrderBy(e => e.Fingerprint.StartBarIndex)
                        .Select(e =>
                        {
                            var key = ExemplarOverrides.KeyFor(e.Fingerprint);
                            var isEnabled = !disabledByKey.TryGetValue(key, out var disabled) || !disabled;
                            var row = new ExemplarLibraryEntryViewModel(e, isEnabled);
                            row.PropertyChanged += (_, args) =>
                            {
                                if (args.PropertyName == nameof(ExemplarLibraryEntryViewModel.IsEnabled))
                                {
                                    OnPropertyChanged(nameof(EnabledEntryCount));
                                    if (!_isBulkUpdating)
                                    {
                                        _ = PersistAsync();
                                    }
                                }
                            };
                            return row;
                        })
                        .ToList();
                    return new ExemplarLibrarySongGroupViewModel(g.Key, rows, ToggleSongAsync);
                })
                .ToList();

            SongGroups = groups;
            LoadState = groups.Count == 0 ? ScreenLoadState.Empty : ScreenLoadState.Loaded;
        }
        catch (FileNotFoundException)
        {
            SongGroups = [];
            LoadState = ScreenLoadState.Empty;
        }
        catch (Exception ex)
        {
            SongGroups = [];
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
        }
    }

    private Task ToggleSongAsync(ExemplarLibrarySongGroupViewModel group, bool enabled)
    {
        // Suppress each row's own per-toggle persist while applying the whole group at once -- a single
        // "enable/disable all" click should write exemplar-overrides.json once, not once per row.
        _isBulkUpdating = true;
        try
        {
            foreach (var row in group.Entries)
            {
                row.IsEnabled = enabled;
            }
        }
        finally
        {
            _isBulkUpdating = false;
        }

        OnPropertyChanged(nameof(EnabledEntryCount));
        return PersistAsync();
    }

    private async Task PersistAsync()
    {
        if (_knowledgeDirectory is null)
        {
            return;
        }

        var overrides = SongGroups
            .SelectMany(g => g.Entries)
            .Where(e => !e.IsEnabled)
            .ToDictionary(e => e.Key, _ => true, StringComparer.Ordinal);

        try
        {
            await new JsonExemplarOverrideStore(_knowledgeDirectory).SaveAsync(overrides, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save curation choice: {ex.Message}";
        }
    }
}

/// <summary>One source song's exemplars, with an "enable/disable all" action for this screen.</summary>
public sealed class ExemplarLibrarySongGroupViewModel
{
    private readonly Func<ExemplarLibrarySongGroupViewModel, bool, Task> _onSetAll;

    public ExemplarLibrarySongGroupViewModel(
        string songTitle, IReadOnlyList<ExemplarLibraryEntryViewModel> entries, Func<ExemplarLibrarySongGroupViewModel, bool, Task> onSetAll)
    {
        SongTitle = songTitle;
        Entries = entries;
        _onSetAll = onSetAll ?? throw new ArgumentNullException(nameof(onSetAll));
        EnableAllCommand = new DelegateCommand(() => _ = _onSetAll(this, true));
        DisableAllCommand = new DelegateCommand(() => _ = _onSetAll(this, false));
    }

    public string SongTitle { get; }

    public IReadOnlyList<ExemplarLibraryEntryViewModel> Entries { get; }

    public int EnabledCount => Entries.Count(e => e.IsEnabled);

    public DelegateCommand EnableAllCommand { get; }

    public DelegateCommand DisableAllCommand { get; }
}

/// <summary>
/// One mined exemplar's curation row. <see cref="IsEnabled"/> is meant to be bound two-way directly to a
/// checkbox (e.g. <c>IsChecked="{Binding IsEnabled}"</c>) -- the owning <see cref="ExemplarLibraryViewModel"/>
/// listens to this row's own <see cref="ObservableObject.PropertyChanged"/> to persist a single-row
/// toggle, rather than this type knowing anything about persistence itself.
/// </summary>
public sealed class ExemplarLibraryEntryViewModel : ObservableObject
{
    private bool _isEnabled;

    public ExemplarLibraryEntryViewModel(ExemplarEntry entry, bool isEnabled)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _isEnabled = isEnabled;
    }

    public ExemplarEntry Entry { get; }

    public string Key => ExemplarOverrides.KeyFor(Entry.Fingerprint);

    public string SongTitle => Entry.Fingerprint.SongTitle;

    public int StartBarIndex => Entry.Fingerprint.StartBarIndex;

    public int EndBarIndexExclusive => Entry.Fingerprint.EndBarIndexExclusive;

    public string BarRangeDisplay => $"bars {StartBarIndex}-{EndBarIndexExclusive}";

    public SectionKind SectionKind => Entry.Fingerprint.SectionKind;

    public double TempoBpm => Entry.Fingerprint.TempoBpm;

    public double EnergyPercentile => Entry.Fingerprint.EnergyPercentile;

    public string? Description => Entry.Description;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }
}
