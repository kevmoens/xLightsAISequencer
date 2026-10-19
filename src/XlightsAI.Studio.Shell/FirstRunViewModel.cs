using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-15 Step 095 ("Build installer/uninstaller, show-folder onboarding and safe defaults").
/// First-run onboarding: the user chooses a workspace folder (where projects are stored) before
/// anything else, never via an environment variable or a command-line argument — this step's own
/// required acceptance, verbatim: "User completes first run without command line." Also probes the
/// real machine's <see cref="RuntimeCapability"/> and runs <see cref="IStartupHealthChecks"/>
/// (both Step 094 boundaries, reused rather than re-implemented) so an unsupported runtime or a
/// missing/incompatible release asset is shown as a clear, human-readable diagnostic instead of a
/// silent failure later — this step's own required failure/edge case, verbatim: "Clean machine and
/// unsupported runtime must produce user-readable diagnostics." A diagnostic never blocks
/// completion by itself (Section 1: the app must still work, degraded, offline/without ML) —
/// only an actually unusable workspace choice (empty, or an administrator-only location) does.
/// </summary>
public sealed class FirstRunViewModel : ObservableObject
{
    private string _workspaceDirectory = string.Empty;
    private bool _isBusy;
    private string? _errorMessage;
    private RuntimeCapability? _probedCapability;
    private IReadOnlyList<string> _healthIssues = Array.Empty<string>();
    private bool _isCompleted;
    private bool _optionalAssetConsent;
    private bool _isInstallingOptionalAsset;
    private string? _optionalAssetMessage;

