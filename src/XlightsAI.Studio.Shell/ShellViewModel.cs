using Microsoft.Extensions.Logging;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Projects;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Xlights;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Root view model for the Avalonia shell. Composes the startup/navigation state machine with
/// the screens shipped so far (Welcome, Projects, Settings, Diagnostics). The Avalonia MainWindow
/// binds to this type only; it contains no Avalonia types itself, so it is fully unit-testable.
///
/// <paramref name="settingsStore"/> and <paramref name="diagnostics"/> are new in Step 005 and
/// default to no-op implementations (<see cref="NullSettingsStore"/>, <see cref="NullAppDiagnostics"/>)
/// specifically so Step 004's existing two-argument call sites and tests keep compiling and
/// behaving identically (always starts on Welcome, nothing persisted) without modification — an
/// explicit compatibility decision rather than a breaking contract change. Real composition (the
/// App project) always supplies the real implementations.
///
/// <paramref name="projectService"/> is new in Step 006 and follows the exact same compatibility
/// pattern: it defaults to <see cref="NullProjectService"/>, so Step 004/005's existing call sites
/// and tests keep compiling and behaving identically (Create/Open simply report "no project
/// service is configured" rather than succeeding) without modification.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly IShellNavigator _navigator;
    private readonly Func<IReadOnlyList<string>> _projectNamesSource;
    private readonly ISettingsStore _settingsStore;
    private readonly IAppDiagnostics _diagnostics;
    private readonly IProjectService _projectService;
    private readonly IShowDirectoryDiscoveryService _showDirectoryDiscoveryService;
    private readonly IShowLayoutImporter _showLayoutImporter;
    private readonly IThreeEffectPlanFactory _threeEffectPlanFactory;
    private readonly IEffectRecipeRegistry _effectRecipeRegistry;
    private readonly ISequenceDocumentStore _sequenceDocumentStore;
    private readonly IXsqExporter _xsqExporter;
    private readonly IXlightsLauncher _xlightsLauncher;
    private readonly IAudioDecoder _audioDecoder;
    private readonly ISongAssetStore _songAssetStore;
    private readonly IAudioTransport _audioTransport;
    private readonly IWaveformPyramidStore _waveformPyramidStore;
    private readonly IWaveformBuilder _waveformBuilder;
    private readonly MasterPlaybackClock _masterClock;
    private readonly IRuntimeCapabilityDetector _runtimeCapabilityDetector;
    private readonly IStartupHealthChecks _startupHealthChecks;
    private readonly IAssetInstaller _assetInstaller;
    private readonly DownloadableAsset? _optionalModelAsset;
    private readonly IFfmpegInstaller _ffmpegInstaller;
    private readonly IWholeSongGenerator _wholeSongGenerator;
    private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;
    private readonly IModelAssetManager _stemModelAssetManager;
    private readonly INoteTrackEditor _noteTrackEditor;
    private readonly IMotifLinkOverrideStore _motifLinkOverrideStore;
    private readonly IDisplayLayoutEditor _displayLayoutEditor;
    private readonly IDisplayGraphBuilder _displayGraphBuilder;
    private readonly string? _corpusKnowledgeDirectory;

    /// <summary>
    /// Build-plan Phase 16 §2b: resolves the Director role's configured client for real exemplar
    /// descriptions during <see cref="RunCorpusMiningAsync"/>. Nullable/defaults to <see langword="null"/>
    /// (no description generation, the pre-existing behavior) for the same "only the real composition
    /// root knows this" reasoning as <see cref="_corpusKnowledgeDirectory"/> above.
    /// </summary>
    private readonly AiRoleClientResolver? _aiRoleClientResolver;
    private bool _isRestoringLastScreen;
    private ProjectManifest? _currentProject;

    /// <summary>PHASE-03 Step 016: how many pixels <see cref="TimelineScrollLeftCommand"/>/<see cref="TimelineScrollRightCommand"/> scroll per click.</summary>
    public const double TimelineScrollStepPixels = 80d;

    public ShellViewModel(
        IShellNavigator navigator,
        Func<IReadOnlyList<string>> projectNamesSource,
        ISettingsStore? settingsStore = null,
        IAppDiagnostics? diagnostics = null,
        IProjectService? projectService = null,
        IShowDirectoryDiscoveryService? showDirectoryDiscoveryService = null,
        IShowLayoutImporter? showLayoutImporter = null,
        IThreeEffectPlanFactory? threeEffectPlanFactory = null,
        IEffectRecipeRegistry? effectRecipeRegistry = null,
        ISequenceDocumentStore? sequenceDocumentStore = null,
        IXsqExporter? xsqExporter = null,
        IXlightsLauncher? xlightsLauncher = null,
        IAudioDecoder? audioDecoder = null,
        ISongAssetStore? songAssetStore = null,
        IAudioTransport? audioTransport = null,
        IWaveformPyramidStore? waveformPyramidStore = null,
        IWaveformBuilder? waveformBuilder = null,
        IRuntimeCapabilityDetector? runtimeCapabilityDetector = null,
        IStartupHealthChecks? startupHealthChecks = null,
        IAssetInstaller? assetInstaller = null,
        DownloadableAsset? optionalModelAsset = null,
        IFfmpegInstaller? ffmpegInstaller = null,
        IWholeSongGenerator? wholeSongGenerator = null,
        IModelAssetManager? stemModelAssetManager = null,
        INoteTrackEditor? noteTrackEditor = null,
        IMotifLinkOverrideStore? motifLinkOverrideStore = null,
        IDisplayLayoutEditor? displayLayoutEditor = null,
        IDisplayGraphBuilder? displayGraphBuilder = null,
        string? corpusKnowledgeDirectory = null,
        ICustomPromptStore? customPromptStore = null,
        AiRoleClientResolver? aiRoleClientResolver = null,
        Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _projectNamesSource = projectNamesSource ?? throw new ArgumentNullException(nameof(projectNamesSource));
        _settingsStore = settingsStore ?? NullSettingsStore.Instance;
        _diagnostics = diagnostics ?? NullAppDiagnostics.Instance;
        _projectService = projectService ?? NullProjectService.Instance;
        _showDirectoryDiscoveryService = showDirectoryDiscoveryService ?? NullShowDirectoryDiscoveryService.Instance;

        // Deliberately NOT the NullShowLayoutImporter compatibility pattern used by the other
        // optional collaborators above: ShowLayoutImporter is a pure, stateless, read-only XML
        // parser with no external configuration (no path/service to wire up), so there is nothing
        // "unconfigured" about omitting it — unlike IProjectService/IShowDirectoryDiscoveryService,
        // which need a real caller-supplied target before they can do anything meaningful. Defaulting
        // to a real instance here also means Step 009's own already-accepted Discover flow keeps
        // parsing (now also successfully round-tripping Step 010's own Layout) without every one of
        // Step 009's existing tests needing to start passing an explicit importer. See
        // implementation-evidence/PHASE-02/STEP-10.md Section 9 for the full architecture note.
        _showLayoutImporter = showLayoutImporter ?? new ShowLayoutImporter();

        // PHASE-02 Step 012: same "pure, stateless domain logic, nothing to configure" reasoning as
        // _showLayoutImporter above — both default to a real instance rather than the throwing
        // NullX pattern used by collaborators that need external configuration (below).
        _threeEffectPlanFactory = threeEffectPlanFactory ?? new ThreeEffectPlanFactory();
        _effectRecipeRegistry = effectRecipeRegistry ?? new EffectRecipeRegistry();

        // Unlike the two collaborators above, a sequence-document store needs a real output
        // directory before it can do anything meaningful (same reasoning as
        // _showDirectoryDiscoveryService/_projectService): defaults to the throwing
        // NullSequenceDocumentStore, so Step 009-011's existing call sites keep compiling and
        // behaving identically, and a caller that omits it gets an explicit, disclosed failure
        // rather than a silently-succeeding fake save.
        _sequenceDocumentStore = sequenceDocumentStore ?? NullSequenceDocumentStore.Instance;

        // PHASE-02 Step 013: same "pure, stateless domain logic, nothing to configure" reasoning as
        // _showLayoutImporter/_threeEffectPlanFactory/_effectRecipeRegistry above — XsqExporter takes
        // its output path as a per-call argument rather than needing a configured directory, so it
        // defaults to a real instance rather than a throwing NullX stub.
        _xsqExporter = xsqExporter ?? new XsqExporter();

        // PHASE-02 Step 014: same "pure, stateless domain logic, nothing to configure" reasoning as
        // _xsqExporter above — XlightsLauncher takes both the executable path and the xsq path as
        // per-call arguments rather than needing a configured directory, so it defaults to a real
        // instance rather than a throwing NullX stub.
        _xlightsLauncher = xlightsLauncher ?? new XlightsLauncher();

        // PHASE-04 Step 024: same "pure, stateless domain logic, nothing to configure" reasoning as
        // _xsqExporter/_xlightsLauncher above — WavAudioDecoder probes/decodes a per-call file path
        // rather than needing a configured directory, so it defaults to a real instance.
        _audioDecoder = audioDecoder ?? new WavAudioDecoder();

        // Unlike _audioDecoder above, a song-asset store needs a real output directory before it
        // can do anything meaningful (same reasoning as _sequenceDocumentStore): defaults to the
        // throwing NullSongAssetStore, so a caller that omits it gets an explicit, disclosed
        // failure rather than a silently-succeeding fake save.
        _songAssetStore = songAssetStore ?? NullSongAssetStore.Instance;

        // PHASE-04 Step 025: same "pure, stateless, nothing external to configure" reasoning as
        // _audioDecoder — a real WaveOutAudioTransport opens no OS resource at construction time
        // (only OpenAsync does), so it is safe to default to a real instance rather than a throwing
        // NullX stub. On any non-Windows platform (no real backend exists yet — see
        // docs/ARCHITECTURE-AND-GUARDRAILS.md's "Platform coverage" rule) this defaults instead to
        // UnsupportedPlatformAudioTransport, which throws a clear, typed message naming the gap on
        // first use rather than letting a raw native-interop failure surface.
        _audioTransport = audioTransport ?? CreateDefaultAudioTransport(_audioDecoder);

        // PHASE-04 Step 026: same "needs a real output directory before it can do anything
        // meaningful" reasoning as _songAssetStore above: defaults to the throwing
        // NullWaveformPyramidStore so a caller that omits it gets an explicit, disclosed failure
        // rather than a silently-succeeding fake build.
        _waveformPyramidStore = waveformPyramidStore ?? NullWaveformPyramidStore.Instance;

        // Unlike _waveformPyramidStore, WaveformPyramidBuilder itself is pure, stateless-at-
        // construction domain logic (same reasoning as _audioDecoder) once handed its two
        // collaborators, so it defaults to a real instance built from whichever decoder/store this
        // constructor ended up with above — including the throwing Null store, if the caller never
        // configured one; the failure then surfaces from the first real BuildPyramidAsync call,
        // not from construction.
        _waveformBuilder = waveformBuilder ?? new WaveformPyramidBuilder(_audioDecoder, _waveformPyramidStore);

        // PHASE-04 Step 028: the one authoritative playhead/master clock, wrapping whichever real
        // (or, off-Windows, stub) transport this instance resolved to above — there is exactly one
        // MasterPlaybackClock per ShellViewModel, so the Audio screen's own position display and the
        // Timeline screen's playhead/scrub preview always read the same real, sample-rate-accurate
        // conversion (never two independently-computed estimates that could disagree or drift).
        _masterClock = new MasterPlaybackClock(_audioTransport);

        // PHASE-15 Step 095: same "pure, stateless domain logic, nothing to configure" reasoning as
        // _showLayoutImporter/_xsqExporter/_xlightsLauncher above — RuntimeCapabilityDetector opens
        // no external resource at construction, so it defaults to a real instance. Unlike that
        // detector, a real IStartupHealthChecks needs a concrete bundle directory and ReleaseManifest
        // this layer does not yet own (PHASE-15 Step 094 Section 6 deliberately deferred that
        // wiring), so it defaults to the non-throwing NullStartupHealthChecks — first-run reports
        // zero asset-verification issues until a real release composition root supplies one, rather
        // than crashing for a collaborator this step has no real bundle to check.
        _runtimeCapabilityDetector = runtimeCapabilityDetector ?? new RuntimeCapabilityDetector();
        _startupHealthChecks = startupHealthChecks ?? NullStartupHealthChecks.Instance;

        // PHASE-15 Step 096: same "needs a real output/install directory this layer does not yet own"
        // reasoning as _sequenceDocumentStore/_songAssetStore above — a real AssetInstaller needs a
        // concrete install directory only a real published release's own composition root can supply,
        // which does not exist yet at the Shell/first-run layer (same disclosed deferral as
        // _startupHealthChecks). Defaults to the throwing NullAssetInstaller so a caller that omits it
        // gets an explicit, disclosed failure rather than a silently-succeeding fake install;
        // _optionalModelAsset defaults to null ("no optional asset declared for this build yet"),
        // surfaced as its own explained message by FirstRunViewModel.InstallOptionalAssetAsync rather
        // than fabricated.
        _assetInstaller = assetInstaller ?? NullAssetInstaller.Instance;
        _optionalModelAsset = optionalModelAsset;

        // Same "needs a real install/extraction directory this layer does not yet own" reasoning as
        // _assetInstaller above. Deliberately independent of _audioDecoder: whichever decoder this
        // instance ends up with (a plain WavAudioDecoder here, or a real TranscodingAudioDecoder wired
        // in by the App composition root) is unaffected by this collaborator, which only backs the
        // Audio screen's own explicit "Download ffmpeg" action.
        _ffmpegInstaller = ffmpegInstaller ?? NullFfmpegInstaller.Instance;

        // Same "needs a real analysis-cache directory tree/stem-separation model this layer does not yet
        // own" reasoning as _assetInstaller/_ffmpegInstaller above.
        _wholeSongGenerator = wholeSongGenerator ?? NullWholeSongGenerator.Instance;
        _stemModelAssetManager = stemModelAssetManager ?? NullModelAssetManager.Instance;

        // Same "needs a real directory-backed store this layer does not yet own" reasoning as
        // _sequenceDocumentStore/_songAssetStore above — each backs one of the Piano Roll/Structure/
        // Display Paths screens (see docs/UI-CLEANUP-PLAN.md item 2, Phase 2a) and needs a real
        // on-disk directory only a real composition root can supply.
        _noteTrackEditor = noteTrackEditor ?? NullNoteTrackEditor.Instance;
        _motifLinkOverrideStore = motifLinkOverrideStore ?? NullMotifLinkOverrideStore.Instance;
        _displayLayoutEditor = displayLayoutEditor ?? NullDisplayLayoutEditor.Instance;

        // Pure, stateless domain logic (same reasoning as _showLayoutImporter/_xsqExporter above):
        // DisplayGraphBuilder takes the imported layout as a per-call argument and needs nothing
        // configured, so it defaults to a real instance rather than a throwing NullX stub.
        _displayGraphBuilder = displayGraphBuilder ?? new DisplayGraphBuilder();

        // Build-plan Phase 10 §3f ("Activate corpus mining"): the directory RunCorpusMiningCommand
        // persists CraftRules/ExemplarIndex/PatternLibrary under (same base as App.axaml.cs's other
        // %AppData% stores). Deliberately nullable, not defaulted to a real path here -- unlike every
        // other JSON-backed store above, only the real composition root knows the real %AppData% base;
        // null just means "corpus mining unavailable," surfaced as an explicit error rather than a
        // silent wrong-directory write, same as every other "needs a real configured directory this
        // layer does not yet own" collaborator in this constructor.
        _corpusKnowledgeDirectory = corpusKnowledgeDirectory;
        _aiRoleClientResolver = aiRoleClientResolver;

        Welcome = new WelcomeViewModel();
        Projects = new ProjectsViewModel();
        Settings = new SettingsViewModel();
        Diagnostics = new DiagnosticsViewModel();
        ShowImport = new ShowImportViewModel();
        // Build-plan Phase 14 §2b.2: same "needs a real settings directory this layer does not yet
        // own" reasoning as _wholeSongGenerator/_noteTrackEditor above -- defaults to the throwing-
        // on-save NullCustomPromptStore so a caller that omits it still shows an empty, usable screen
        // (Load always succeeds with the empty default) rather than crashing at construction.
        CustomPrompt = new CustomPromptEditorViewModel(customPromptStore ?? NullCustomPromptStore.Instance);
        Timeline = new TimelineViewModel();
        Audio = new AudioImportViewModel();
        PianoRoll = new PianoRollViewModel();
        StructureInspector = new PhraseRelationshipInspectorViewModel();
        DisplayPathEditor = new DisplayPathEditorViewModel();
        FirstRun = new FirstRunViewModel();
        ExemplarLibrary = new ExemplarLibraryViewModel();

        NavigateToProjectsCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.Projects),
            () => _navigator.State == StartupState.Ready);
        NavigateToWelcomeCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.Welcome),
            () => _navigator.State == StartupState.Ready);
        NavigateToSettingsCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.Settings),
            () => _navigator.State == StartupState.Ready);
        NavigateToDiagnosticsCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.Diagnostics),
            () => _navigator.State == StartupState.Ready);
        NavigateToExemplarLibraryCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.ExemplarLibrary),
            () => _navigator.State == StartupState.Ready);
        NavigateToShowImportCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.ShowImport),
            () => _navigator.State == StartupState.Ready);
        NavigateToCustomPromptCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.CustomPrompt),
            () => _navigator.State == StartupState.Ready);
        NavigateToGenerateExportCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.GenerateExport),
            () => _navigator.State == StartupState.Ready);
        NavigateToTimelineCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.Timeline),
            () => _navigator.State == StartupState.Ready);
        NavigateToAudioCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.Audio),
            () => _navigator.State == StartupState.Ready);
        NavigateToPianoRollCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.PianoRoll),
            () => _navigator.State == StartupState.Ready);
        NavigateToStructureInspectorCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.StructureInspector),
            () => _navigator.State == StartupState.Ready);
        NavigateToDisplayPathEditorCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.DisplayPathEditor),
            () => _navigator.State == StartupState.Ready);
        OpenDisplayPathEditorCommand = new DelegateCommand(
            () => LastOpenDisplayPathEditorCommandTask = RunOpenDisplayPathEditorAsync(),
            () => _navigator.State == StartupState.Ready && ShowImport.IsLoaded);
        CorrectDisplayNodePositionCommand = new DelegateCommand(
            () => LastCorrectDisplayNodePositionCommandTask = RunCorrectDisplayNodePositionAsync(),
            () => _navigator.State == StartupState.Ready && DisplayPathEditor.IsLoaded && DisplayPathEditor.SelectedNodeId.HasValue);
        CorrectSelectedNoteCommand = new DelegateCommand(
            () => LastCorrectSelectedNoteCommandTask = RunCorrectSelectedNoteAsync(),
            () => _navigator.State == StartupState.Ready && PianoRoll.IsLoaded && PianoRoll.SelectedNoteId.HasValue);
        ToggleLockSelectedNoteCommand = new DelegateCommand(
            () => LastToggleLockSelectedNoteCommandTask = PianoRoll.ToggleLockSelectedNoteAsync(_noteTrackEditor),
            () => _navigator.State == StartupState.Ready && PianoRoll.IsLoaded && PianoRoll.SelectedNoteId.HasValue);
        UnlinkSelectedPhraseCommand = new DelegateCommand(
            () => LastUnlinkSelectedPhraseCommandTask = RunUnlinkSelectedPhraseAsync(),
            () => _navigator.State == StartupState.Ready && StructureInspector.IsLoaded && StructureInspector.SelectedPhraseId.HasValue);
        LinkSelectedPhraseCommand = new DelegateCommand(
            () => LastLinkSelectedPhraseCommandTask = RunLinkSelectedPhraseAsync(),
            () => _navigator.State == StartupState.Ready && StructureInspector.IsLoaded
                && StructureInspector.SelectedPhraseId.HasValue && StructureInspector.PendingLinkTargetPhraseId.HasValue);
        NavigateToFirstRunCommand = new DelegateCommand(
            () => _navigator.NavigateTo(ShellScreen.FirstRun),
            () => _navigator.State == StartupState.Ready);
        CompleteFirstRunCommand = new DelegateCommand(
            () => LastCompleteFirstRunCommandTask = RunCompleteFirstRunAsync(),
            () => _navigator.State == StartupState.Ready && !FirstRun.IsBusy);
        InstallOptionalAssetCommand = new DelegateCommand(
            () => LastInstallOptionalAssetCommandTask = RunInstallOptionalAssetAsync(),
            () => _navigator.State == StartupState.Ready && !FirstRun.IsInstallingOptionalAsset);
        LoadTimelineCommand = new DelegateCommand(
            // PHASE-03 Step 017: pass through the already-imported layout (if any) so Load Timeline
            // also binds the prop rows, not just the time ruler; null when no Discover has
            // succeeded yet, which LoadTimeline itself treats as "no rows" rather than an error.
            // PHASE-03 Step 018: also pass through the already-generated plan (if any), so Load
            // Timeline binds real effect blocks too; null when no plan has been generated yet,
            // which LoadTimeline itself treats as "no effect blocks" rather than an error.
            // PHASE-04 Step 027: also pass through the most recently built/reopened waveform
            // pyramid (if any) and its source song's OriginTicks, so Load Timeline also renders the
            // synchronized waveform beneath the ruler; null/0 when no waveform cache has been built
            // yet, which LoadTimeline itself treats as "no waveform strip" rather than an error.
            () => Timeline.LoadTimeline(
                ShowImport.Layout, ShowImport.Plan, Audio.LoadedWaveformCache, Audio.ProbedAsset?.OriginTicks ?? 0),
            () => _navigator.State == StartupState.Ready);
        TimelineZoomInCommand = new DelegateCommand(
            () => Timeline.ZoomIn(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineZoomOutCommand = new DelegateCommand(
            () => Timeline.ZoomOut(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineScrollLeftCommand = new DelegateCommand(
            () => Timeline.ScrollByPixels(-TimelineScrollStepPixels),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineScrollRightCommand = new DelegateCommand(
            () => Timeline.ScrollByPixels(TimelineScrollStepPixels),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineSelectNextEffectCommand = new DelegateCommand(
            () => Timeline.SelectNextEffect(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineSelectPreviousEffectCommand = new DelegateCommand(
            () => Timeline.SelectPreviousEffect(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineClearSelectionCommand = new DelegateCommand(
            () => Timeline.ClearSelection(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineApplyRangeSelectionCommand = new DelegateCommand(
            () => Timeline.ApplyRangeSelection(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineClearRangeSelectionCommand = new DelegateCommand(
            () => Timeline.ClearRangeSelection(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        DiscoverShowDirectoryCommand = new DelegateCommand(
            () => LastShowImportCommandTask = RunDiscoverShowDirectoryAsync(),
            () => _navigator.State == StartupState.Ready && !ShowImport.IsBusy);
        GenerateEffectPlanCommand = new DelegateCommand(
            () => LastGenerateEffectPlanCommandTask = ShowImport.GenerateEffectPlanAsync(
                _threeEffectPlanFactory, _effectRecipeRegistry, _sequenceDocumentStore),
            () => _navigator.State == StartupState.Ready && !ShowImport.IsPlanBusy);
        // User feedback (2026-11-03): "don't allow generate until that is filled in" -- when the quality
        // pass toggle is on, Generate is disabled until a real xLights executable path is configured,
        // rather than letting the user click through to a QualityPassUnavailableException every time.
        // Quality pass off (the default) needs no such gate -- unchanged from before.
        GenerateWholeSongPlanCommand = new DelegateCommand(
            () => LastGenerateEffectPlanCommandTask = RunGenerateWholeSongPlanAsync(),
            () => _navigator.State == StartupState.Ready && !ShowImport.IsPlanBusy &&
                  (!CustomPrompt.EnableQualityPass || !string.IsNullOrWhiteSpace(ShowImport.XlightsExecutablePath)));
        ApplyPostProcessingPromptCommand = new DelegateCommand(
            () => LastApplyPostProcessingPromptCommandTask = ShowImport.ApplyPostProcessingPromptAsync(_wholeSongGenerator, Audio.ProbedAsset, _sequenceDocumentStore),
            () => _navigator.State == StartupState.Ready && !ShowImport.IsPostProcessingBusy);
        DownloadStemSeparationModelCommand = new DelegateCommand(
            () => LastDownloadStemSeparationModelCommandTask = ShowImport.DownloadStemSeparationModelAsync(_stemModelAssetManager),
            () => _navigator.State == StartupState.Ready && ShowImport.RequiresStemModelDownload && !ShowImport.IsDownloadingStemModel);
        ExportXsqCommand = new DelegateCommand(
            () => LastExportXsqCommandTask = ShowImport.ExportXsqAsync(_xsqExporter),
            () => _navigator.State == StartupState.Ready && !ShowImport.IsExportBusy);
        LaunchXlightsCommand = new DelegateCommand(
            () => LastLaunchXlightsCommandTask = ShowImport.LaunchXlightsAsync(_xlightsLauncher),
            () => _navigator.State == StartupState.Ready && !ShowImport.IsLaunchBusy);
        RunCorpusMiningCommand = new DelegateCommand(
            () => LastRunCorpusMiningCommandTask = RunCorpusMiningAsync(),
            () => _navigator.State == StartupState.Ready && !ShowImport.IsCorpusMiningBusy && _corpusKnowledgeDirectory is not null);
        CreateProjectCommand = new DelegateCommand(
            () => LastProjectCommandTask = RunCreateProjectAsync(),
            () => _navigator.State == StartupState.Ready && !Projects.IsBusy);
        OpenProjectCommand = new DelegateCommand(
            () => LastProjectCommandTask = RunOpenProjectAsync(),
            () => _navigator.State == StartupState.Ready && !Projects.IsBusy);
        CloseProjectCommand = new DelegateCommand(
            () => RunCloseCurrentProject(),
            () => _navigator.State == StartupState.Ready && HasCurrentProject);
        ProbeAudioFileCommand = new DelegateCommand(
            () => LastProbeAudioFileCommandTask = RunProbeAudioFileAsync(),
            () => _navigator.State == StartupState.Ready && !Audio.IsBusy);
        DownloadFfmpegCommand = new DelegateCommand(
            () => LastDownloadFfmpegCommandTask = Audio.DownloadFfmpegAsync(_ffmpegInstaller),
            () => _navigator.State == StartupState.Ready && Audio.RequiresFfmpegDownload && !Audio.IsDownloadingFfmpeg);
        OpenAudioPlaybackCommand = new DelegateCommand(
            () => LastOpenAudioPlaybackCommandTask = Audio.OpenPlaybackAsync(_audioTransport),
            () => _navigator.State == StartupState.Ready && Audio.HasProbedAsset);
        PlayAudioCommand = new DelegateCommand(
            () => LastPlayAudioCommandTask = Audio.PlayAsync(_audioTransport),
            () => _navigator.State == StartupState.Ready && Audio.IsPlaybackOpen && !Audio.IsPlaying);
        PauseAudioCommand = new DelegateCommand(
            () => LastPauseAudioCommandTask = Audio.PauseAsync(_audioTransport),
            () => _navigator.State == StartupState.Ready && Audio.IsPlaybackOpen && Audio.IsPlaying);
        SeekAudioCommand = new DelegateCommand(
            () => LastSeekAudioCommandTask = Audio.SeekAsync(_audioTransport),
            () => _navigator.State == StartupState.Ready && Audio.IsPlaybackOpen);
        RefreshAudioPositionCommand = new DelegateCommand(
            () => Audio.RefreshPlaybackPosition(_audioTransport),
            () => _navigator.State == StartupState.Ready && Audio.IsPlaybackOpen);
        BuildWaveformCacheCommand = new DelegateCommand(
            () => LastBuildWaveformCacheCommandTask = Audio.BuildWaveformCacheAsync(_waveformBuilder, _waveformPyramidStore),
            () => _navigator.State == StartupState.Ready && Audio.HasProbedAsset && !Audio.IsBuildingWaveform);
        ScrubTimelinePlayheadCommand = new DelegateCommand(
            () => LastScrubTimelinePlayheadCommandTask = RunScrubTimelinePlayheadAsync(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        TimelineZoomToSelectionCommand = new DelegateCommand(
            () => Timeline.ZoomToSelection(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded && Timeline.SelectedRangeScope is not null);
        SetTimelineLoopFromSelectionCommand = new DelegateCommand(
            () => RunSetLoopFromSelection(),
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded && Timeline.SelectedRangeScope is not null);
        ClearTimelineLoopCommand = new DelegateCommand(
            () => RunClearLoop(),
            () => _navigator.State == StartupState.Ready && Timeline.HasLoop);
        SetTimelineSnapNoneCommand = new DelegateCommand(
            () => Timeline.SnapPolicy = XlightsAI.Studio.Editing.SnapPolicy.None,
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);
        SetTimelineSnapFrameCommand = new DelegateCommand(
            () => Timeline.SnapPolicy = XlightsAI.Studio.Editing.SnapPolicy.Frame,
            () => _navigator.State == StartupState.Ready && Timeline.IsLoaded);

        _navigator.Changed += OnNavigatorChanged;

        // Raise CanExecuteChanged exactly when IsBusy actually flips, not just before/after the
        // async call — IsBusy becomes true synchronously inside Projects.CreateProjectAsync/
        // OpenProjectAsync, so without this the buttons would appear enabled for the full duration
        // of the operation (see RunCreateProjectAsync's own history: the previous implementation
        // raised CanExecuteChanged only before IsBusy was set, which never actually disabled them).
        Projects.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProjectsViewModel.IsBusy))
            {
                CreateProjectCommand.RaiseCanExecuteChanged();
                OpenProjectCommand.RaiseCanExecuteChanged();
            }
        };

        CustomPrompt.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CustomPromptEditorViewModel.EnableQualityPass))
            {
                GenerateWholeSongPlanCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ShowQualityPassPathWarning));
            }
        };

        ShowImport.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShowImportViewModel.IsBusy))
            {
                DiscoverShowDirectoryCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(ShowImportViewModel.IsLoaded))
            {
                OpenDisplayPathEditorCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(ShowImportViewModel.IsPlanBusy))
            {
                GenerateEffectPlanCommand.RaiseCanExecuteChanged();
                GenerateWholeSongPlanCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(ShowImportViewModel.IsPostProcessingBusy))
            {
                ApplyPostProcessingPromptCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName is nameof(ShowImportViewModel.RequiresStemModelDownload) or nameof(ShowImportViewModel.IsDownloadingStemModel))
            {
                DownloadStemSeparationModelCommand.RaiseCanExecuteChanged();
            }

            // User feedback (2026-11-03): "Can't that be loaded on start up and don't allow generate
            // until that is filled in" -- persisted on every change (so it survives the next app
            // restart, unlike before) and re-evaluates whether Generate can run (see
            // GenerateWholeSongPlanCommand's own CanExecute: quality pass needs a non-blank path).
            if (e.PropertyName == nameof(ShowImportViewModel.XlightsExecutablePath))
            {
                var current = _settingsStore.Load();
                if (current.XlightsExecutablePath != ShowImport.XlightsExecutablePath)
                {
                    _settingsStore.Save(current with { SchemaVersion = AppSettings.CurrentSchemaVersion, XlightsExecutablePath = ShowImport.XlightsExecutablePath });
                }

                GenerateWholeSongPlanCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ShowQualityPassPathWarning));
            }

            if (e.PropertyName == nameof(ShowImportViewModel.IsExportBusy))
            {
                ExportXsqCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(ShowImportViewModel.IsLaunchBusy))
            {
                LaunchXlightsCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(ShowImportViewModel.IsCorpusMiningBusy))
            {
                RunCorpusMiningCommand.RaiseCanExecuteChanged();
            }
        };

        FirstRun.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FirstRunViewModel.IsBusy))
            {
                CompleteFirstRunCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(FirstRunViewModel.IsInstallingOptionalAsset))
            {
                InstallOptionalAssetCommand.RaiseCanExecuteChanged();
            }
        };

        Audio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AudioImportViewModel.IsBusy))
            {
                ProbeAudioFileCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName is nameof(AudioImportViewModel.RequiresFfmpegDownload) or nameof(AudioImportViewModel.IsDownloadingFfmpeg))
            {
                DownloadFfmpegCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(AudioImportViewModel.HasProbedAsset))
            {
                OpenAudioPlaybackCommand.RaiseCanExecuteChanged();
                BuildWaveformCacheCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName is nameof(AudioImportViewModel.IsPlaybackOpen) or nameof(AudioImportViewModel.IsPlaying))
            {
                PlayAudioCommand.RaiseCanExecuteChanged();
                PauseAudioCommand.RaiseCanExecuteChanged();
                SeekAudioCommand.RaiseCanExecuteChanged();
                RefreshAudioPositionCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(AudioImportViewModel.IsBuildingWaveform))
            {
                BuildWaveformCacheCommand.RaiseCanExecuteChanged();
            }

            // PHASE-04 Step 028: every time the Audio screen's own bindable position/open-state
            // (itself always set from the real transport by OpenPlaybackAsync/PlayAsync/PauseAsync/
            // SeekAsync/RefreshPlaybackPosition) changes, push the exact same MasterPlaybackClock
            // reading into the Timeline's own playhead — the one place both screens' playhead state
            // is ever written, so they can never disagree.
            if (e.PropertyName is nameof(AudioImportViewModel.PlaybackPositionTicks) or nameof(AudioImportViewModel.IsPlaybackOpen))
            {
                Timeline.SetPlayhead(_masterClock.CanonicalPositionTicks, Audio.IsPlaybackOpen);
            }
        };

        Timeline.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimelineViewModel.IsLoaded))
            {
                TimelineZoomInCommand.RaiseCanExecuteChanged();
                TimelineZoomOutCommand.RaiseCanExecuteChanged();
                TimelineScrollLeftCommand.RaiseCanExecuteChanged();
                TimelineScrollRightCommand.RaiseCanExecuteChanged();
                TimelineSelectNextEffectCommand.RaiseCanExecuteChanged();
                TimelineSelectPreviousEffectCommand.RaiseCanExecuteChanged();
                TimelineClearSelectionCommand.RaiseCanExecuteChanged();
                TimelineApplyRangeSelectionCommand.RaiseCanExecuteChanged();
                TimelineClearRangeSelectionCommand.RaiseCanExecuteChanged();
                ScrubTimelinePlayheadCommand.RaiseCanExecuteChanged();
                TimelineZoomToSelectionCommand.RaiseCanExecuteChanged();
                SetTimelineLoopFromSelectionCommand.RaiseCanExecuteChanged();
                ClearTimelineLoopCommand.RaiseCanExecuteChanged();
                SetTimelineSnapNoneCommand.RaiseCanExecuteChanged();
                SetTimelineSnapFrameCommand.RaiseCanExecuteChanged();
            }

            // PHASE-04 Step 029: zoom-to-selection and set-loop are only ever meaningful against the
            // currently selected range, so their CanExecute tracks SelectedRangeScope directly (not
            // just IsLoaded above) — same "button reflects real preconditions" convention every other
            // command in this project already follows.
            if (e.PropertyName == nameof(TimelineViewModel.SelectedRangeScope))
            {
                TimelineZoomToSelectionCommand.RaiseCanExecuteChanged();
                SetTimelineLoopFromSelectionCommand.RaiseCanExecuteChanged();
            }

            if (e.PropertyName == nameof(TimelineViewModel.HasLoop))
            {
                ClearTimelineLoopCommand.RaiseCanExecuteChanged();
            }
        };

        DisplayPathEditor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DisplayPathEditorViewModel.IsLoaded) or nameof(DisplayPathEditorViewModel.SelectedNodeId))
            {
                CorrectDisplayNodePositionCommand.RaiseCanExecuteChanged();
            }
        };

        PianoRoll.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PianoRollViewModel.IsLoaded) or nameof(PianoRollViewModel.SelectedNoteId))
            {
                CorrectSelectedNoteCommand.RaiseCanExecuteChanged();
                ToggleLockSelectedNoteCommand.RaiseCanExecuteChanged();
            }
        };

        StructureInspector.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PhraseRelationshipInspectorViewModel.IsLoaded)
                or nameof(PhraseRelationshipInspectorViewModel.SelectedPhraseId)
                or nameof(PhraseRelationshipInspectorViewModel.PendingLinkTargetPhraseId))
            {
                UnlinkSelectedPhraseCommand.RaiseCanExecuteChanged();
                LinkSelectedPhraseCommand.RaiseCanExecuteChanged();
            }
        };
    }

    public WelcomeViewModel Welcome { get; }

    public ProjectsViewModel Projects { get; }

    public SettingsViewModel Settings { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public ShowImportViewModel ShowImport { get; }

    /// <summary>Build-plan Phase 14: the "last stop before you generate" custom-prompt tab's own view model.</summary>
    public CustomPromptEditorViewModel CustomPrompt { get; }

    /// <summary>PHASE-03 Step 016: SCREEN 13 TIMELINE.</summary>
    public TimelineViewModel Timeline { get; }

    /// <summary>PHASE-04 Step 024: SCREEN 06 AUDIO.</summary>
    public AudioImportViewModel Audio { get; }

    /// <summary>PHASE-07 Step 049: SCREEN 10 PIANO ROLL.</summary>
    public PianoRollViewModel PianoRoll { get; }

    /// <summary>PHASE-08 Step 058: SCREEN 11 STRUCTURE.</summary>
    public PhraseRelationshipInspectorViewModel StructureInspector { get; }

    /// <summary>PHASE-09 Step 061: SCREEN 05 DISPLAY PATHS.</summary>
    public DisplayPathEditorViewModel DisplayPathEditor { get; }

    /// <summary>PHASE-15 Step 095: first-run onboarding wizard.</summary>
    public FirstRunViewModel FirstRun { get; }

    /// <summary>Build-plan Phase 17 §2e: the exemplar curation library screen.</summary>
    public ExemplarLibraryViewModel ExemplarLibrary { get; }

    public DelegateCommand NavigateToProjectsCommand { get; }

    public DelegateCommand NavigateToWelcomeCommand { get; }

    public DelegateCommand NavigateToSettingsCommand { get; }

    public DelegateCommand NavigateToDiagnosticsCommand { get; }

    public DelegateCommand NavigateToExemplarLibraryCommand { get; }

    public DelegateCommand NavigateToShowImportCommand { get; }

    /// <summary>Navigates to <see cref="ShellScreen.CustomPrompt"/>.</summary>
    public DelegateCommand NavigateToCustomPromptCommand { get; }

    /// <summary>Navigates to <see cref="ShellScreen.GenerateExport"/> (same <see cref="ShowImport"/> view model).</summary>
    public DelegateCommand NavigateToGenerateExportCommand { get; }

    public DelegateCommand NavigateToTimelineCommand { get; }

    public DelegateCommand NavigateToAudioCommand { get; }

    public DelegateCommand NavigateToPianoRollCommand { get; }

    public DelegateCommand NavigateToStructureInspectorCommand { get; }

    public DelegateCommand NavigateToDisplayPathEditorCommand { get; }

    /// <summary>
    /// Builds a <see cref="XlightsAI.Studio.Display.SpatialDisplayGraph"/> from
    /// <see cref="ShowImportViewModel.Layout"/> and opens it on <see cref="DisplayPathEditor"/> —
    /// enabled only once a show layout has actually been imported. Same "nothing happens until an
    /// explicit user action" convention as every other screen action in this class; navigating to
    /// the Display Paths screen alone never triggers this.
    /// </summary>
    public DelegateCommand OpenDisplayPathEditorCommand { get; }

    /// <summary>Applies <see cref="DisplayPathEditorViewModel.PendingPositionXText"/>/<see cref="DisplayPathEditorViewModel.PendingPositionYText"/> to the selected node — the "drag" action, entered numerically rather than by pointer drag (same scope decision as <see cref="TimelineViewModel"/>'s own numeric range selection).</summary>
    public DelegateCommand CorrectDisplayNodePositionCommand { get; }

    /// <summary>Applies <see cref="PianoRollViewModel.PendingMidiNoteText"/>/<see cref="PianoRollViewModel.PendingStartTicksText"/>/<see cref="PianoRollViewModel.PendingEndTicksText"/> to the selected note.</summary>
    public DelegateCommand CorrectSelectedNoteCommand { get; }

    /// <summary>Toggles the selected note's locked bit.</summary>
    public DelegateCommand ToggleLockSelectedNoteCommand { get; }

    /// <summary>Unlinks the selected phrase from every other phrase currently in its effective motif group.</summary>
    public DelegateCommand UnlinkSelectedPhraseCommand { get; }

    /// <summary>Links the selected phrase to <see cref="PhraseRelationshipInspectorViewModel.PendingLinkTargetPhraseId"/>.</summary>
    public DelegateCommand LinkSelectedPhraseCommand { get; }

    /// <summary>PHASE-15 Step 095: navigates to the first-run onboarding wizard (also entered automatically on a fresh install — see <see cref="RunStartup"/>).</summary>
    public DelegateCommand NavigateToFirstRunCommand { get; }

    /// <summary>PHASE-15 Step 095 "Complete First Run" action: validates/creates <see cref="FirstRunViewModel.WorkspaceDirectory"/>, probes runtime capability and startup health, then — on success — persists <see cref="AppSettings.WorkspaceDirectory"/>/<see cref="AppSettings.HasCompletedFirstRun"/> and returns to Welcome.</summary>
    public DelegateCommand CompleteFirstRunCommand { get; }

    /// <summary>PHASE-15 Step 096 "Install Optional Asset" action: installs <see cref="_optionalModelAsset"/> (if any) via the real <see cref="_assetInstaller"/> only when <see cref="FirstRunViewModel.OptionalAssetConsent"/> is set, and records the real, human-readable outcome to <see cref="IAppDiagnostics"/>.</summary>
    public DelegateCommand InstallOptionalAssetCommand { get; }

    /// <summary>PHASE-03 Step 016 "Load Timeline" action, against <see cref="TimelineViewModel.TotalDurationTicks"/>.</summary>
    public DelegateCommand LoadTimelineCommand { get; }

    public DelegateCommand TimelineZoomInCommand { get; }

    public DelegateCommand TimelineZoomOutCommand { get; }

    public DelegateCommand TimelineScrollLeftCommand { get; }

    public DelegateCommand TimelineScrollRightCommand { get; }

    /// <summary>PHASE-03 Step 018 keyboard navigation: selects the next/previous effect in visual order.</summary>
    public DelegateCommand TimelineSelectNextEffectCommand { get; }

    public DelegateCommand TimelineSelectPreviousEffectCommand { get; }

    public DelegateCommand TimelineClearSelectionCommand { get; }

    /// <summary>PHASE-03 Step 021: builds <see cref="TimelineViewModel.SelectedRangeScope"/> from the current drag-rectangle inputs.</summary>
    public DelegateCommand TimelineApplyRangeSelectionCommand { get; }

    public DelegateCommand TimelineClearRangeSelectionCommand { get; }

    public DelegateCommand CreateProjectCommand { get; }

    public DelegateCommand OpenProjectCommand { get; }

    /// <summary>Clears <see cref="CurrentProject"/> without touching disk — the explicit "I'm done with this project" action; the project itself, and everything it persisted, is untouched.</summary>
    public DelegateCommand CloseProjectCommand { get; }

    /// <summary>PHASE-04 Step 024 "Probe Audio File" action.</summary>
    public DelegateCommand ProbeAudioFileCommand { get; }

    /// <summary>"Download ffmpeg" action: enabled only once a probe attempt has set <see cref="AudioImportViewModel.RequiresFfmpegDownload"/>.</summary>
    public DelegateCommand DownloadFfmpegCommand { get; }

    /// <summary>PHASE-04 Step 025 "Open Playback" action: opens the real device transport against the most recently probed asset.</summary>
    public DelegateCommand OpenAudioPlaybackCommand { get; }

    /// <summary>PHASE-04 Step 025 "Play" action.</summary>
    public DelegateCommand PlayAudioCommand { get; }

    /// <summary>PHASE-04 Step 025 "Pause" action.</summary>
    public DelegateCommand PauseAudioCommand { get; }

    /// <summary>PHASE-04 Step 025 "Seek" action, against <see cref="AudioImportViewModel.SeekTargetTicksText"/>.</summary>
    public DelegateCommand SeekAudioCommand { get; }

    /// <summary>PHASE-04 Step 025 "Refresh Position" action: re-reads the real device position without issuing a new transport command.</summary>
    public DelegateCommand RefreshAudioPositionCommand { get; }

    /// <summary>PHASE-04 Step 026 "Build Waveform Cache" action: builds (or reopens an already-valid) the multiresolution waveform peaks pyramid for the most recently probed asset.</summary>
    public DelegateCommand BuildWaveformCacheCommand { get; }

    /// <summary>
    /// PHASE-04 Step 028 "Scrub Playhead" action, against <see cref="TimelineViewModel.ScrubTargetTicksText"/>:
    /// validates the target against the loaded timeline's own content bounds
    /// (<see cref="TimelineViewModel.ValidateScrubTarget"/>), then seeks the one real
    /// <see cref="MasterPlaybackClock"/> both the Audio and Timeline screens share — never a
    /// standalone, Timeline-only seek that could disagree with the Audio screen's own position.
    /// </summary>
    public DelegateCommand ScrubTimelinePlayheadCommand { get; }

    /// <summary>
    /// PHASE-04 Step 029 "Zoom to Selection" action: rezooms/scrolls the Timeline so
    /// <see cref="TimelineViewModel.SelectedRangeScope"/>'s own time window exactly fills the
    /// viewport. Disabled whenever nothing is selected.
    /// </summary>
    public DelegateCommand TimelineZoomToSelectionCommand { get; }

    /// <summary>
    /// PHASE-04 Step 029 "Set Loop (A/B)" action: arms the loop window from the currently selected
    /// time range on the one real shared <see cref="IAudioTransport"/> both the Audio and Timeline
    /// screens already play through, then mirrors the result into
    /// <see cref="TimelineViewModel.LoopWindow"/>. Disabled whenever nothing is selected.
    /// </summary>
    public DelegateCommand SetTimelineLoopFromSelectionCommand { get; }

    /// <summary>PHASE-04 Step 029 "Clear Loop" action. Disabled whenever no loop is currently armed.</summary>
    public DelegateCommand ClearTimelineLoopCommand { get; }

    /// <summary>PHASE-04 Step 030 "Snap: None" action — sets <see cref="TimelineViewModel.SnapPolicy"/> to <see cref="XlightsAI.Studio.Editing.SnapPolicy.None"/>. Disabled while no timeline is loaded (the "stale" case).</summary>
    public DelegateCommand SetTimelineSnapNoneCommand { get; }

    /// <summary>PHASE-04 Step 030 "Snap: Frame" action — sets <see cref="TimelineViewModel.SnapPolicy"/> to <see cref="XlightsAI.Studio.Editing.SnapPolicy.Frame"/>. Disabled while no timeline is loaded (the "stale" case).</summary>
    public DelegateCommand SetTimelineSnapFrameCommand { get; }

    public DelegateCommand DiscoverShowDirectoryCommand { get; }

    /// <summary>PHASE-02 Step 012 "Generate 3 Effects" action.</summary>
    public DelegateCommand GenerateEffectPlanCommand { get; }

    /// <summary>Real whole-song AI pipeline "Generate Whole-Song Plan" action (uses every real prop, never a manually-typed 3-name list).</summary>
    public DelegateCommand GenerateWholeSongPlanCommand { get; }

    /// <summary>Build-plan "richer analysis tools" post-processing phase (2026-11-02): applies <see cref="ShowImportViewModel.PostProcessingPromptText"/> as a targeted touch-up to the most recent generation, leaving every other section untouched.</summary>
    public DelegateCommand ApplyPostProcessingPromptCommand { get; }

    /// <summary>"Download separation model" action: enabled only once a whole-song plan attempt has set <see cref="ShowImportViewModel.RequiresStemModelDownload"/>.</summary>
    public DelegateCommand DownloadStemSeparationModelCommand { get; }

    /// <summary>PHASE-02 Step 013 "Export XSQ" action.</summary>
    public DelegateCommand ExportXsqCommand { get; }

    /// <summary>PHASE-02 Step 014 "Open in xLights" action.</summary>
    public DelegateCommand LaunchXlightsCommand { get; }

    /// <summary>Build-plan Phase 10 §3f "Run corpus mining" action (Generate &amp; Export screen's Developer Tools). Disabled while a run is already in progress, or while no <c>corpusKnowledgeDirectory</c> was supplied by the composition root.</summary>
    public DelegateCommand RunCorpusMiningCommand { get; }

    /// <summary>
    /// The in-flight task started by the most recent <see cref="CreateProjectCommand"/>/
    /// <see cref="OpenProjectCommand"/> execution. <see cref="DelegateCommand"/>/<see cref="System.Windows.Input.ICommand"/>
    /// has no async member, so a real UI click cannot await this; it exists so an automated test
    /// (or, if a future step wants it, a UI busy-overlay) can deterministically await completion
    /// instead of polling <see cref="ProjectsViewModel.IsBusy"/>, which can momentarily read
    /// <see langword="false"/> after the service call finishes but before the resulting list
    /// refresh has run.
    /// </summary>
    public Task? LastProjectCommandTask { get; private set; }

    /// <summary>The fire-and-forget recent-projects load <see cref="OnNavigatorChanged"/> kicks off when navigating to the Projects screen, so a test can deterministically await it -- same reason every other <c>LastXxxTask</c> property exists.</summary>
    public Task? LastLoadRecentProjectsTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="DiscoverShowDirectoryCommand"/>.</summary>
    public Task? LastShowImportCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="OpenDisplayPathEditorCommand"/>.</summary>
    public Task? LastOpenDisplayPathEditorCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="CorrectDisplayNodePositionCommand"/>.</summary>
    public Task? LastCorrectDisplayNodePositionCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="CorrectSelectedNoteCommand"/>.</summary>
    public Task? LastCorrectSelectedNoteCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="ToggleLockSelectedNoteCommand"/>.</summary>
    public Task? LastToggleLockSelectedNoteCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="UnlinkSelectedPhraseCommand"/>.</summary>
    public Task? LastUnlinkSelectedPhraseCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="LinkSelectedPhraseCommand"/>.</summary>
    public Task? LastLinkSelectedPhraseCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="GenerateEffectPlanCommand"/>.</summary>
    public Task? LastGenerateEffectPlanCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="ApplyPostProcessingPromptCommand"/>.</summary>
    public Task? LastApplyPostProcessingPromptCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="DownloadStemSeparationModelCommand"/>.</summary>
    public Task? LastDownloadStemSeparationModelCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="ExportXsqCommand"/>.</summary>
    public Task? LastExportXsqCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="LaunchXlightsCommand"/>.</summary>
    public Task? LastLaunchXlightsCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="RunCorpusMiningCommand"/>.</summary>
    public Task? LastRunCorpusMiningCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="ProbeAudioFileCommand"/>.</summary>
    public Task? LastProbeAudioFileCommandTask { get; private set; }

    /// <summary>The fire-and-forget startup auto-discovery task <see cref="RunStartup"/> kicks off when a previously-used show directory still exists on disk, so a test can deterministically await it -- same reason every other <c>LastXxxCommandTask</c> property exists.</summary>
    public Task? LastStartupDiscoverShowDirectoryTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="DownloadFfmpegCommand"/>.</summary>
    public Task? LastDownloadFfmpegCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="OpenAudioPlaybackCommand"/>.</summary>
    public Task? LastOpenAudioPlaybackCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="PlayAudioCommand"/>.</summary>
    public Task? LastPlayAudioCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="PauseAudioCommand"/>.</summary>
    public Task? LastPauseAudioCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="SeekAudioCommand"/>.</summary>
    public Task? LastSeekAudioCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="BuildWaveformCacheCommand"/>.</summary>
    public Task? LastBuildWaveformCacheCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="ScrubTimelinePlayheadCommand"/>.</summary>
    public Task? LastScrubTimelinePlayheadCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="CompleteFirstRunCommand"/>.</summary>
    public Task? LastCompleteFirstRunCommandTask { get; private set; }

    /// <summary>Same deterministic-await purpose as <see cref="LastProjectCommandTask"/>, for <see cref="InstallOptionalAssetCommand"/>.</summary>
    public Task? LastInstallOptionalAssetCommandTask { get; private set; }

    public StartupState State => _navigator.State;

    public StartupFailure? Failure => _navigator.Failure;

    public ShellScreen CurrentScreen => _navigator.CurrentScreen;

    /// <summary>
    /// The project created/opened via <see cref="CreateProjectCommand"/>/<see cref="OpenProjectCommand"/>
    /// this session, or <see langword="null"/> if none has been (or it has been explicitly closed via
    /// <see cref="CloseProjectCommand"/>). Set from the real <see cref="ProjectManifest"/>
    /// <see cref="ProjectsViewModel.CreateProjectAsync"/>/<see cref="ProjectsViewModel.OpenProjectAsync"/>
    /// already return — previously discarded immediately after a successful create/open, which is
    /// why no other screen could ever answer "which project is this." Restored across a restart only
    /// as a pre-filled, re-openable suggestion (<see cref="AppSettings.LastProjectRootDirectory"/> via
    /// <see cref="ProjectsViewModel.RestoreLastProjectDirectory"/>), never auto-reopened, same
    /// "restore the typed value, require an explicit action" convention as every other restored path
    /// in this class.
    /// </summary>
    public ProjectManifest? CurrentProject
    {
        get => _currentProject;
        private set
        {
            if (SetField(ref _currentProject, value))
            {
                OnPropertyChanged(nameof(HasCurrentProject));
                CloseProjectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasCurrentProject => CurrentProject is not null;

    public bool IsStarting => State == StartupState.Starting;

    public bool IsFailed => State == StartupState.Failed;

    public bool IsShowingWelcome => State == StartupState.Ready && CurrentScreen == ShellScreen.Welcome;

    public bool IsShowingProjects => State == StartupState.Ready && CurrentScreen == ShellScreen.Projects;

    public bool IsShowingSettings => State == StartupState.Ready && CurrentScreen == ShellScreen.Settings;

    public bool IsShowingDiagnostics => State == StartupState.Ready && CurrentScreen == ShellScreen.Diagnostics;

    public bool IsShowingExemplarLibrary => State == StartupState.Ready && CurrentScreen == ShellScreen.ExemplarLibrary;

    public bool IsShowingShowImport => State == StartupState.Ready && CurrentScreen == ShellScreen.ShowImport;

    public bool IsShowingCustomPrompt => State == StartupState.Ready && CurrentScreen == ShellScreen.CustomPrompt;

    /// <summary>
    /// User feedback (2026-11-03): "don't allow generate until that is filled in" -- true exactly when
    /// <see cref="GenerateWholeSongPlanCommand"/>'s own quality-pass gate is the reason Generate is
    /// disabled, so the Generate &amp; export screen can explain why instead of showing a silently-disabled
    /// button.
    /// </summary>
    public bool ShowQualityPassPathWarning => CustomPrompt.EnableQualityPass && string.IsNullOrWhiteSpace(ShowImport.XlightsExecutablePath);

    public bool IsShowingGenerateExport => State == StartupState.Ready && CurrentScreen == ShellScreen.GenerateExport;

    public bool IsShowingTimeline => State == StartupState.Ready && CurrentScreen == ShellScreen.Timeline;

    public bool IsShowingAudio => State == StartupState.Ready && CurrentScreen == ShellScreen.Audio;

    public bool IsShowingPianoRoll => State == StartupState.Ready && CurrentScreen == ShellScreen.PianoRoll;

    public bool IsShowingStructureInspector => State == StartupState.Ready && CurrentScreen == ShellScreen.StructureInspector;

    public bool IsShowingDisplayPathEditor => State == StartupState.Ready && CurrentScreen == ShellScreen.DisplayPathEditor;

    public bool IsShowingFirstRun => State == StartupState.Ready && CurrentScreen == ShellScreen.FirstRun;

    /// <summary>
    /// Runs the (synchronous, for this step) startup sequence. Any thrown exception is captured
    /// as an explicit <see cref="StartupState.Failed"/> transition and recorded to
    /// <see cref="IAppDiagnostics"/> (crash handling: the failure is reported, not silently
    /// swallowed and not a process crash), rather than propagating.
    ///
    /// On a successful startup, restores the previously persisted screen (if any) from
    /// <see cref="ISettingsStore"/> — the "restart restores settings" acceptance for this step.
    /// </summary>
    public void RunStartup(Action? startupWork = null)
    {
        try
        {
            startupWork?.Invoke();

            // Load settings before completing startup: CompleteStartup() itself raises Changed
            // (CurrentScreen == Welcome at that point), and without this guard that transition
            // would save "Welcome" and clobber the very value being restored below.
            var settings = _settingsStore.Load();

            _isRestoringLastScreen = true;
            try
            {
                _navigator.CompleteStartup();

                // PHASE-15 Step 095 required acceptance, verbatim: "User completes first run
                // without command line." A fresh install (no persisted HasCompletedFirstRun yet)
                // always lands on the onboarding wizard first, taking priority over restoring
                // whatever LastScreen a pre-Step-095 settings file happened to record — there is no
                // meaningful "last screen" to return to before a workspace has ever been chosen.
                if (!settings.HasCompletedFirstRun)
                {
                    _navigator.NavigateTo(ShellScreen.FirstRun);
                }
                else if (settings.LastScreen != ShellScreen.Welcome)
                {
                    _navigator.NavigateTo(settings.LastScreen);
                }
            }
            finally
            {
                _isRestoringLastScreen = false;
            }

            // User feedback (2026-11-01): "on startup preload Show layout if a folder is there" -- a
            // previously-discovered show folder used to only prefill the text box (RestoreLastDirectory),
            // requiring an explicit "Read show folder" click every single session before anything else
            // could be done. If that folder still exists on disk, actually re-run discovery now, not just
            // show its path. Fire-and-forget (RunStartup itself stays synchronous, same as every other
            // startup step) -- the task is kept on LastStartupDiscoverShowDirectoryTask so a test can
            // deterministically await it, same convention as every LastXxxCommandTask property. A missing/
            // renamed/deleted folder is silently skipped here (not an error) -- the user still sees the
            // ordinary empty Show Import screen and can browse to wherever it went.
            ShowImport.SetRecentShowDirectories(settings.RecentShowDirectoriesOrEmpty);
            if (!string.IsNullOrWhiteSpace(settings.LastShowDirectory) && Directory.Exists(settings.LastShowDirectory))
            {
                ShowImport.RestoreLastDirectory(settings.LastShowDirectory);
                LastStartupDiscoverShowDirectoryTask = RunDiscoverShowDirectoryAsync();
            }

            // User feedback (2026-11-03): "Can't that be loaded on start up" -- the xLights executable
            // path used to reset to blank every session, re-triggering the quality-pass toggle's own
            // QualityPassUnavailableException even after it had already been configured once.
            ShowImport.RestoreXlightsExecutablePath(settings.XlightsExecutablePath);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordFailure("startup-failed", ex);
            _navigator.FailStartup("startup-failed", ex.Message);
        }
    }

    private void OnNavigatorChanged(object? sender, EventArgs e)
    {
        OnPropertyChangedAll();

        if (_navigator.State == StartupState.Ready)
        {
            switch (_navigator.CurrentScreen)
            {
                case ShellScreen.Projects:
                    Projects.Load(_projectNamesSource);
                    Projects.RestoreLastProjectDirectory(_settingsStore.Load().LastProjectRootDirectory);
                    LastLoadRecentProjectsTask = RunLoadRecentProjectsAsync();
                    break;
                case ShellScreen.Settings:
                    Settings.Load(_settingsStore.Load);
                    break;
                case ShellScreen.Diagnostics:
                    Diagnostics.Load(() => _diagnostics.RecentEvents);
                    break;
                case ShellScreen.ExemplarLibrary:
                    // Build-plan Phase 17 §2e: re-load every time this screen is navigated to (unlike
                    // the "restore, never re-run" screens above) -- a re-mine since the last visit
                    // should be visible without a separate manual refresh action, and loading is cheap
                    // local JSON reads, not a real corpus-mining run.
                    if (_corpusKnowledgeDirectory is not null)
                    {
                        _ = ExemplarLibrary.LoadAsync(_corpusKnowledgeDirectory, CancellationToken.None);
                    }
                    break;
                case ShellScreen.ShowImport:
                    {
                        var showImportSettings = _settingsStore.Load();
                        ShowImport.RestoreLastDirectory(showImportSettings.LastShowDirectory);
                        ShowImport.SetRecentShowDirectories(showImportSettings.RecentShowDirectoriesOrEmpty);
                    }

                    break;
                case ShellScreen.CustomPrompt:
                    // Build-plan Phase 14 §2a.2: reload the persisted prompt (round-trips across
                    // sessions) every time this screen is navigated to, same "restore, never re-run"
                    // convention as ShowImport.RestoreLastDirectory above.
                    _ = CustomPrompt.LoadAsync();
                    break;
                case ShellScreen.GenerateExport:
                    // Same "nothing happens until an explicit user action" convention as Timeline
                    // below: this screen reads the ShowImport view model the import screen already
                    // populated, and never re-runs discovery or a plan on navigation alone. Restoring
                    // CorpusRootDirectory is the one exception -- same "fill in a blank field only"
                    // convention as ShowImport.RestoreLastDirectory above, it never runs mining itself.
                    ShowImport.RestoreLastCorpusRootDirectory(_settingsStore.Load().CorpusRootDirectory);
                    ShowImport.RestoreXlightsExecutablePath(_settingsStore.Load().XlightsExecutablePath);
                    break;
                case ShellScreen.Timeline:
                    // Deliberately does not auto-load: the required failure/edge case ("missing
                    // media must be handled explicitly") means navigating to this screen with no
                    // media loaded yet must show the real Empty state, not a silently-populated
                    // default viewport — the user explicitly clicks Load Timeline (same "nothing
                    // happens until an explicit user action" convention as ShowImport's Discover).
                    break;
                case ShellScreen.Audio:
                    // Same "nothing happens until an explicit user action" convention as Timeline
                    // above: navigating here never auto-probes a stale/previous path.
                    break;
                case ShellScreen.PianoRoll:
                    // Same "nothing happens until an explicit user action" convention as Timeline/
                    // Audio above: navigating here never auto-loads a stale previous track/session.
                    break;
                case ShellScreen.StructureInspector:
                    // Same "nothing happens until an explicit user action" convention as PianoRoll
                    // above: navigating here never auto-opens a stale previous song's structure.
                    break;
                case ShellScreen.DisplayPathEditor:
                    // Same "nothing happens until an explicit user action" convention as PianoRoll/
                    // StructureInspector above: navigating here never auto-opens a stale previous
                    // layout.
                    break;
                case ShellScreen.FirstRun:
                    // Restore (never re-create) a previously entered-but-not-yet-completed workspace
                    // choice, same "restore, never re-run" convention as ShowImport above.
                    FirstRun.RestoreLastWorkspaceDirectory(_settingsStore.Load().WorkspaceDirectory);
                    break;
            }

            if (!_isRestoringLastScreen)
            {
                // Preserve LastShowDirectory (Step 009) / WorkspaceDirectory + HasCompletedFirstRun
                // (Step 095) across a plain screen-navigation save — this save path only ever
                // intends to update LastScreen. Uses `with` (not positional reconstruction) so a
                // future additive field is preserved automatically too.
                var existing = _settingsStore.Load();
                _settingsStore.Save(existing with { SchemaVersion = AppSettings.CurrentSchemaVersion, LastScreen = _navigator.CurrentScreen });
            }
        }

        NavigateToProjectsCommand.RaiseCanExecuteChanged();
        NavigateToWelcomeCommand.RaiseCanExecuteChanged();
        NavigateToSettingsCommand.RaiseCanExecuteChanged();
        NavigateToDiagnosticsCommand.RaiseCanExecuteChanged();
        NavigateToExemplarLibraryCommand.RaiseCanExecuteChanged();
        NavigateToShowImportCommand.RaiseCanExecuteChanged();
        NavigateToCustomPromptCommand.RaiseCanExecuteChanged();
        NavigateToGenerateExportCommand.RaiseCanExecuteChanged();
        NavigateToTimelineCommand.RaiseCanExecuteChanged();
        NavigateToAudioCommand.RaiseCanExecuteChanged();
        NavigateToPianoRollCommand.RaiseCanExecuteChanged();
        NavigateToStructureInspectorCommand.RaiseCanExecuteChanged();
        NavigateToDisplayPathEditorCommand.RaiseCanExecuteChanged();
        OpenDisplayPathEditorCommand.RaiseCanExecuteChanged();
        CorrectDisplayNodePositionCommand.RaiseCanExecuteChanged();
        CorrectSelectedNoteCommand.RaiseCanExecuteChanged();
        ToggleLockSelectedNoteCommand.RaiseCanExecuteChanged();
        UnlinkSelectedPhraseCommand.RaiseCanExecuteChanged();
        LinkSelectedPhraseCommand.RaiseCanExecuteChanged();
        NavigateToFirstRunCommand.RaiseCanExecuteChanged();
        CompleteFirstRunCommand.RaiseCanExecuteChanged();
        ProbeAudioFileCommand.RaiseCanExecuteChanged();
        DownloadFfmpegCommand.RaiseCanExecuteChanged();
        OpenAudioPlaybackCommand.RaiseCanExecuteChanged();
        PlayAudioCommand.RaiseCanExecuteChanged();
        PauseAudioCommand.RaiseCanExecuteChanged();
        SeekAudioCommand.RaiseCanExecuteChanged();
        RefreshAudioPositionCommand.RaiseCanExecuteChanged();
        BuildWaveformCacheCommand.RaiseCanExecuteChanged();
        LoadTimelineCommand.RaiseCanExecuteChanged();
        TimelineZoomInCommand.RaiseCanExecuteChanged();
        TimelineZoomOutCommand.RaiseCanExecuteChanged();
        TimelineScrollLeftCommand.RaiseCanExecuteChanged();
        TimelineScrollRightCommand.RaiseCanExecuteChanged();
        TimelineSelectNextEffectCommand.RaiseCanExecuteChanged();
        TimelineSelectPreviousEffectCommand.RaiseCanExecuteChanged();
        TimelineClearSelectionCommand.RaiseCanExecuteChanged();
        TimelineApplyRangeSelectionCommand.RaiseCanExecuteChanged();
        TimelineClearRangeSelectionCommand.RaiseCanExecuteChanged();
        ScrubTimelinePlayheadCommand.RaiseCanExecuteChanged();
        TimelineZoomToSelectionCommand.RaiseCanExecuteChanged();
        SetTimelineLoopFromSelectionCommand.RaiseCanExecuteChanged();
        ClearTimelineLoopCommand.RaiseCanExecuteChanged();
        CreateProjectCommand.RaiseCanExecuteChanged();
        OpenProjectCommand.RaiseCanExecuteChanged();
        CloseProjectCommand.RaiseCanExecuteChanged();
        DiscoverShowDirectoryCommand.RaiseCanExecuteChanged();
        GenerateEffectPlanCommand.RaiseCanExecuteChanged();
        GenerateWholeSongPlanCommand.RaiseCanExecuteChanged();
        ApplyPostProcessingPromptCommand.RaiseCanExecuteChanged();
        DownloadStemSeparationModelCommand.RaiseCanExecuteChanged();
        ExportXsqCommand.RaiseCanExecuteChanged();
        LaunchXlightsCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Windows uses winmm and macOS uses OpenAL; any other platform gets the typed "unsupported" stub (see <see cref="AudioTransportFactory"/>).</summary>
    private static IAudioTransport CreateDefaultAudioTransport(IAudioDecoder audioDecoder) =>
        AudioTransportFactory.Create(audioDecoder);

    /// <summary>
    /// Creates a project from <see cref="ProjectsViewModel.WorkspaceDirectory"/>/
    /// <see cref="ProjectsViewModel.NewProjectName"/>, then — on success — reloads the Projects
    /// list from the same <paramref name="_projectNamesSource"/> the screen already uses, so the
    /// newly created project is immediately visible without a separate manual refresh, and adopts
    /// the returned manifest as <see cref="CurrentProject"/> (see <see cref="OnProjectOpened"/>).
    /// </summary>
    private async Task RunCreateProjectAsync()
    {
        var manifest = await Projects.CreateProjectAsync(_projectService).ConfigureAwait(true);
        if (manifest is not null)
        {
            Projects.Load(_projectNamesSource);
            OnProjectOpened(manifest);
        }
    }

    /// <summary>Opens the project at <see cref="ProjectsViewModel.WorkspaceDirectory"/>; same reload-on-success/adopt-as-current contract as <see cref="RunCreateProjectAsync"/>.</summary>
    private async Task RunOpenProjectAsync()
    {
        var manifest = await Projects.OpenProjectAsync(_projectService).ConfigureAwait(true);
        if (manifest is not null)
        {
            Projects.Load(_projectNamesSource);
            OnProjectOpened(manifest);
        }
    }

    /// <summary>
    /// User feedback (2026-11-01): populates <see cref="ProjectsViewModel.RecentProjects"/> from
    /// <see cref="IProjectService.RecentAsync"/> every time the Projects screen is shown -- a failure
    /// here (e.g. a corrupt recent-projects file) must never block the screen itself, so it is swallowed
    /// rather than surfaced as an action error; the dropdown is simply empty for this visit.
    /// </summary>
    private async Task RunLoadRecentProjectsAsync()
    {
        try
        {
            var recentProjects = await _projectService.RecentAsync(CancellationToken.None).ConfigureAwait(true);
            Projects.SetRecentProjects(recentProjects);
        }
        catch (Exception)
        {
            Projects.SetRecentProjects([]);
        }
    }

    /// <summary>
    /// Adopts <paramref name="manifest"/> as <see cref="CurrentProject"/> and persists its root
    /// directory as <see cref="AppSettings.LastProjectRootDirectory"/> (restored, never
    /// auto-reopened, next time the Projects screen is shown — see
    /// <see cref="ProjectsViewModel.RestoreLastProjectDirectory"/>), same "preserve the other
    /// restorable fields" `with`-pattern every other persisted-path save in this class uses.
    /// </summary>
    private void OnProjectOpened(ProjectManifest manifest)
    {
        CurrentProject = manifest;
        ShowImport.SetDefaultXsqOutputDirectory(manifest.RootDirectory);

        var existing = _settingsStore.Load();
        _settingsStore.Save(existing with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            LastProjectRootDirectory = manifest.RootDirectory,
        });
    }

    /// <summary>
    /// Clears <see cref="CurrentProject"/> — the <see cref="CloseProjectCommand"/> action. Never
    /// touches disk or <see cref="AppSettings.LastProjectRootDirectory"/>: closing is a session-local
    /// "nothing is open right now," not "forget this project exists," so the directory is still
    /// offered back on the Projects screen next time.
    /// </summary>
    private void RunCloseCurrentProject() => CurrentProject = null;

    /// <summary>
    /// Runs discovery against <see cref="ShowImportViewModel.ShowDirectory"/>; on success, persists
    /// it as the new "last successful folder selection" (Step 009 required behavior) — a failed
    /// discovery (missing folder/required file, cancellation, invalid input) never updates the
    /// persisted value, so a prior good selection survives a later failed attempt.
    /// </summary>
    /// <summary>
    /// User feedback (2026-11-01): "when you load the song automatically rebuild waveform" -- probing a
    /// song used to leave the waveform cache stale/absent until a separate manual "Build waveform" click.
    /// On a successful probe, immediately builds the waveform cache for the newly-probed asset too, so
    /// the Timeline screen's waveform is ready without a second action. A failed/cancelled probe never
    /// attempts a build (nothing valid to build from); a failed build surfaces via
    /// <see cref="AudioImportViewModel"/>'s own existing error state exactly as the manual button already
    /// does -- this only changes when that call happens, not its own error handling.
    /// </summary>
    private async Task<bool> RunProbeAudioFileAsync()
    {
        var probed = await Audio.ProbeAudioFileAsync(_audioDecoder, _songAssetStore).ConfigureAwait(true);
        if (probed)
        {
            await Audio.BuildWaveformCacheAsync(_waveformBuilder, _waveformPyramidStore).ConfigureAwait(true);
        }

        return probed;
    }

    private async Task RunDiscoverShowDirectoryAsync()
    {
        var succeeded = await ShowImport.DiscoverAsync(_showDirectoryDiscoveryService, _showLayoutImporter).ConfigureAwait(true);
        if (succeeded)
        {
            var existing = _settingsStore.Load();
            // User feedback (2026-11-01): push this directory to the front of the recent list (dedup,
            // case-insensitive -- Windows paths), capped at MaxRecentShowDirectories, so the dropdown
            // never grows without bound and a re-opened show jumps back to the top rather than appearing
            // twice.
            var recent = new List<string> { ShowImport.ShowDirectory };
            recent.AddRange(existing.RecentShowDirectoriesOrEmpty.Where(d => !string.Equals(d, ShowImport.ShowDirectory, StringComparison.OrdinalIgnoreCase)));
            var trimmedRecent = recent.Take(AppSettings.MaxRecentShowDirectories).ToList();

            _settingsStore.Save(existing with
            {
                SchemaVersion = AppSettings.CurrentSchemaVersion,
                LastShowDirectory = ShowImport.ShowDirectory,
                RecentShowDirectories = trimmedRecent,
            });
            ShowImport.SetRecentShowDirectories(trimmedRecent);
        }
    }

    /// <summary>
    /// Runs <see cref="ShowImportViewModel.RunCorpusMiningAsync"/> against <see cref="_corpusKnowledgeDirectory"/>;
    /// on success, persists <see cref="ShowImportViewModel.CorpusRootDirectory"/> as the new "last
    /// corpus root" (same reasoning as <see cref="RunDiscoverShowDirectoryAsync"/>'s own
    /// <see cref="AppSettings.LastShowDirectory"/> persistence) -- a failed run never updates the
    /// persisted value, so a prior good corpus root survives a later failed attempt. A <see langword="null"/>
    /// <see cref="_corpusKnowledgeDirectory"/> (no real composition root configured it) surfaces as an
    /// explicit error rather than silently doing nothing.
    /// </summary>
    private async Task RunCorpusMiningAsync()
    {
        if (_corpusKnowledgeDirectory is null)
        {
            return;
        }

        // Build-plan Phase 17 §2a: exclude this app's own prior project directories from corpus mining,
        // even if a generated .xsq was later copied/moved outside its project's own exports/ folder.
        // A failure to enumerate recent projects must never block corpus mining itself -- it only makes
        // this one exclusion rule a no-op for this run.
        IReadOnlyList<string> excludedProjectRootDirectories;
        try
        {
            var recentProjects = await _projectService.RecentAsync(CancellationToken.None).ConfigureAwait(true);
            excludedProjectRootDirectories = recentProjects.Select(p => p.RootDirectory).ToList();
        }
        catch (Exception)
        {
            excludedProjectRootDirectories = [];
        }

        var succeeded = await ShowImport.RunCorpusMiningAsync(_corpusKnowledgeDirectory, _aiRoleClientResolver, excludedProjectRootDirectories).ConfigureAwait(true);
        if (succeeded)
        {
            var existing = _settingsStore.Load();
            _settingsStore.Save(existing with { SchemaVersion = AppSettings.CurrentSchemaVersion, CorpusRootDirectory = ShowImport.CorpusRootDirectory });
        }
    }

    /// <summary>
    /// Runs <see cref="ShowImportViewModel.GenerateWholeSongPlanAsync"/>; on success, also opens the
    /// Piano Roll/Structure screens against the exact same real
    /// <see cref="ShowImportViewModel.LastWholeSongGeneration"/> run just produced — see
    /// docs/UI-CLEANUP-PLAN.md item 2 (Phase 2a) for why this was previously impossible (the
    /// pipeline computed this melody/phrase/motif analysis internally but never returned it).
    /// Tries <see cref="PianoRollViewModel.OpenTrackAsync"/> before
    /// <see cref="PianoRollViewModel.BeginEditingAsync"/> so a melody id that *does* already have an
    /// open session (never discarding its corrections) resumes rather than being rejected by the
    /// real editor's own "a session already exists" guard — in practice each analysis run mints a
    /// fresh <see cref="MelodyTrack.Id"/>, so today this always begins a brand-new session; the
    /// resume path is defensive, not yet reachable through ordinary re-generation (see
    /// <c>PianoRollStructureDisplayPathsWiringTests</c> for the disclosed, empirically-confirmed
    /// reasoning).
    /// </summary>
    private async Task RunGenerateWholeSongPlanAsync()
    {
        // Build-plan Phase 14 §2c: reads whatever is currently typed on the CustomPrompt screen --
        // not gated on an explicit Save click, since that screen is a "last stop before you generate"
        // waypoint, not a save-gated settings form (see CustomPromptEditorViewModel's own doc comment).
        // An empty/blank prompt here is CustomGenerationPrompt.Empty's own IsEmpty case, which every
        // downstream builder treats identically to the pre-existing no-custom-prompt default.
        var customPrompt = new CustomGenerationPrompt(CustomPrompt.PromptText, CustomPrompt.StayCloseToExemplars, CustomPrompt.SelectedStyle, CustomPrompt.EnableQualityPass);
        var succeeded = await ShowImport.GenerateWholeSongPlanAsync(
            _wholeSongGenerator, Audio.ProbedAsset, _sequenceDocumentStore, ct: default, customPrompt: customPrompt,
            logger: _loggerFactory.CreateLogger<ShowImportViewModel>()).ConfigureAwait(true);
        if (!succeeded || ShowImport.LastWholeSongGeneration is not { } generation)
        {
            return;
        }

        var resumed = await PianoRoll.OpenTrackAsync(_noteTrackEditor, generation.Melody.Id).ConfigureAwait(true);
        if (!resumed)
        {
            await PianoRoll.BeginEditingAsync(_noteTrackEditor, generation.Melody).ConfigureAwait(true);
        }

        await StructureInspector.OpenAsync(
            _motifLinkOverrideStore, generation.SongId, generation.Phrases, generation.Motifs, templateAnalysis: null).ConfigureAwait(true);
    }

    /// <summary>
    /// Default <see cref="GeometryPolicy"/> for <see cref="RunOpenDisplayPathEditorAsync"/> — not yet
    /// user-configurable; <c>0.3</c>/<c>true</c> matches the value exercised by this project's own
    /// Display tests (<c>Step060AcceptanceTests</c>) as a reasonable starting default.
    /// </summary>
    private static readonly GeometryPolicy DefaultDisplayGeometryPolicy = new(MaxNeighborDistance: 0.3, RespectGroups: true);

    /// <summary>
    /// Builds a <see cref="SpatialDisplayGraph"/> from the already-imported
    /// <see cref="ShowImportViewModel.Layout"/> and opens it on <see cref="DisplayPathEditor"/> — the
    /// <see cref="OpenDisplayPathEditorCommand"/> action.
    /// </summary>
    private async Task RunOpenDisplayPathEditorAsync()
    {
        if (ShowImport.Layout is not { } layout)
        {
            return;
        }

        var graph = _displayGraphBuilder.Build(layout, DefaultDisplayGeometryPolicy);
        await DisplayPathEditor.OpenAsync(_displayLayoutEditor, graph).ConfigureAwait(true);
    }

    /// <summary>Parses <see cref="DisplayPathEditorViewModel.PendingPositionXText"/>/<see cref="DisplayPathEditorViewModel.PendingPositionYText"/> and applies them via <see cref="CorrectDisplayNodePositionCommand"/>.</summary>
    private async Task RunCorrectDisplayNodePositionAsync()
    {
        if (!double.TryParse(DisplayPathEditor.PendingPositionXText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(DisplayPathEditor.PendingPositionYText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
        {
            return;
        }

        await DisplayPathEditor.CorrectSelectedNodePositionAsync(_displayLayoutEditor, x, y).ConfigureAwait(true);
    }

    /// <summary>Parses <see cref="PianoRollViewModel.PendingMidiNoteText"/>/<see cref="PianoRollViewModel.PendingStartTicksText"/>/<see cref="PianoRollViewModel.PendingEndTicksText"/> and applies them via <see cref="CorrectSelectedNoteCommand"/>.</summary>
    private async Task RunCorrectSelectedNoteAsync()
    {
        if (!int.TryParse(PianoRoll.PendingMidiNoteText, out var midiNote)
            || !long.TryParse(PianoRoll.PendingStartTicksText, out var startTicks)
            || !long.TryParse(PianoRoll.PendingEndTicksText, out var endTicks))
        {
            return;
        }

        await PianoRoll.CorrectSelectedNoteAsync(_noteTrackEditor, midiNote, startTicks, endTicks).ConfigureAwait(true);
    }

    private async Task RunUnlinkSelectedPhraseAsync()
    {
        if (StructureInspector.SelectedPhraseId is not { } phraseId)
        {
            return;
        }

        await StructureInspector.UnlinkPhraseAsync(_motifLinkOverrideStore, phraseId).ConfigureAwait(true);
    }

    private async Task RunLinkSelectedPhraseAsync()
    {
        if (StructureInspector.SelectedPhraseId is not { } phraseAId || StructureInspector.PendingLinkTargetPhraseId is not { } phraseBId)
        {
            return;
        }

        await StructureInspector.LinkPhrasesAsync(_motifLinkOverrideStore, phraseAId, phraseBId).ConfigureAwait(true);
    }

    /// <summary>
    /// Runs <see cref="FirstRunViewModel.CompleteFirstRunAsync"/> against the real Step 094
    /// <see cref="_runtimeCapabilityDetector"/>/<see cref="_startupHealthChecks"/> collaborators;
    /// on success, persists the chosen workspace folder and marks first-run complete (Step 095's
    /// own "saved-state verification" requirement — a restart must never show this wizard again for
    /// the same installation), then returns to Welcome. A failed attempt (invalid/admin-only folder,
    /// cancellation, I/O error) never touches persisted settings and leaves the wizard on screen so
    /// the user can correct it — same "no mutation on failure" convention as
    /// <see cref="RunDiscoverShowDirectoryAsync"/>.
    /// </summary>
    private async Task RunCompleteFirstRunAsync()
    {
        var succeeded = await FirstRun.CompleteFirstRunAsync(_runtimeCapabilityDetector, _startupHealthChecks).ConfigureAwait(true);
        if (succeeded)
        {
            var existing = _settingsStore.Load();
            _settingsStore.Save(existing with
            {
                SchemaVersion = AppSettings.CurrentSchemaVersion,
                WorkspaceDirectory = FirstRun.WorkspaceDirectory,
                HasCompletedFirstRun = true,
            });
            _navigator.NavigateTo(ShellScreen.Welcome);
        }
    }

    /// <summary>
    /// PHASE-15 Step 096 ("Add opt-in model asset installs, offline validation, logs and update
    /// notices"): installs <see cref="_optionalModelAsset"/> (if any) through the real
    /// <see cref="FirstRunViewModel.InstallOptionalAssetAsync"/>/<see cref="_assetInstaller"/>
    /// pipeline, then records the real, human-readable outcome (success, declined consent, no asset
    /// declared yet, or a recoverable failure) to <see cref="_diagnostics"/> — this step's own
    /// "...logs..." requirement, verbatim, realized by reusing the existing <see cref="IAppDiagnostics"/>
    /// sink rather than a second, parallel logging mechanism.
    /// </summary>
    private async Task RunInstallOptionalAssetAsync()
    {
        var succeeded = await FirstRun
            .InstallOptionalAssetAsync(_optionalModelAsset, FirstRun.OptionalAssetConsent, _assetInstaller)
            .ConfigureAwait(true);

        if (FirstRun.OptionalAssetMessage is { } message)
        {
            _diagnostics.RecordEvent(
                succeeded ? DiagnosticEventLevel.Info : DiagnosticEventLevel.Warning,
                "optional-asset-install",
                message);
        }
    }

    /// <summary>
    /// PHASE-04 Step 028: validates <see cref="TimelineViewModel.ScrubTargetTicksText"/> against the
    /// loaded timeline's own content bounds <em>before</em> touching the real transport, then seeks
    /// the one shared <see cref="_masterClock"/> and pushes its resulting
    /// <see cref="MasterPlaybackClock.CanonicalPositionTicks"/> into
    /// <see cref="TimelineViewModel.PlayheadTicks"/>. Any failure (an unparsable value, an
    /// out-of-scope target, or no playback session currently open — the "stale" case) sets
    /// <see cref="TimelineViewModel.ScrubErrorMessage"/> and leaves <see cref="TimelineViewModel.PlayheadTicks"/>
    /// completely untouched — never a partial mutation — same "catch, set an error property, never
    /// let an exception reach the UI" convention every other screen action in this project already
    /// follows.
    /// </summary>
    private async Task RunScrubTimelinePlayheadAsync()
    {
        if (!long.TryParse(Timeline.ScrubTargetTicksText, out var requestedTicks))
        {
            Timeline.SetScrubError("Scrub target must be a whole number of 100 ns ticks.");
            return;
        }

        // PHASE-04 Step 030 ("Add frame/beat/off-grid snapping..."): the real scrub/drag entry point
        // this step's own "snap policy... as dependency not baked into drag math" implementation
        // guidance targets — Timeline.SnapPolicy is an injected dependency (defaults to
        // Editing.SnapPolicy.None, so pre-Step-030 scrub behavior is unchanged), applied here, once,
        // *before* validation/seek, never a second hardcoded rounding call elsewhere.
        var ticks = Timeline.SnapPolicy.Snap(requestedTicks);

        try
        {
            Timeline.ValidateScrubTarget(ticks);
            await _masterClock.SeekToCanonicalAsync(ticks, CancellationToken.None).ConfigureAwait(true);

            // Refresh the Audio screen's own bindable position too, from the exact same transport
            // this scrub just moved — its PropertyChanged handler (above) then pushes the identical
            // MasterPlaybackClock reading into Timeline.PlayheadTicks, so both screens observe the
            // one real seek result rather than two independently-computed values.
            Audio.RefreshPlaybackPosition(_audioTransport);
            Timeline.SetPlayhead(_masterClock.CanonicalPositionTicks, Audio.IsPlaybackOpen);
            Timeline.SetScrubError(null);

            // This step's own "gather sync/drift telemetry" (implementation guidance): the real,
            // observed difference between the already-snapped requested tick and what the device
            // actually ended up reporting afterward (real sample-rate quantization, never an estimate)
            // — recorded only on success, so a rejected/out-of-scope scrub never overwrites the last
            // real telemetry with a value that was never actually sent to the transport.
            Timeline.RecordScrubTelemetry(ticks, _masterClock.CanonicalPositionTicks);
        }
        catch (Exception ex)
        {
            Timeline.SetScrubError(ex.Message);
        }
    }

    /// <summary>
    /// PHASE-04 Step 029 ("A/B loop playback"): arms the real shared transport's loop window from
    /// <see cref="TimelineViewModel.SelectedRangeScope"/>'s own time range. Rejects, before ever
    /// touching the real transport, both named failure/edge cases: **"stale"** — no playback session
    /// is currently open (<c>_audioTransport.CurrentSong</c> is <see langword="null"/>), or nothing is
    /// selected at all; **"out-of-scope"** — the selected window falls outside the loaded timeline's
    /// own content bounds (<see cref="TimelineViewModel.ValidateLoopWindow"/>) or, independently,
    /// outside the open song's own duration (the real transport's own <c>IAudioTransport.SetLoop</c>
    /// validation). Any rejection sets <see cref="TimelineViewModel.LoopErrorMessage"/> and leaves
    /// <see cref="TimelineViewModel.LoopWindow"/> and the real transport's own loop state completely
    /// untouched — never a partial mutation, same convention as <see cref="RunScrubTimelinePlayheadAsync"/>.
    /// </summary>
    private void RunSetLoopFromSelection()
    {
        if (Timeline.SelectedRangeScope is not { } scope)
        {
            Timeline.SetLoopError("Select a time range before setting a loop.");
            return;
        }

        if (_audioTransport.CurrentSong is null)
        {
            Timeline.SetLoopError("No playback session open. Open Playback on the Audio screen first.");
            return;
        }

        try
        {
            Timeline.ValidateLoopWindow(scope.Time);
            _audioTransport.SetLoop(scope.Time);
            Timeline.SetLoop(scope.Time);
            Timeline.SetLoopError(null);
        }
        catch (Exception ex)
        {
            Timeline.SetLoopError(ex.Message);
        }
    }

    /// <summary>Clears the real shared transport's loop window (never fails) and mirrors the clear into <see cref="TimelineViewModel.LoopWindow"/>.</summary>
    private void RunClearLoop()
    {
        _audioTransport.SetLoop(null);
        Timeline.SetLoop(null);
        Timeline.SetLoopError(null);
    }

    private void OnPropertyChangedAll()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Failure));
        OnPropertyChanged(nameof(CurrentScreen));
        OnPropertyChanged(nameof(CurrentProject));
        OnPropertyChanged(nameof(HasCurrentProject));
        OnPropertyChanged(nameof(IsStarting));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsShowingWelcome));
        OnPropertyChanged(nameof(IsShowingProjects));
        OnPropertyChanged(nameof(IsShowingSettings));
        OnPropertyChanged(nameof(IsShowingDiagnostics));
        OnPropertyChanged(nameof(IsShowingExemplarLibrary));
        OnPropertyChanged(nameof(IsShowingShowImport));
        OnPropertyChanged(nameof(IsShowingCustomPrompt));
        OnPropertyChanged(nameof(IsShowingGenerateExport));
        OnPropertyChanged(nameof(IsShowingTimeline));
        OnPropertyChanged(nameof(IsShowingAudio));
        OnPropertyChanged(nameof(IsShowingPianoRoll));
        OnPropertyChanged(nameof(IsShowingStructureInspector));
        OnPropertyChanged(nameof(IsShowingDisplayPathEditor));
        OnPropertyChanged(nameof(IsShowingFirstRun));
    }
}
