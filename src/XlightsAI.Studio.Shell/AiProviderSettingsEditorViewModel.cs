using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Director;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// The Settings screen's own "connect an AI provider" editor -- one row per <see cref="AiRole"/>
/// (Director/Choreographer/Critic, independently configurable per architecture §14), each persisted
/// via <see cref="IRoleProviderConfigStore"/> and its own credential (if any) via
/// <see cref="ICredentialStore"/> -- the same DPAPI-encrypted local file <see cref="DpapiCredentialStore"/>
/// already provides, reused directly, not reimplemented. Contains no Avalonia types, the same convention
/// every other screen view model in this project already follows.
/// </summary>
public sealed class AiProviderSettingsEditorViewModel : ObservableObject
{
    private readonly IRoleProviderConfigStore _configStore;
    private readonly ICredentialStore _credentialStore;

    private AiRole _selectedRole = AiRole.Director;
    private RoleProviderConfig _currentConfig = RoleProviderConfig.Disabled(AiRole.Director);
    private bool _hasStoredCredential;
    private string _credentialInput = "";
    private string? _statusMessage;
    private string? _errorMessage;

    public AiProviderSettingsEditorViewModel(IRoleProviderConfigStore configStore, ICredentialStore credentialStore)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));

        SaveCommand = new DelegateCommand(() => _ = SaveAsync());
        DisableAndClearCredentialCommand = new DelegateCommand(() => _ = DisableAndClearCredentialAsync());
    }

    /// <summary>Fire-and-forget wrapper around <see cref="SaveAsync"/> for XAML <c>Button.Command</c> binding (this project's minimal <see cref="DelegateCommand"/> has no async-aware overload).</summary>
    public DelegateCommand SaveCommand { get; }

    /// <summary>Fire-and-forget wrapper around <see cref="DisableAndClearCredentialAsync"/>, same reason as <see cref="SaveCommand"/>.</summary>
    public DelegateCommand DisableAndClearCredentialCommand { get; }

    public IReadOnlyList<AiRole> Roles { get; } = Enum.GetValues<AiRole>();

    public IReadOnlyList<AiProviderType> ProviderTypes { get; } = Enum.GetValues<AiProviderType>();

    public AiRole SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetField(ref _selectedRole, value))
            {
                _ = LoadAsync();
            }
        }
    }

    /// <summary>The config currently being edited for <see cref="SelectedRole"/> -- reloaded by <see cref="LoadAsync"/>, mutated locally until <see cref="SaveAsync"/>.</summary>
    public RoleProviderConfig CurrentConfig
    {
        get => _currentConfig;
        set => SetField(ref _currentConfig, value);
    }

    /// <summary>Whether a credential is already stored for this role -- shown so a user never has to guess whether re-typing is required.</summary>
    public bool HasStoredCredential
    {
        get => _hasStoredCredential;
        private set => SetField(ref _hasStoredCredential, value);
    }

    /// <summary>
    /// A transient, write-only staging field for a new credential value -- never pre-filled from a
    /// loaded config (the real secret is never read back for display, only overwritten), and cleared
    /// immediately after a successful save so the plaintext does not linger in view-model state.
    /// </summary>
    public string CredentialInput
    {
        get => _credentialInput;
        set => SetField(ref _credentialInput, value);
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

    /// <summary>Loads <see cref="SelectedRole"/>'s own persisted config (or the disabled default) and whether a credential already exists for it.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        ErrorMessage = null;
        try
        {
            CurrentConfig = await _configStore.LoadAsync(SelectedRole, ct).ConfigureAwait(true);
            HasStoredCredential = CurrentConfig.CredentialName is { Length: > 0 } name
                && await _credentialStore.TryGetAsync(name, ct).ConfigureAwait(true) is not null;
            CredentialInput = "";
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Saves <see cref="CurrentConfig"/> (provider type/endpoint/model/enabled) and, when
    /// <see cref="CredentialInput"/> is non-empty, encrypts and stores it under a per-role credential
    /// name (<c>ai-provider-{role}</c>) -- two independent writes, exactly like <c>AiProviderSettings</c>/
    /// <c>DpapiCredentialStore</c> already do for the single-role AiEdits planner. A blank
    /// <see cref="CredentialInput"/> leaves any already-stored credential untouched (never silently
    /// clears it).
    /// </summary>
    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var credentialName = $"ai-provider-{SelectedRole.ToString().ToLowerInvariant()}";
            var configToSave = CurrentConfig with { Role = SelectedRole, CredentialName = credentialName };

            if (CredentialInput.Length > 0)
            {
                await _credentialStore.StoreAsync(credentialName, CredentialInput, ct).ConfigureAwait(true);
                HasStoredCredential = true;
                CredentialInput = "";
            }

            await _configStore.SaveAsync(configToSave, ct).ConfigureAwait(true);
            CurrentConfig = configToSave;
            StatusMessage = $"Saved {SelectedRole} provider settings.";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Removes the stored credential (if any) for <see cref="SelectedRole"/> and disables it -- never leaves a dangling credential name pointing at a now-deleted secret.</summary>
    public async Task<bool> DisableAndClearCredentialAsync(CancellationToken ct = default)
    {
        ErrorMessage = null;
        try
        {
            var credentialName = $"ai-provider-{SelectedRole.ToString().ToLowerInvariant()}";
            await _credentialStore.DeleteAsync(credentialName, ct).ConfigureAwait(true);
            var disabled = RoleProviderConfig.Disabled(SelectedRole);
            await _configStore.SaveAsync(disabled, ct).ConfigureAwait(true);
            CurrentConfig = disabled;
            HasStoredCredential = false;
            CredentialInput = "";
            StatusMessage = $"Disabled {SelectedRole} and removed its stored credential.";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }
}
