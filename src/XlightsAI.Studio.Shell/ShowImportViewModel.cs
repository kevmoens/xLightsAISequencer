using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-02 Step 009 — SCREEN 04 SHOW IMPORT, read-only initial discovery. PHASE-02 Step 010 extends
/// the same Discover action: once the required file is confirmed present, it is also parsed into a
/// normalized <see cref="Layout"/> (models/groups/geometry). Mirrors <see cref="ProjectsViewModel"/>'s
/// loading/empty/error/loaded and busy/action-error conventions: a thrown exception from either the
/// discovery service or the layout importer is always surfaced as an explicit, user-readable error,
/// never an unhandled exception or a silently blank screen.
/// </summary>
public sealed class ShowImportViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private ShowDirectoryDiscovery? _discovery;
    private DisplayGraph? _layout;
    private string _showDirectory = string.Empty;
    private bool _isBusy;

    // PHASE-02 Step 012: "Create effect plan containing three hardcoded simple effects on selected
    // real props." Deliberately separate busy/error/result state from the Discover flow above —
    // generating a plan never re-runs or invalidates an already-successful Layout, and a failed plan
    // attempt never clobbers a previously generated, still-valid Plan (the required failure path:
    // "Malformed proposal... must not auto-commit").
    private string _effectTargetNamesCsv = string.Empty;
    private long _songDurationTicks = DefaultSongDurationTicks;
    private bool _isPlanBusy;
    private string? _planErrorMessage;
    private SequenceDocument? _plan;
    private string? _planSavedPath;

    // PHASE-02 Step 013: "Port minimal editable XSQ writer and required sequencing metadata."
    // Deliberately separate busy/error/result state from the Discover/Generate-plan flows above,
    // same reasoning as Plan's own separation from Discover — exporting never re-runs Discover or
    // Generate, and a failed export attempt never clobbers a previously successful ExportResult.
    private string _xsqOutputPath = string.Empty;
    private string _targetXlightsVersion = "2026.03";
    private bool _isExportBusy;
    private string? _exportErrorMessage;
    private ExportResult? _lastExport;

    // PHASE-02 Step 015: "Implement atomic export, filename safety, backups..." Defaults to
    // RejectIfExists — Step 013's own original, unmodified default behavior — so no existing screen
    // state or test changes unless a caller explicitly chooses a different policy.
    private XsqExportPolicy _exportPolicy = XsqExportPolicy.RejectIfExists;

    // PHASE-02 Step 014: "Open output in installed xLights; verify duration, timing, models and
    // visible effects." Deliberately separate busy/error/result state from the Export flow above,
    // same reasoning as Export's own separation from Generate — launching never re-runs Export, and
    // a failed launch attempt never clobbers a previously successful LastLaunch.
    private string _xlightsExecutablePath = string.Empty;
    private bool _isLaunchBusy;
    private string? _launchErrorMessage;
    private XlightsLaunchResult? _lastLaunch;

    /// <summary>30 seconds at the canonical 100 ns/tick unit — a reasonable demo default; a real song duration is Phase 04's own <c>SongAsset</c> concern.</summary>
    public const long DefaultSongDurationTicks = 300_000_000;

    /// <summary>Bindable directory text for the picker form.</summary>
    public string ShowDirectory
    {
        get => _showDirectory;
        set => SetField(ref _showDirectory, value);
    }

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

    public ShowDirectoryDiscovery? Discovery
    {
        get => _discovery;
        private set => SetField(ref _discovery, value);
    }

    /// <summary>PHASE-02 Step 010: the normalized models/groups/geometry parsed from this directory's <c>xlights_rgbeffects.xml</c>, once a discovery has completed successfully.</summary>
    public DisplayGraph? Layout
    {
        get => _layout;
        private set => SetField(ref _layout, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public bool IsLoading => LoadState == ScreenLoadState.Loading;

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>Bindable comma-separated "choose real props" input (Step 012); e.g. "Roofline, Arch 1, Arch 2" for the phase gate demo.</summary>
    public string EffectTargetNamesCsv
    {
        get => _effectTargetNamesCsv;
        set => SetField(ref _effectTargetNamesCsv, value);
    }

    /// <summary>Bindable "known song duration" (Step 012 implementation guidance), in 100 ns ticks.</summary>
    public long SongDurationTicks
    {
        get => _songDurationTicks;
        set => SetField(ref _songDurationTicks, value);
    }

    public bool IsPlanBusy
    {
        get => _isPlanBusy;
        private set => SetField(ref _isPlanBusy, value);
    }

    public string? PlanErrorMessage
    {
        get => _planErrorMessage;
        private set => SetField(ref _planErrorMessage, value);
    }

    /// <summary>The most recently generated plan (Step 012 required acceptance: "Plan is serialized and inspectable independently of XSQ"); <see langword="null"/> until a Generate attempt succeeds.</summary>
    public SequenceDocument? Plan
    {
        get => _plan;
        private set
        {
            if (SetField(ref _plan, value))
            {
                OnPropertyChanged(nameof(HasPlan));
            }
        }
    }

    public bool HasPlan => Plan is not null;

    /// <summary>The real on-disk path <see cref="Plan"/> was last persisted to.</summary>
    public string? PlanSavedPath
    {
        get => _planSavedPath;
        private set => SetField(ref _planSavedPath, value);
    }

    /// <summary>Bindable output path for <see cref="ExportXsqAsync"/>; must end in <c>.xsq</c> and must not already exist.</summary>
    public string XsqOutputPath
    {
        get => _xsqOutputPath;
        set => SetField(ref _xsqOutputPath, value);
    }

    /// <summary>Bindable "target xLights version" (PHASE-02 Section 3: <see cref="IXsqExporter.ExportNewAsync"/>'s own parameter), written verbatim into the exported file's <c>&lt;head&gt;&lt;version&gt;</c>.</summary>
    public string TargetXlightsVersion
    {
        get => _targetXlightsVersion;
        set => SetField(ref _targetXlightsVersion, value);
    }

    public bool IsExportBusy
    {
        get => _isExportBusy;
        private set => SetField(ref _isExportBusy, value);
    }

    public string? ExportErrorMessage
    {
        get => _exportErrorMessage;
        private set => SetField(ref _exportErrorMessage, value);
    }

    /// <summary>The most recently successful export (Step 013 required acceptance: "Generated file passes XML/structural tests and round-trip parser"); <see langword="null"/> until an Export attempt succeeds.</summary>
    public ExportResult? LastExport
    {
        get => _lastExport;
        private set
        {
            if (SetField(ref _lastExport, value))
            {
                OnPropertyChanged(nameof(HasExport));
            }
        }
    }

    public bool HasExport => LastExport is not null;

    /// <summary>Bindable export-collision policy (PHASE-02 Step 015); defaults to <see cref="XsqExportPolicy.RejectIfExists"/>, Step 013's own original behavior.</summary>
    public XsqExportPolicy ExportPolicy
    {
        get => _exportPolicy;
        set => SetField(ref _exportPolicy, value);
    }

    /// <summary>Every selectable value of <see cref="ExportPolicy"/>, for the screen's own combo box.</summary>
    public static IReadOnlyList<XsqExportPolicy> ExportPolicyOptions { get; } =
        Enum.GetValues<XsqExportPolicy>();

    /// <summary>Bindable path to the installed xLights executable to open <see cref="LastExport"/> in (Step 014).</summary>
    public string XlightsExecutablePath
    {
        get => _xlightsExecutablePath;
        set => SetField(ref _xlightsExecutablePath, value);
    }

    public bool IsLaunchBusy
    {
        get => _isLaunchBusy;
        private set => SetField(ref _isLaunchBusy, value);
    }

    public string? LaunchErrorMessage
    {
        get => _launchErrorMessage;
        private set => SetField(ref _launchErrorMessage, value);
    }

    /// <summary>The most recently successful xLights launch (Step 014 required acceptance: "Human-attested integration test with screenshot/render notes" — this record is the auditable launch evidence, not a substitute for the human's own visual check); <see langword="null"/> until a Launch attempt succeeds.</summary>
    public XlightsLaunchResult? LastLaunch
    {
        get => _lastLaunch;
        private set
        {
            if (SetField(ref _lastLaunch, value))
            {
                OnPropertyChanged(nameof(HasLaunch));
            }
        }
    }

    public bool HasLaunch => LastLaunch is not null;

    /// <summary>
    /// Restores a previously persisted successful directory selection into the bindable
    /// <see cref="ShowDirectory"/> text, without touching the filesystem (discovery only ever runs
    /// on explicit user action via <see cref="DiscoverAsync"/>).
    /// </summary>
    public void RestoreLastDirectory(string? lastShowDirectory)
    {
        if (!string.IsNullOrWhiteSpace(lastShowDirectory) && string.IsNullOrEmpty(ShowDirectory))
        {
            ShowDirectory = lastShowDirectory;
        }
    }

    /// <summary>
    /// Runs discovery against <see cref="ShowDirectory"/> via <paramref name="discoveryService"/>,
    /// then — PHASE-02 Step 010 — once the required file is confirmed present, also parses it via
    /// <paramref name="layoutImporter"/> into <see cref="Layout"/> (when supplied; <see langword="null"/>
    /// preserves Step 009's discovery-only behavior for any caller that does not pass one). Returns
    /// <see langword="true"/> (and sets state Loaded) only when both discovery and parsing succeed; a
    /// missing-folder/missing-required-file/cancelled/invalid-input/malformed-XML failure sets an
    /// explicit Error state with the real message and returns <see langword="false"/> — never an
    /// unhandled exception out of a UI command, and no mutation of any state (persisted or in-memory,
    /// and never the source show file) beyond this screen's own bindable properties.
    /// </summary>
    public async Task<bool> DiscoverAsync(
        IShowDirectoryDiscoveryService discoveryService,
        IShowLayoutImporter? layoutImporter = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(discoveryService);

        // Reentrancy guard: same synchronous check-and-set pattern as
        // ProjectsViewModel.CreateProjectAsync/OpenProjectAsync.
        if (_isBusy)
        {
            return false;
        }

        IsBusy = true;
        LoadState = ScreenLoadState.Loading;
        ErrorMessage = null;
        try
        {
            var discovery = await discoveryService.DiscoverAsync(ShowDirectory, ct).ConfigureAwait(true);
            Discovery = discovery;
            Layout = null;

            if (!discovery.IsComplete)
            {
                ErrorMessage =
                    $"Missing required file(s): {string.Join(", ", discovery.MissingRequiredFiles)}.";
                LoadState = ScreenLoadState.Error;
                return false;
            }

            if (layoutImporter is not null)
            {
                Layout = await layoutImporter.ImportReadOnlyAsync(ShowDirectory, ct).ConfigureAwait(true);
            }

            LoadState = ScreenLoadState.Loaded;
            return true;
        }
        catch (Exception ex)
        {
            Discovery = null;
            Layout = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// PHASE-02 Step 012 "Generate 3 Effects" action: parses <see cref="EffectTargetNamesCsv"/>,
    /// builds a plan against the already-imported <see cref="Layout"/> via
    /// <paramref name="planFactory"/>/<paramref name="recipeRegistry"/>, and persists it via
    /// <paramref name="planStore"/>. Requires a successful Discover/parse to have already populated
    /// <see cref="Layout"/> (Step 012's own dependency on Step 010/011's contracts) — never guesses a
    /// layout. Returns <see langword="true"/> (and sets <see cref="Plan"/>/<see cref="PlanSavedPath"/>)
    /// only on full success; any failure (no layout yet, malformed target list, an unresolved target,
    /// an invalid duration, or a save I/O error) sets <see cref="PlanErrorMessage"/> and returns
    /// <see langword="false"/> without ever producing or persisting a partial/invalid plan — the
    /// required failure path: "Malformed proposal... must not auto-commit."
    /// </summary>
    public async Task<bool> GenerateEffectPlanAsync(
        IThreeEffectPlanFactory planFactory,
        IEffectRecipeRegistry recipeRegistry,
        ISequenceDocumentStore planStore,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(planFactory);
        ArgumentNullException.ThrowIfNull(recipeRegistry);
        ArgumentNullException.ThrowIfNull(planStore);

        // Reentrancy guard: same synchronous check-and-set pattern as DiscoverAsync above.
        if (_isPlanBusy)
        {
            return false;
        }

        IsPlanBusy = true;
        PlanErrorMessage = null;
        try
        {
            if (Layout is null)
            {
                PlanErrorMessage = "Import a show layout (Discover) before generating an effect plan.";
                return false;
            }

            var targetNames = EffectTargetNamesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();

            // CreatePlan itself also rejects a wrong count, but this screen's own error message is
            // phrased against the raw CSV input the user actually typed, not the parsed array.
            var plan = planFactory.CreatePlan(Layout, targetNames, SongDurationTicks, recipeRegistry);
            var savedPath = await planStore.SaveAsync(plan, ct).ConfigureAwait(true);

            Plan = plan;
            PlanSavedPath = savedPath;
            return true;
        }
        catch (Exception ex)
        {
            Plan = null;
            PlanSavedPath = null;
            PlanErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsPlanBusy = false;
        }
    }

    /// <summary>
    /// PHASE-02 Step 013 "Export XSQ" action: serializes the already-generated <see cref="Plan"/>
    /// against the already-imported <see cref="Layout"/> via <paramref name="exporter"/>. Requires a
    /// successful Discover (Step 009/010) and Generate (Step 012) to have already populated
    /// <see cref="Layout"/>/<see cref="Plan"/> — never guesses either. Returns <see langword="true"/>
    /// (and sets <see cref="LastExport"/>) only on full success; any failure (no layout/plan yet, an
    /// invalid output path, an unresolved target, or an unknown recipe) sets
    /// <see cref="ExportErrorMessage"/> and returns <see langword="false"/> without ever producing a
    /// partial file — the required failure path: "Invalid model/format/version must fail safely
    /// without rewriting source files."
    /// </summary>
    public async Task<bool> ExportXsqAsync(IXsqExporter exporter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(exporter);

        if (_isExportBusy)
        {
            return false;
        }

        IsExportBusy = true;
        ExportErrorMessage = null;
        try
        {
            if (Layout is null)
            {
                ExportErrorMessage = "Import a show layout (Discover) before exporting an XSQ.";
                return false;
            }

            if (Plan is null)
            {
                ExportErrorMessage = "Generate an effect plan before exporting an XSQ.";
                return false;
            }

            var result = await exporter.ExportAsync(Layout, Plan, XsqOutputPath, TargetXlightsVersion, ExportPolicy, ct).ConfigureAwait(true);

            LastExport = result;
            return true;
        }
        catch (Exception ex)
        {
            LastExport = null;
            ExportErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsExportBusy = false;
        }
    }

    /// <summary>
    /// PHASE-02 Step 014 "Open in xLights" action: launches <see cref="XlightsExecutablePath"/> against
    /// the already-exported <see cref="LastExport"/> via <paramref name="launcher"/>. Requires a
    /// successful Export (Step 013) to have already populated <see cref="LastExport"/> — never guesses
    /// a path. Returns <see langword="true"/> (and sets <see cref="LastLaunch"/>) only on full success;
    /// any failure (no export yet, a missing/incompatible executable, cancellation, or the OS refusing
    /// to start the process) sets <see cref="LaunchErrorMessage"/> and returns <see langword="false"/>
    /// without leaving the screen in a stuck/busy state — the required failure path: "Missing or
    /// incompatible model, cancellation and offline mode must yield a recoverable state."
    /// </summary>
    public async Task<bool> LaunchXlightsAsync(IXlightsLauncher launcher, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(launcher);

        if (_isLaunchBusy)
        {
            return false;
        }

        IsLaunchBusy = true;
        LaunchErrorMessage = null;
        try
        {
            if (LastExport is null)
            {
                LaunchErrorMessage = "Export an XSQ (Step 013) before opening it in xLights.";
                return false;
            }

            var result = await launcher.LaunchAsync(XlightsExecutablePath, LastExport.Path, ct).ConfigureAwait(true);

            LastLaunch = result;
            return true;
        }
        catch (Exception ex)
        {
            LastLaunch = null;
            LaunchErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsLaunchBusy = false;
        }
    }
}
