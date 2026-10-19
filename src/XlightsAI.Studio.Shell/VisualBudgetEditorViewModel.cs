using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-10 Step 068 ("Create configurable visual density and focus budget") -- SCREEN 12 CHOREOGRAPHY's own
/// bindable visual budget editor/preview view model. Owns the UI-facing projection of one named
/// <see cref="VisualBudgetPreset"/> -- loaded/saved through <see cref="IVisualBudgetPresetCatalog"/> -- and a
/// live, re-computable <see cref="VisualBudgetResolution"/> preview over a caller-supplied candidate gesture
/// set, via <see cref="IVisualBudgetResolver"/>. Contains no Avalonia types, the same convention every other
/// screen view model in this project already follows (<see cref="RoleRoutingEditorViewModel"/>/<see cref="DisplayPathEditorViewModel"/>).
///
/// <para>
/// Mirrors <see cref="RoleRoutingEditorViewModel"/>'s own explicit loading/empty/error/loaded state machine
/// (no preset opened yet is <see cref="ScreenLoadState.Empty"/>, never a silently blank budget) and its own
/// "catch the domain's typed invalid-input exception, set <see cref="ActionErrorMessage"/>, leave bindable
/// state completely unchanged on failure" convention for every mutating action.
/// </para>
///
/// <para>
/// Required acceptance, verbatim: "Clutter fixture is reduced while preserving focal musical event" -- proven
/// directly at the domain layer by <see cref="VisualBudgetResolver"/>'s own acceptance test; this view model's
/// own job is only to surface that same resolution as a bindable table (<see cref="Rows"/>) plus persisted,
/// saved-state-verified budget presets a real SCREEN 12 view would bind to.
/// </para>
/// </summary>
public sealed class VisualBudgetEditorViewModel : ObservableObject
{
    /// <summary>A permissive starting budget shown before any preset has ever been saved under the opened name.</summary>
    public static readonly VisualBudget DefaultBudget = new(
        MaxConcurrentFocalProps: 2, MaxMeanBrightness: 0.7, MaxEventDensityPerSecond: 2.0, DarknessFractionTarget: 0.2);

    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;
    private string? _presetName;
    private int? _version;
    private VisualBudget _currentBudget = DefaultBudget;
    private VisualBudgetResolution? _preview;

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

    /// <summary>Set whenever a save/preview action fails (invalid input); cleared on the next successful action. Bindable state is left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The preset name currently open, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public string? PresetName => _presetName;

    /// <summary>The currently persisted version for <see cref="PresetName"/>, or <see langword="null"/> while <see cref="IsEmpty"/> or before the first save.</summary>
    public int? Version => _version;

    /// <summary>The currently open budget -- either reloaded from a saved preset, or <see cref="DefaultBudget"/> if <see cref="PresetName"/> has never been saved.</summary>
    public VisualBudget CurrentBudget => _currentBudget;

    /// <summary>
    /// Every candidate's own resolved outcome from the most recent <see cref="Preview"/> call, empty until
    /// <see cref="Preview"/> has been called at least once for the currently open preset.
    /// </summary>
    public IReadOnlyList<VisualBudgetCandidateRowView> Rows
    {
        get
        {
            if (_preview is not { } preview)
            {
                return [];
            }

            return preview.Results
                .Select(r => new VisualBudgetCandidateRowView(
                    r.Candidate.Assignment.SourceTrackId,
                    r.Candidate.Assignment.Role,
                    r.Candidate.IsFocal,
                    r.Candidate.Window,
                    r.Decision,
                    r.AdmittedWindow,
                    r.Reason))
                .ToList();
        }
    }

    /// <summary>Non-fatal observations from the most recent <see cref="Preview"/> call (e.g. observed darkness fraction below target); empty until a preview has run.</summary>
    public IReadOnlyList<string> Warnings => _preview?.Warnings ?? [];

    public double? ObservedMeanBrightness => _preview?.ObservedMeanBrightness;

    public double? ObservedEventDensityPerSecond => _preview?.ObservedEventDensityPerSecond;

    public int? ObservedPeakConcurrentFocalProps => _preview?.ObservedPeakConcurrentFocalProps;

    public double? ObservedDarknessFraction => _preview?.ObservedDarknessFraction;

    /// <summary>
    /// Opens (or switches to) <paramref name="presetName"/>'s own independently persisted budget, loaded
    /// through <paramref name="catalog"/>, or <see cref="DefaultBudget"/> if nothing has ever been saved
    /// under that name. Calling this again with a different <paramref name="presetName"/> at any time loads
    /// that preset's own independently saved budget and clears any prior <see cref="Rows"/> preview.
    /// </summary>
    public async Task<bool> OpenPresetAsync(IVisualBudgetPresetCatalog catalog, string presetName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var preset = await catalog.GetAsync(presetName, ct).ConfigureAwait(true);
            _presetName = presetName;
            _version = preset?.Version;
            _currentBudget = preset?.Budget ?? DefaultBudget;
            _preview = null;
            LoadState = ScreenLoadState.Loaded;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (Exception ex)
        {
            _presetName = null;
            _version = null;
            _currentBudget = DefaultBudget;
            _preview = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>
    /// Saves <paramref name="budget"/> under the currently open preset name via <see cref="IVisualBudgetPresetCatalog.SaveAsync"/>.
    /// Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when no preset is open or the
    /// catalog rejects the budget -- never a partial mutation of <see cref="CurrentBudget"/>.
    /// </summary>
    public async Task<bool> SaveAsync(IVisualBudgetPresetCatalog catalog, VisualBudget budget, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(budget);

        if (_presetName is not { } presetName)
        {
            ActionErrorMessage = "Open a preset name before saving a budget.";
            return false;
        }

        try
        {
            var saved = await catalog.SaveAsync(presetName, budget, ct).ConfigureAwait(true);
            _currentBudget = saved.Budget;
            _version = saved.Version;
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

    /// <summary>
    /// Resolves <paramref name="candidates"/> against <see cref="CurrentBudget"/> over <paramref name="evaluationWindow"/>
    /// via <paramref name="resolver"/> and publishes the outcome through <see cref="Rows"/>/<see cref="Warnings"/>/the
    /// observed-metric properties. Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>) when
    /// no preset is open or <paramref name="resolver"/> rejects the inputs -- never a partial preview.
    /// </summary>
    public bool Preview(IVisualBudgetResolver resolver, IReadOnlyList<VisualBudgetCandidate> candidates, TimeWindow evaluationWindow)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(candidates);

        if (_presetName is null)
        {
            ActionErrorMessage = "Open a preset name before previewing a candidate set.";
            return false;
        }

        try
        {
            _preview = resolver.Resolve(candidates, _currentBudget, evaluationWindow);
            ActionErrorMessage = null;
            RaisePreviewChanged();
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
        OnPropertyChanged(nameof(PresetName));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(CurrentBudget));
        RaisePreviewChanged();
    }

    private void RaisePreviewChanged()
    {
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Warnings));
        OnPropertyChanged(nameof(ObservedMeanBrightness));
        OnPropertyChanged(nameof(ObservedEventDensityPerSecond));
        OnPropertyChanged(nameof(ObservedPeakConcurrentFocalProps));
        OnPropertyChanged(nameof(ObservedDarknessFraction));
    }
}