    /// <summary>Bindable workspace folder text for the first-run form — never an environment variable, never admin-only storage (Section 1/2).</summary>
    public string WorkspaceDirectory
    {
        get => _workspaceDirectory;
        set => SetField(ref _workspaceDirectory, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>The real probed runtime capability (Step 094's own <see cref="IRuntimeCapabilityDetector"/>), or <see langword="null"/> until a completion attempt has run.</summary>
    public RuntimeCapability? ProbedCapability
    {
        get => _probedCapability;
        private set => SetField(ref _probedCapability, value);
    }

    /// <summary>
    /// Human-readable diagnostics from <see cref="IStartupHealthChecks"/> (unsupported OS/arch,
    /// a missing or tampered required release asset). Never blocks completion by itself — "Clean
    /// machine and unsupported runtime must produce user-readable diagnostics," not a crash or a
    /// refusal to continue in a degraded/offline mode.
    /// </summary>
    public IReadOnlyList<string> HealthIssues
    {
        get => _healthIssues;
        private set
        {
            if (SetField(ref _healthIssues, value))
            {
                OnPropertyChanged(nameof(HasHealthIssues));
            }
        }
    }

    public bool HasHealthIssues => HealthIssues.Count > 0;

    /// <summary>PHASE-15 Step 096: bindable consent checkbox for <see cref="InstallOptionalAssetAsync"/> — Section 1: "optionally download ML models," never installed without this being explicitly set <see langword="true"/> first.</summary>
    public bool OptionalAssetConsent
    {
        get => _optionalAssetConsent;
        set => SetField(ref _optionalAssetConsent, value);
    }

    public bool IsInstallingOptionalAsset
    {
        get => _isInstallingOptionalAsset;
        private set => SetField(ref _isInstallingOptionalAsset, value);
    }

    /// <summary>
    /// Human-readable outcome of the most recent <see cref="InstallOptionalAssetAsync"/> call — "missing
    /// models explained" (this step's own required acceptance, verbatim), whether that outcome was a
    /// declined consent, no asset declared yet, a successful install, or a recoverable failure.
    /// </summary>
    public string? OptionalAssetMessage
    {
        get => _optionalAssetMessage;
        private set => SetField(ref _optionalAssetMessage, value);
    }

    /// <summary>Set once <see cref="CompleteFirstRunAsync"/> has succeeded at least once this session; the caller (ShellViewModel) is the sole authority on the persisted equivalent (<see cref="AppSettings.HasCompletedFirstRun"/>).</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        private set => SetField(ref _isCompleted, value);
    }

    /// <summary>
    /// Restores a previously persisted workspace folder into the bindable <see cref="WorkspaceDirectory"/>
    /// text without touching the filesystem — same "restore, never re-run" convention as
    /// <see cref="ShowImportViewModel.RestoreLastDirectory"/>.
    /// </summary>
    public void RestoreLastWorkspaceDirectory(string? lastWorkspaceDirectory)
    {
        if (!string.IsNullOrWhiteSpace(lastWorkspaceDirectory) && string.IsNullOrEmpty(WorkspaceDirectory))
        {
            WorkspaceDirectory = lastWorkspaceDirectory;
        }
    }

    /// <summary>
    /// Validates and creates <see cref="WorkspaceDirectory"/> (never an admin-only location, never
    /// empty), probes the real machine's <see cref="RuntimeCapability"/>, and runs
    /// <see cref="IStartupHealthChecks"/>, recording any issues as non-blocking diagnostics. Returns
    /// <see langword="true"/> (and sets <see cref="IsCompleted"/>) only once the workspace folder
    /// itself is confirmed valid and creatable; an invalid/unusable folder choice, a cancellation,
    /// or an unexpected I/O failure sets <see cref="ErrorMessage"/> and returns <see langword="false"/>
    /// without ever marking first-run complete — same "catch, set an error property, never let an
    /// exception reach the UI" convention every other screen action in this project already follows.
    /// </summary>
    public async Task<bool> CompleteFirstRunAsync(
        IRuntimeCapabilityDetector capabilityDetector,
        IStartupHealthChecks healthChecks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(capabilityDetector);
        ArgumentNullException.ThrowIfNull(healthChecks);

        // Reentrancy guard: same synchronous check-and-set pattern as ShowImportViewModel.DiscoverAsync.
        if (_isBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (string.IsNullOrWhiteSpace(WorkspaceDirectory))
            {
                ErrorMessage = "Choose a workspace folder before continuing.";
                return false;
            }

            if (IsAdministratorOnlyLocation(WorkspaceDirectory, out var adminLocation))
            {
                ErrorMessage =
                    $"Choose a folder you own. '{adminLocation}' is an administrator-only location and " +
                    "is not supported for project storage.";
                return false;
            }

            Directory.CreateDirectory(WorkspaceDirectory);

            var capability = await capabilityDetector.ProbeAsync(ct).ConfigureAwait(true);
            ProbedCapability = capability;

            var issues = await healthChecks.CheckAsync(ct).ConfigureAwait(true);
            HealthIssues = issues;

            IsCompleted = true;
            return true;
        }
        catch (Exception ex)
        {
            ProbedCapability = null;
            HealthIssues = Array.Empty<string>();
            IsCompleted = false;
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// PHASE-15 Step 096 ("Add opt-in model asset installs, offline validation, logs and update
    /// notices"): installs a single declared optional release/model asset only with explicit
    /// <paramref name="userConsent"/> — Section 1: "optionally download ML models." Never blocks or
    /// undoes an already-completed first run on failure; a missing declared asset, a declined
    /// consent, a hash-mismatch, an offline/network failure, or a cancellation is reported as a
    /// clear, human-readable <see cref="OptionalAssetMessage"/> instead of a crash or a silent no-op
    /// — this step's own required acceptance/failure-case, verbatim: "Core app works offline after
    /// assets installed; missing models explained" / "Missing or incompatible model, cancellation
    /// and offline mode must yield a recoverable state." Returns <see langword="true"/> only on an
    /// actual successful install.
    /// </summary>
    public async Task<bool> InstallOptionalAssetAsync(
        DownloadableAsset? asset, bool userConsent, IAssetInstaller installer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(installer);

        // Reentrancy guard: same synchronous check-and-set pattern as CompleteFirstRunAsync above.
        if (_isInstallingOptionalAsset)
        {
            return false;
        }

        if (asset is null)
        {
            OptionalAssetMessage = "No optional model asset is declared for this build yet; continuing without it.";
            return false;
        }

        if (!userConsent)
        {
            OptionalAssetMessage =
                $"Skipped optional asset '{asset.Id}' (not installed without consent). The feature it " +
                "enables stays disabled until you choose to install it.";
            return false;
        }

        IsInstallingOptionalAsset = true;
        try
        {
            var path = await installer.EnsureInstalledAsync(asset, progress: null, ct).ConfigureAwait(true);
            OptionalAssetMessage = $"Installed optional asset '{asset.Id}' (version {asset.Version}) at '{path}'.";
            return true;
        }
        catch (OperationCanceledException)
        {
            OptionalAssetMessage =
                $"Installing optional asset '{asset.Id}' was cancelled; it was not installed. You can try again later.";
            return false;
        }
        catch (AssetInstallationHashMismatchException ex)
        {
            OptionalAssetMessage = ex.Message;
            return false;
        }
        catch (AssetInstallationFailedException ex)
        {
            OptionalAssetMessage = ex.Message;
            return false;
        }
        finally
        {
            IsInstallingOptionalAsset = false;
        }
    }

    /// <summary>
    /// Section 1/implementation guidance: "no environment variable/Python setup or admin-only
    /// project storage." Rejects a workspace folder that resolves under any well-known
    /// administrator-only Windows location (Program Files, Program Files (x86), or the Windows
    /// install directory itself) — the same explicit, named-location convention
    /// <see cref="XlightsAI.Studio.Distribution.RuntimeCapabilityDetector"/> already uses for its
    /// own candidate-path probing, rather than a registry/permissions probe.
    /// </summary>
    private static bool IsAdministratorOnlyLocation(string candidateDirectory, out string matchedLocation)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidateDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unparsable path is not this check's concern — CreateDirectory below will surface
            // the real, specific I/O error instead.
            matchedLocation = string.Empty;
            return false;
        }

        foreach (var special in AdminOnlySpecialFolders)
        {
            var folderPath = Environment.GetFolderPath(special);
            if (string.IsNullOrEmpty(folderPath))
            {
                continue;
            }

            if (fullPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
            {
                matchedLocation = folderPath;
                return true;
            }
        }

        matchedLocation = string.Empty;
        return false;
    }

    private static readonly Environment.SpecialFolder[] AdminOnlySpecialFolders =
    [
        Environment.SpecialFolder.ProgramFiles,
        Environment.SpecialFolder.ProgramFilesX86,
        Environment.SpecialFolder.Windows,
    ];
}
