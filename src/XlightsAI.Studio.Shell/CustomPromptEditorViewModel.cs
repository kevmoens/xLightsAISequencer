using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Director;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Build-plan Phase 14 §2b.2: the "last stop before you generate" custom-prompt tab's own view
/// model, same shape/persistence discipline as <see cref="AiProviderSettingsEditorViewModel"/> --
/// load-on-navigate, explicit Save, visible status/error text. Contains no Avalonia types, the same
/// convention every other screen view model in this project already follows.
///
/// Unlike <see cref="AiProviderSettingsEditorViewModel"/> (self-contained, constructs its own store
/// in its view's code-behind), this one is owned by <see cref="ShellViewModel"/> directly: the
/// "Generate whole-song plan" action needs to read this screen's live text at generation time, the
/// same way it already reads <c>ShowImportViewModel</c>'s live state -- a self-contained view model
/// nothing else in the shell holds a reference to could not be read from there.
/// </summary>
public sealed class CustomPromptEditorViewModel : ObservableObject
{
    private readonly ICustomPromptStore _store;
    private string _promptText = "";
    private bool _stayCloseToExemplars = true;
    private string _selectedStyle = StylePresetCatalog.HighEnergyEdmId;
    private bool _enableQualityPass;
    private string? _statusMessage;
    private string? _errorMessage;

    public CustomPromptEditorViewModel(ICustomPromptStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        // User feedback (2026-11-01): previously the style was fixed at app-startup composition-root
        // time (Program.cs hardcoded "High-Energy / EDM" for every generation, every song) with no UI
        // to change it -- this is that control. Defaults to the same id Program.cs has always hardcoded,
        // so a user who never touches this control keeps getting exactly today's behavior.
        AvailableStyles = StylePresetCatalog.All
            .Select(s => new StyleOption(s.Id, s.Name, s.Description))
            .ToList();

        SaveCommand = new DelegateCommand(() => _ = SaveAsync());

        // Build-plan Phase 14 §2b.3: each chip gets its own bindable DelegateCommand (this project's
        // minimal ICommand has no CommandParameter-aware overload -- see DelegateCommand's own doc
        // comment) rather than one shared parameterized command.
        Examples = ExampleTemplates
            .Select(example => new CustomPromptExampleChip(example.Category, example.Text, new DelegateCommand(() => ApplyExample(example.Text))))
            .ToList();
    }

    public const int MaxLength = CustomGenerationPrompt.MaxLength;

    /// <summary>Fire-and-forget wrapper around <see cref="SaveAsync"/> for XAML <c>Button.Command</c> binding, same reason <see cref="AiProviderSettingsEditorViewModel.SaveCommand"/> exists.</summary>
    public DelegateCommand SaveCommand { get; }

    /// <summary>Build-plan Phase 14 §2b.3: the example prompts shown as clickable chips, spanning colors/effects/layers/intensity/pacing/prop-focus -- each carrying its own ready-to-bind <see cref="CustomPromptExampleChip.ApplyCommand"/>.</summary>
    public IReadOnlyList<CustomPromptExampleChip> Examples { get; }

    /// <summary>
    /// The live, currently-typed creative direction -- clamped to <see cref="MaxLength"/> rather than
    /// rejected outright (a user pasting slightly-too-long text keeps everything up to the cap rather
    /// than losing the whole paste). This is the value <see cref="ShellViewModel"/> reads directly at
    /// generation time, whether or not it has been explicitly saved since the last edit -- the screen
    /// is a "last stop before you generate" waypoint, not a save-gated settings form.
    /// </summary>
    public string PromptText
    {
        get => _promptText;
        set
        {
            var clamped = value.Length > MaxLength ? value[..MaxLength] : value;
            if (SetField(ref _promptText, clamped))
            {
                OnPropertyChanged(nameof(RemainingCharacters));
            }
        }
    }

    /// <summary>Live remaining-character count against the §2a cap, for the UI's own counter.</summary>
    public int RemainingCharacters => MaxLength - PromptText.Length;

    public bool HasText => PromptText.Length > 0;

    /// <summary>
    /// User feedback (2026-10-31): checked (the default) keeps exemplar retrieval locked to the
    /// deterministic closest real corpus match every generation -- unchecking it lets a "regenerate" draw
    /// a genuinely different (not just differently-colored) set of worked examples each time, via
    /// <see cref="XlightsAI.Studio.Pipeline.ExemplarIndex.FindVaried"/> -- see
    /// <see cref="CustomGenerationPrompt.StayCloseToExemplars"/> for the full mechanism and its
    /// deliberate prompt-cache tradeoff.
    /// </summary>
    public bool StayCloseToExemplars
    {
        get => _stayCloseToExemplars;
        set => SetField(ref _stayCloseToExemplars, value);
    }

