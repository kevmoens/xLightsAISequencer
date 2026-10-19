using XlightsAI.Studio.Composition;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-10 Step 070 ("Implement build, silence, transitions, release and phrase-ending recipes") -- SCREEN 12
/// CHOREOGRAPHY's own bindable build/silence/transition/release/phrase-ending recipe editor view model. Owns
/// the UI-facing projection of one phrase's <see cref="TransitionPlan"/> -- planned via
/// <see cref="ITransitionRecipePlanner"/>, loaded/saved through <see cref="ITransitionPlanCatalog"/>. Contains
/// no Avalonia types, the same convention every other screen view model in this project already follows
/// (<see cref="VisualStoryEditorViewModel"/>/<see cref="VisualBudgetEditorViewModel"/>).
///
/// <para>
/// Mirrors <see cref="VisualStoryEditorViewModel"/>'s own explicit loading/empty/error/loaded state machine,
/// its own "catch the domain's typed invalid-input exception, set <see cref="ActionErrorMessage"/>, leave
/// bindable state completely unchanged on failure" convention for every mutating action, and its own
/// plan-then-save split: <see cref="Plan"/> computes a new plan in memory without persisting it (so a user can
/// inspect the recipe before committing), <see cref="SaveAsync"/> persists whatever <see cref="Plan"/> most
/// recently computed.
/// </para>
///
/// <para>
/// Required acceptance, verbatim: "Musical break can intentionally darken all permitted props" -- proven
/// directly at the domain layer by <see cref="TransitionRecipePlanner"/>'s own acceptance test; this view
/// model's own job is only to surface that same plan as a bindable table (<see cref="Rows"/>) plus a
/// persisted, saved-state-verified plan a real SCREEN 12 view would bind to.
/// </para>
/// </summary>
public sealed class TransitionRecipeEditorViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;
    private Guid? _phraseId;
    private int? _version;
    private TransitionPlan? _savedPlan;
    private TransitionPlan? _pendingPlan;

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

    /// <summary>The phrase id currently open, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public Guid? PhraseId => _phraseId;

    /// <summary>The currently persisted version for <see cref="PhraseId"/>, or <see langword="null"/> while <see cref="IsEmpty"/> or before the first save.</summary>
    public int? Version => _version;

    /// <summary>
    /// The real, currently-active rows for the open phrase -- the most recently <see cref="Plan"/>ned plan if
    /// one has not yet been saved, else the most recently persisted plan; empty exactly when
    /// <see cref="IsLoaded"/> is <see langword="false"/> or nothing has ever been planned/saved.
    /// </summary>
    public IReadOnlyList<TransitionGestureRowView> Rows
    {
        get
        {
            var current = _pendingPlan ?? _savedPlan;
            if (current is null)
            {
                return [];
            }

            return current.Gestures
                .Select(g => new TransitionGestureRowView(
                    g.Id,
                    g.Kind,
                    g.TargetProps,
                    g.TargetProps.Count,
                    (int)Math.Round(g.StartIntensity * 100.0),
                    (int)Math.Round(g.EndIntensity * 100.0),
                    g.Kind == TransitionKind.Blackout))
                .ToList();
        }
    }

    /// <summary><see langword="true"/> exactly when <see cref="Plan"/> has computed a plan that has not yet been <see cref="SaveAsync"/>d.</summary>
    public bool HasUnsavedPlan => _pendingPlan is not null;

    /// <summary>
    /// Opens (or switches to) <paramref name="phraseId"/>'s own independently persisted plan, loaded through
    /// <paramref name="catalog"/>. Calling this again with a different <paramref name="phraseId"/> at any time
    /// loads that phrase's own independently saved plan and discards any unsaved <see cref="Plan"/> for the
    /// previous phrase.
    /// </summary>
    public async Task<bool> OpenPhraseAsync(ITransitionPlanCatalog catalog, Guid phraseId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var record = await catalog.GetAsync(phraseId, ct).ConfigureAwait(true);
            _phraseId = phraseId;
            _version = record?.Version;
            _savedPlan = record?.Plan;
            _pendingPlan = null;
            LoadState = ScreenLoadState.Loaded;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (Exception ex)
        {
            _phraseId = null;
            _version = null;
            _savedPlan = null;
            _pendingPlan = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>
    /// Plans a new plan from <paramref name="requests"/> via <paramref name="planner"/> for the currently open
    /// phrase, publishing it through <see cref="Rows"/> without persisting it. Returns <see langword="false"/>
    /// (via <see cref="ActionErrorMessage"/>) when no phrase is open or <paramref name="planner"/> rejects the
    /// inputs -- never a partial plan.
    /// </summary>
    public bool Plan(ITransitionRecipePlanner planner, IReadOnlyList<TransitionRecipeRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(requests);

        if (_phraseId is null)
        {
            ActionErrorMessage = "Open a phrase before planning a transition recipe.";
            return false;
        }

        try
        {
            _pendingPlan = planner.Plan(requests);
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
    /// Saves the most recently <see cref="Plan"/>ned plan under the currently open phrase via
    /// <paramref name="catalog"/>. Returns <see langword="false"/> (via <see cref="ActionErrorMessage"/>)
    /// when no phrase is open, nothing has been planned yet, or the catalog rejects the plan -- never a
    /// partial mutation of the persisted <see cref="Rows"/>.
    /// </summary>
    public async Task<bool> SaveAsync(ITransitionPlanCatalog catalog, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (_phraseId is not { } phraseId)
        {
            ActionErrorMessage = "Open a phrase before saving a transition recipe.";
            return false;
        }

        if (_pendingPlan is not { } pending)
        {
            ActionErrorMessage = "Plan a transition recipe before saving it.";
            return false;
        }

        try
        {
            var saved = await catalog.SaveAsync(phraseId, pending, ct).ConfigureAwait(true);
            _savedPlan = saved.Plan;
            _version = saved.Version;
            _pendingPlan = null;
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
        OnPropertyChanged(nameof(PhraseId));
        OnPropertyChanged(nameof(Version));
        RaisePendingChanged();
    }

    private void RaisePendingChanged()
    {
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(HasUnsavedPlan));
    }
}
