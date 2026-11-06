using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Stems;
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
    private IReadOnlyList<string> _recentShowDirectories = [];
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
    private WholeSongGenerationResult? _lastWholeSongGeneration;
    private string _postProcessingPromptText = string.Empty;
    private bool _isPostProcessingBusy;
    private string? _postProcessingErrorMessage;
    private string? _postProcessingStatusMessage;

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

    // Real whole-song AI pipeline (M1-M7): a separate action from the Step 012 "3 hardcoded props" demo
    // above, deliberately sharing this same screen's Plan/PlanSavedPath/IsPlanBusy/PlanErrorMessage
    // result state (both are just different ways of producing the one Plan this screen displays) rather
    // than duplicating a second parallel set of properties.
    private bool _requiresStemModelDownload;
    private bool _isDownloadingStemModel;
    private string? _stemModelStatusMessage;

    // Build-plan Phase 10 §3f ("Activate corpus mining"): the Generate & Export screen's Developer
    // Tools "Run corpus mining" action. Deliberately separate busy/error/result state from every
    // flow above, same reasoning as Export's own separation from Generate -- running corpus mining
    // never re-runs or invalidates Discover/Generate/Export, and a failed run never clobbers a
    // previously successful LastCorpusMiningResult.
    private string _corpusRootDirectory = string.Empty;
    private bool _isCorpusMiningBusy;
    private string? _corpusMiningStatusMessage;
    private string? _corpusMiningErrorMessage;
    private CorpusMiningResult? _lastCorpusMiningResult;

    /// <summary>30 seconds at the canonical 100 ns/tick unit — a reasonable demo default; a real song duration is Phase 04's own <c>SongAsset</c> concern.</summary>
    public const long DefaultSongDurationTicks = 300_000_000;

    /// <summary>Bindable directory text for the picker form.</summary>
    public string ShowDirectory
    {
        get => _showDirectory;
        set => SetField(ref _showDirectory, value);
    }

    /// <summary>
    /// User feedback (2026-11-01): every show directory ever successfully discovered, most-recent first
    /// -- backs this screen's dropdown so re-opening an already-known show is a pick, not a retyped path.
    /// Populated by <see cref="ShellViewModel"/> from <see cref="AppSettings.RecentShowDirectoriesOrEmpty"/>
    /// on navigate/startup; this view model owns no persistence of its own (same division of
    /// responsibility as every other restored/persisted value on this screen).
    /// </summary>
    public IReadOnlyList<string> RecentShowDirectories
    {
        get => _recentShowDirectories;
        private set
        {
            if (SetField(ref _recentShowDirectories, value))
            {
                OnPropertyChanged(nameof(HasRecentShowDirectories));
            }
        }
    }

    public bool HasRecentShowDirectories => RecentShowDirectories.Count > 0;

    /// <summary>Sets <see cref="RecentShowDirectories"/> -- a plain setter method (not a public property setter) so <see cref="ShellViewModel"/> cannot be mistaken for the owner of this list's persistence, the same convention <see cref="RestoreLastDirectory"/> already follows for <see cref="ShowDirectory"/>.</summary>
    public void SetRecentShowDirectories(IReadOnlyList<string> recentShowDirectories) =>
        RecentShowDirectories = recentShowDirectories ?? throw new ArgumentNullException(nameof(recentShowDirectories));

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

    /// <summary>
    /// The full result of the most recent successful <see cref="GenerateWholeSongPlanAsync"/> run —
    /// <see cref="Plan"/>/<see cref="PlanSavedPath"/> above are this result's own
    /// <see cref="WholeSongGenerationResult.Document"/>, kept as the screen's existing bindable
    /// properties so nothing that already binds to them has to change. This additionally carries the
    /// intermediate melody/phrase/motif analysis so <see cref="ShellViewModel"/> can open the Piano
    /// Roll/Structure screens against the same real run, instead of a second, duplicate analysis pass.
    /// <see langword="null"/> until a Generate attempt succeeds; never touched by the 3-hardcoded-props
    /// <see cref="GenerateEffectPlanAsync"/>, which produces no such analysis.
    /// </summary>
    public WholeSongGenerationResult? LastWholeSongGeneration
    {
        get => _lastWholeSongGeneration;
        private set
        {
            if (SetField(ref _lastWholeSongGeneration, value))
            {
                OnPropertyChanged(nameof(HasTemplatedLookUsage));
                OnPropertyChanged(nameof(TemplatedLookUsageSummaries));
                OnPropertyChanged(nameof(CanApplyPostProcessingPrompt));
            }
        }
    }

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): <see langword="true"/> once
    /// a generation has actually run and returned a real, reloadable <see cref="WholeSongGenerationResult.GenerationRunId"/>
    /// -- the Generate &amp; Export screen's own "touch-up" card is only shown then; there is nothing yet to
    /// touch up before a first generation succeeds.
    /// </summary>
    public bool CanApplyPostProcessingPrompt => LastWholeSongGeneration?.GenerationRunId is not null;

    /// <summary>The live, currently-typed touch-up prompt -- a "last stop before you apply" field, same convention as <see cref="CustomPromptEditorViewModel.PromptText"/>, deliberately not persisted across sessions (a touch-up is a one-shot action, not a standing setting).</summary>
    public string PostProcessingPromptText
    {
        get => _postProcessingPromptText;
        set => SetField(ref _postProcessingPromptText, value);
    }

    public bool IsPostProcessingBusy
    {
        get => _isPostProcessingBusy;
        private set => SetField(ref _isPostProcessingBusy, value);
    }

    public string? PostProcessingErrorMessage
    {
        get => _postProcessingErrorMessage;
        private set
        {
            if (SetField(ref _postProcessingErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasPostProcessingErrorMessage));
            }
        }
    }

    public bool HasPostProcessingErrorMessage => PostProcessingErrorMessage is not null;

    public string? PostProcessingStatusMessage
    {
        get => _postProcessingStatusMessage;
        private set
        {
            if (SetField(ref _postProcessingStatusMessage, value))
            {
                OnPropertyChanged(nameof(HasPostProcessingStatusMessage));
            }
        }
    }

    public bool HasPostProcessingStatusMessage => PostProcessingStatusMessage is not null;

    /// <summary>
    /// Build-plan Phase 10 §6b task 4: <see langword="true"/> once <see cref="LastWholeSongGeneration"/>
    /// has at least one Look whose own seed-template provenance (<see cref="GeneratedLookTemplateUsage.TemplateId"/>)
    /// the Director actually declared -- the Generate &amp; Export screen's own "Seed templates used"
    /// disclosure is only shown then, never for a run that drew on no named template.
    /// </summary>
    public bool HasTemplatedLookUsage => LastWholeSongGeneration?.LookTemplateUsageOrEmpty.Any(u => u.TemplateId is not null) ?? false;

    /// <summary>One human-readable line per Look that declared a seed template, e.g. <c>"Chorus Build — edm-dense-canvas-tree-matrix"</c> -- the exact "this Chorus Look is using the EDM dense-canvas Tree pattern" visibility build-plan Phase 10 §6b task 4 asks for.</summary>
    public IReadOnlyList<string> TemplatedLookUsageSummaries =>
        LastWholeSongGeneration?.LookTemplateUsageOrEmpty
            .Where(u => u.TemplateId is not null)
            .Select(u => $"{u.LookName} — {u.TemplateId}")
            .ToList()
        ?? [];

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
    /// <see langword="true"/> when the last <see cref="GenerateWholeSongPlanAsync"/> attempt failed
    /// because the stem-separation model needed for real melody analysis is not yet installed — the
    /// screen shows a "Download separation model" action instead of a dead-end error while this is set.
    /// Cleared at the start of every new whole-song plan attempt and on a successful model install.
    /// </summary>
    public bool RequiresStemModelDownload
    {
        get => _requiresStemModelDownload;
        private set => SetField(ref _requiresStemModelDownload, value);
    }

    public bool IsDownloadingStemModel
    {
        get => _isDownloadingStemModel;
        private set => SetField(ref _isDownloadingStemModel, value);
    }

    /// <summary>Human-readable outcome of the most recent <see cref="DownloadStemSeparationModelAsync"/> attempt.</summary>
    public string? StemModelStatusMessage
    {
        get => _stemModelStatusMessage;
        private set => SetField(ref _stemModelStatusMessage, value);
    }

    /// <summary>Bindable "corpus root directory" input for the Developer Tools "Run corpus mining" action (build-plan Phase 10 §3f) -- a raw <c>C:\xLights</c>-shaped folder, or a representative subset of one.</summary>
    public string CorpusRootDirectory
    {
        get => _corpusRootDirectory;
        set => SetField(ref _corpusRootDirectory, value);
    }

    public bool IsCorpusMiningBusy
    {
        get => _isCorpusMiningBusy;
        private set => SetField(ref _isCorpusMiningBusy, value);
    }

    /// <summary>The most recent one-line progress message from <see cref="CorpusMiningRunner.RunAsync(string,string,CancellationToken)"/>'s own <c>progress</c> callback -- a "tail the log" affordance for a run that can take up to roughly an hour against a full real corpus.</summary>
    public string? CorpusMiningStatusMessage
    {
        get => _corpusMiningStatusMessage;
        private set => SetField(ref _corpusMiningStatusMessage, value);
    }

    public string? CorpusMiningErrorMessage
    {
        get => _corpusMiningErrorMessage;
        private set => SetField(ref _corpusMiningErrorMessage, value);
    }

    /// <summary>Full result of the most recent successful <see cref="RunCorpusMiningAsync"/> call, including the real on-disk paths <see cref="CorpusMiningRunner.RunAsync(string,string,CancellationToken)"/> wrote <see cref="CraftRules"/>/<see cref="ExemplarIndex"/> to.</summary>
    public CorpusMiningResult? LastCorpusMiningResult
    {
        get => _lastCorpusMiningResult;
        private set => SetField(ref _lastCorpusMiningResult, value);
    }

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

    /// <summary>Same "only fill in a blank field, require an explicit action to actually run it" convention as <see cref="RestoreLastDirectory"/>, for <see cref="CorpusRootDirectory"/>.</summary>
    public void RestoreLastCorpusRootDirectory(string? lastCorpusRootDirectory)
    {
        if (!string.IsNullOrWhiteSpace(lastCorpusRootDirectory) && string.IsNullOrEmpty(CorpusRootDirectory))
        {
            CorpusRootDirectory = lastCorpusRootDirectory;
        }
    }

    /// <summary>
    /// User feedback (2026-11-03): "Can't that be loaded on start up" -- same "only fill in a blank
    /// field" convention as <see cref="RestoreLastDirectory"/>, for <see cref="XlightsExecutablePath"/>.
    /// </summary>
    public void RestoreXlightsExecutablePath(string? lastXlightsExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(lastXlightsExecutablePath) && string.IsNullOrEmpty(XlightsExecutablePath))
        {
            XlightsExecutablePath = lastXlightsExecutablePath;
        }
    }

    /// <summary>
    /// Build-plan Phase 10 §3f "Run corpus mining" action: the real, reachable invocation path for
    /// <see cref="CorpusMiningRunner.RunAsync(string,string,CancellationToken)"/> -- runs the full
    /// manifest-build/per-song-analysis/PatternLibrary-ExemplarIndex-CraftRules pipeline against
    /// <see cref="CorpusRootDirectory"/> and persists the result under <paramref name="knowledgeDirectory"/>
    /// (the runner itself writes <c>craft-rules.json</c>/<c>exemplar-index.json</c>/<c>pattern-library.json</c>
    /// via <see cref="JsonCraftRulesStore"/>/<see cref="JsonExemplarIndexStore"/>/<see cref="JsonPatternLibraryStore"/> --
    /// there is no separate persistence step here). A run can take up to roughly an hour against a full
    /// real corpus (the runner's own doc comment); <see cref="CorpusMiningStatusMessage"/> surfaces its
    /// one-line-per-song progress so a real run can be tailed rather than guessed at. Returns
    /// <see langword="false"/> (and sets <see cref="CorpusMiningErrorMessage"/>) on a blank directory, a
    /// reentrant call while already busy, or any exception from the runner -- never an unhandled
    /// exception out of a UI command.
    /// </summary>
    /// <param name="aiRoleClientResolver">
    /// Build-plan Phase 16 §2b: when supplied and the Director role is configured, real exemplar
    /// descriptions are generated for this run via <see cref="ChatCompletionExemplarDescriptionClient"/>.
    /// Defaults to <see langword="null"/> (no description generation -- the pre-existing behavior) so
    /// every existing call site keeps compiling; an unconfigured Director role (<see cref="AiProviderNotConfiguredException"/>)
    /// is treated the same way, logged to <see cref="CorpusMiningStatusMessage"/> and otherwise non-fatal
    /// -- a missing/unconfigured AI role must never block the rest of corpus mining from succeeding.
    /// </param>
    /// <param name="excludedProjectRootDirectories">Build-plan Phase 17 §2a: forwarded as-is to <see cref="CorpusMiningRunner.RunAsync(string,string,TimeSpan,Action{string}?,CancellationToken,IExemplarDescriptionClient?,int,IReadOnlyList{string}?,int)"/>'s own parameter of the same name. Defaults to <see langword="null"/> so every pre-existing caller keeps compiling.</param>
    public async Task<bool> RunCorpusMiningAsync(
        string knowledgeDirectory, AiRoleClientResolver? aiRoleClientResolver = null,
        IReadOnlyList<string>? excludedProjectRootDirectories = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeDirectory);

        if (_isCorpusMiningBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CorpusRootDirectory))
        {
            CorpusMiningErrorMessage = "Enter a corpus root directory before running corpus mining.";
            return false;
        }

        IsCorpusMiningBusy = true;
        CorpusMiningErrorMessage = null;
        CorpusMiningStatusMessage = "Starting corpus mining...";
        try
        {
            IExemplarDescriptionClient? descriptionClient = null;
            if (aiRoleClientResolver is not null)
            {
                try
                {
                    var (directorClient, directorModel) = await aiRoleClientResolver.ResolveRequiredAsync(AiRole.Director, ct).ConfigureAwait(true);
                    descriptionClient = new ChatCompletionExemplarDescriptionClient(directorClient, directorModel, new JsonAiCallLogStore(Path.Combine(knowledgeDirectory, "llm")));
                }
                catch (AiProviderNotConfiguredException)
                {
                    CorpusMiningStatusMessage = "Director role not configured -- exemplar descriptions will be skipped for this run.";
                }
            }

            var result = await CorpusMiningRunner
                .RunAsync(
                    CorpusRootDirectory, knowledgeDirectory, CorpusMiningRunner.DefaultPerSongTimeout,
                    message => CorpusMiningStatusMessage = message, ct, descriptionClient,
                    excludedProjectRootDirectories: excludedProjectRootDirectories)
                .ConfigureAwait(true);
            LastCorpusMiningResult = result;
            CorpusMiningStatusMessage =
                $"Done: {result.Analyses.Count} song(s) analyzed, {result.FailedSongs.Count} failed/timed out. " +
                $"Wrote '{result.CraftRulesPath}' and '{result.ExemplarIndexPath}'.";
            return true;
        }
        catch (Exception ex)
        {
            CorpusMiningErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsCorpusMiningBusy = false;
        }
    }

    /// <summary>
    /// Defaults <see cref="XsqOutputPath"/> to the current project's own <c>exports/</c>
    /// subdirectory (created for every project by <c>ProjectService.CreateAsync</c>) once a project
    /// becomes current — same "only fill in a blank field, never overwrite something the user
    /// already typed" convention as <see cref="RestoreLastDirectory"/>. Without this, export had no
    /// relationship to the open project at all: the output path was an independent, never-defaulted
    /// text box regardless of which (if any) project was open.
    /// </summary>
    public void SetDefaultXsqOutputDirectory(string projectRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootDirectory);

        if (string.IsNullOrEmpty(XsqOutputPath))
        {
            XsqOutputPath = Path.Combine(projectRootDirectory, "exports", "sequence.xsq");
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
    /// Real whole-song AI pipeline "Generate Whole-Song Plan" action: runs the full real
    /// Stems/Rhythm/Melody/Structure/WholeSong/Composition chain (via <paramref name="generator"/>)
    /// against <paramref name="probedSong"/> and the already-imported <see cref="Layout"/>, using every
    /// real prop in the layout — never a manually-typed name list. Shares this screen's own
    /// <see cref="Plan"/>/<see cref="PlanSavedPath"/>/<see cref="IsPlanBusy"/>/<see cref="PlanErrorMessage"/>
    /// result state with <see cref="GenerateEffectPlanAsync"/> (both just produce the one <see cref="Plan"/>
    /// this screen displays, by different means) rather than a second parallel set of properties. A
    /// not-yet-installed stem-separation model surfaces as <see cref="RequiresStemModelDownload"/> instead
    /// of a dead-end error, mirroring the Audio screen's own ffmpeg-download affordance.
    /// </summary>
    /// <param name="customPrompt">Build-plan Phase 14 §2c: the user's own free-text creative direction for this run, or <see langword="null"/>/empty for the pre-existing default behavior.</param>
    public async Task<bool> GenerateWholeSongPlanAsync(
        IWholeSongGenerator generator, SongAsset? probedSong, ISequenceDocumentStore planStore, CancellationToken ct = default,
        CustomGenerationPrompt? customPrompt = null, ILogger<ShowImportViewModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(planStore);
        logger ??= NullLogger<ShowImportViewModel>.Instance;

        if (_isPlanBusy)
        {
            return false;
        }

        IsPlanBusy = true;
        PlanErrorMessage = null;
        RequiresStemModelDownload = false;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("User requested whole-song plan generation (song probed: {HasProbedSong}, layout imported: {HasLayout}).",
            probedSong is not null, Layout is not null);
        try
        {
            if (Layout is null)
            {
                PlanErrorMessage = "Import a show layout (Discover) before generating a whole-song plan.";
                logger.LogWarning("Whole-song plan generation blocked: no show layout imported.");
                return false;
            }

            if (probedSong is null)
            {
                PlanErrorMessage = "Probe an audio file (Audio screen) before generating a whole-song plan.";
                logger.LogWarning("Whole-song plan generation blocked: no audio probed.");
                return false;
            }

            var generation = await generator.GenerateAsync(probedSong, Layout, ct, customPrompt, ShowDirectory, XlightsExecutablePath).ConfigureAwait(true);
            var savedPath = await planStore.SaveAsync(generation.Document, ct).ConfigureAwait(true);

            Plan = generation.Document;
            PlanSavedPath = savedPath;
            LastWholeSongGeneration = generation;
            logger.LogInformation("Whole-song plan generation succeeded in {ElapsedMs}ms, saved to {SavedPath}.", stopwatch.ElapsedMilliseconds, savedPath);
            return true;
        }
        catch (ModelAssetConsentRequiredException ex)
        {
            Plan = null;
            PlanSavedPath = null;
            LastWholeSongGeneration = null;
            RequiresStemModelDownload = true;
            PlanErrorMessage =
                "Generating a whole-song plan needs a one-time vocal/instrument separation model download.";
            logger.LogWarning(ex, "Whole-song plan generation needs a stem-separation model download.");
            return false;
        }
        catch (AiProviderNotConfiguredException ex)
        {
            Plan = null;
            PlanSavedPath = null;
            LastWholeSongGeneration = null;
            PlanErrorMessage =
                $"Configure an AI provider for the {ex.Role} role in Settings before generating a whole-song plan.";
            logger.LogWarning(ex, "Whole-song plan generation failed: {Role} role has no configured AI provider.", ex.Role);
            return false;
        }
        catch (QualityPassUnavailableException ex)
        {
            Plan = null;
            PlanSavedPath = null;
            LastWholeSongGeneration = null;
            PlanErrorMessage = ex.Message;
            logger.LogWarning(ex, "Whole-song plan generation's quality pass is unavailable.");
            return false;
        }
        catch (StemSeparationIncompatibleSourceException ex)
        {
            // The real separation model accepts only 44.1 kHz stereo. A non-WAV import is already
            // normalized to exactly that by FfmpegProcessTranscoder, so this can only be reached by a
            // native WAV at some other rate/channel count (which is probed directly, never transcoded).
            // Restate the real, specific measured mismatch plus what the user can actually do about it,
            // rather than surfacing the lower-level message alone.
            Plan = null;
            PlanSavedPath = null;
            LastWholeSongGeneration = null;
            PlanErrorMessage =
                $"{ex.Message} Convert this song to 44.1 kHz stereo (or import it as MP3/M4A, which is " +
                "converted automatically on probe), then probe it again on the Audio screen.";
            logger.LogWarning(ex, "Whole-song plan generation failed: incompatible source audio format.");
            return false;
        }
        catch (Exception ex)
        {
            Plan = null;
            PlanSavedPath = null;
            LastWholeSongGeneration = null;
            PlanErrorMessage = ex.Message;
            logger.LogError(ex, "Whole-song plan generation failed after {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
            return false;
        }
        finally
        {
            IsPlanBusy = false;
        }
    }

    /// <summary>
    /// Build-plan "richer analysis tools" post-processing phase (2026-11-02): the user's own explicit ask
    /// -- "a post-processing prompt... where I can put time ranges and have it alter the existing
    /// sequence, so it would only fix what I ask and leave the rest alone." Deliberately does NOT clear
    /// <see cref="Plan"/>/<see cref="LastWholeSongGeneration"/> on failure (unlike <see cref="GenerateWholeSongPlanAsync"/>'s
    /// own all-or-nothing framing): a touch-up attempt that fails must never discard the user's existing,
    /// already-successful sequence -- only <see cref="PostProcessingErrorMessage"/> is set, and the prior
    /// generation stays exactly as it was.
    /// </summary>
    public async Task<bool> ApplyPostProcessingPromptAsync(
        IWholeSongGenerator generator, SongAsset? probedSong, ISequenceDocumentStore planStore, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(planStore);

        if (_isPostProcessingBusy)
        {
            return false;
        }

        IsPostProcessingBusy = true;
        PostProcessingErrorMessage = null;
        PostProcessingStatusMessage = null;
        try
        {
            if (LastWholeSongGeneration?.GenerationRunId is not { } generationRunId)
            {
                PostProcessingErrorMessage = "Generate a whole-song plan first -- there is nothing yet to touch up.";
                return false;
            }

            if (Layout is null)
            {
                PostProcessingErrorMessage = "Import a show layout (Discover) before applying a touch-up prompt.";
                return false;
            }

            if (probedSong is null)
            {
                PostProcessingErrorMessage = "Probe an audio file (Audio screen) before applying a touch-up prompt.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PostProcessingPromptText))
            {
                PostProcessingErrorMessage = "Type a touch-up instruction with at least one time range (e.g. \"00:24-00:25\") first.";
                return false;
            }

            var generation = await generator.ApplyPostProcessingPromptAsync(probedSong, Layout, generationRunId, PostProcessingPromptText, ct).ConfigureAwait(true);
            var savedPath = await planStore.SaveAsync(generation.Document, ct).ConfigureAwait(true);

            Plan = generation.Document;
            PlanSavedPath = savedPath;
            LastWholeSongGeneration = generation;
            PostProcessingStatusMessage = "Touch-up applied.";
            return true;
        }
        catch (PostProcessingPromptHasNoTimedInstructionsException ex)
        {
            PostProcessingErrorMessage = ex.Message;
            return false;
        }
        catch (PostProcessingPromptMatchesNoSectionException ex)
        {
            PostProcessingErrorMessage = ex.Message;
            return false;
        }
        catch (AiProviderNotConfiguredException ex)
        {
            PostProcessingErrorMessage = $"Configure an AI provider for the {ex.Role} role in Settings before applying a touch-up prompt.";
            return false;
        }
        catch (Exception ex)
        {
            PostProcessingErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsPostProcessingBusy = false;
        }
    }

    /// <summary>
    /// Downloads/verifies the real stem-separation model <see cref="GenerateWholeSongPlanAsync"/> needs
    /// for real melody analysis. The user clicking the "Download separation model" action this is wired
    /// to is the explicit consent to download — no separate persisted consent flag, same convention as
    /// the Audio screen's <c>DownloadFfmpegAsync</c>.
    /// </summary>
    public async Task<bool> DownloadStemSeparationModelAsync(IModelAssetManager modelAssetManager, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modelAssetManager);

        if (_isDownloadingStemModel)
        {
            return false;
        }

        IsDownloadingStemModel = true;
        try
        {
            await modelAssetManager.EnsureVerifiedAsync(SherpaOnnxSpleeterModelAsset.Archive, userConsent: true, ct).ConfigureAwait(true);
            StemModelStatusMessage = "Installed the vocal/instrument separation model. Click Generate Whole-Song Plan again.";
            RequiresStemModelDownload = false;
            return true;
        }
        catch (OperationCanceledException)
        {
            StemModelStatusMessage = "Downloading the separation model was cancelled; it was not installed. You can try again later.";
            return false;
        }
        catch (Exception ex)
        {
            StemModelStatusMessage = ex.Message;
            return false;
        }
        finally
        {
            IsDownloadingStemModel = false;
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