    /// <summary>Every selectable style preset (id, display name, one-sentence description) for this screen's style picker, in <see cref="StylePresetCatalog.All"/> order.</summary>
    public IReadOnlyList<StyleOption> AvailableStyles { get; }

    /// <summary>
    /// The style id this generation will use (<see cref="CustomGenerationPrompt.Style"/>) -- defaults to
    /// <see cref="StylePresetCatalog.HighEnergyEdmId"/>, the same id every generation has always used
    /// before this control existed, so a user who never opens this screen keeps today's exact behavior.
    /// </summary>
    public string SelectedStyle
    {
        get => _selectedStyle;
        set
        {
            if (SetField(ref _selectedStyle, value))
            {
                OnPropertyChanged(nameof(SelectedStyleDescription));
            }
        }
    }

    /// <summary>One-sentence description of <see cref="SelectedStyle"/>, for the screen's own helper text under the picker.</summary>
    public string SelectedStyleDescription =>
        AvailableStyles.FirstOrDefault(s => s.Id == SelectedStyle)?.Description ?? string.Empty;

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 3 (2026-11-02): unchecked (the default, and every
    /// pre-existing save/generation's behavior) keeps the original single-pass generation. Checked opts
    /// this generation into the real render-&gt;measure-&gt;critique-&gt;revise loop -- genuinely slower, and
    /// only effective once an xLights executable/show directory is configured on the Show Import screen
    /// (see <see cref="CustomGenerationPrompt.EnableQualityPass"/>'s own doc comment for the exact
    /// unavailability message otherwise).
    /// </summary>
    public bool EnableQualityPass
    {
        get => _enableQualityPass;
        set => SetField(ref _enableQualityPass, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetField(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool HasStatusMessage => StatusMessage is not null;

    public bool HasErrorMessage => ErrorMessage is not null;

    /// <summary>Loads the persisted prompt (or the empty default) -- called on navigating to this screen, same "restore, never re-run" convention <c>ShowImport.RestoreLastDirectory</c> already uses.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        ErrorMessage = null;
        try
        {
            var loaded = await _store.LoadAsync(ct).ConfigureAwait(true);
            PromptText = loaded.Text;
            StayCloseToExemplars = loaded.StayCloseToExemplars;
            // A null Style (every save from before this control existed) keeps the field's own default
            // (HighEnergyEdmId, the hardcoded id every generation has always used) rather than clearing
            // a never-yet-made choice to some other value.
            if (loaded.Style is { } style)
            {
                SelectedStyle = style;
            }

            EnableQualityPass = loaded.EnableQualityPass;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Persists <see cref="PromptText"/> so it round-trips across sessions (build-plan Phase 14 §2a.2).</summary>
    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            await _store.SaveAsync(new CustomGenerationPrompt(PromptText, StayCloseToExemplars, SelectedStyle, EnableQualityPass), ct).ConfigureAwait(true);
            StatusMessage = "Saved.";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Build-plan Phase 14 §2b.3: replaces <see cref="PromptText"/> with one of the example chips' template text -- a direct replace (not an append), the simpler of the two behaviors the plan's own wording ("inserting (or offering to replace)") allows.</summary>
    private void ApplyExample(string exampleText) => PromptText = exampleText;

    private static readonly IReadOnlyList<(string Category, string Text)> ExampleTemplates =
    [
        ("Colors", "Use a red and gold palette throughout; avoid blue."),
        ("Effects", "Favor Shockwave and Meteors; avoid plain Bars/Color Wash."),
        ("Layers", "Keep the Mega Tree densely layered (3+ concurrent effects) during every chorus."),
        ("Intensity", "Keep this calm and low-intensity overall -- this is for a quiet residential street."),
        ("Pacing", "Make the first chorus a big surprise moment relative to the verses before it."),
        ("Prop focus", "Give the Matrix the most visual variety; keep the icicle lights simple and steady."),
    ];
}

/// <summary>One clickable example-prompt chip: <paramref name="Category"/> is the chip's own short label, <paramref name="Text"/> is what it inserts, <paramref name="ApplyCommand"/> is bound directly to a button's <c>Command</c>.</summary>
public sealed record CustomPromptExampleChip(string Category, string Text, DelegateCommand ApplyCommand);

/// <summary>One selectable entry in the style picker, condensed from a <see cref="StylePreset"/> to just what the UI needs.</summary>
public sealed record StyleOption(string Id, string Name, string Description);
