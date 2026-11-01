using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using XlightsAI.Studio.App.Views;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Projects;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Shell;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.App;

public partial class App : Application
{
    /// <summary>
    /// Set by <see cref="Program.Main"/> before <c>StartWithClassicDesktopLifetime</c> runs, so
    /// crash handlers registered there and the shell's own diagnostics/settings share one
    /// instance. Falls back to no-op implementations if unset (e.g. a design-time instantiation),
    /// never null-reference-crashes the composition root.
    /// </summary>
    public static IAppDiagnostics? Diagnostics { get; set; }

    public static ISettingsStore? SettingsStore { get; set; }

    /// <summary>
    /// Set by <see cref="Program.Main"/>, same hand-off pattern as <see cref="Diagnostics"/>/
    /// <see cref="SettingsStore"/>. Falls back to <see cref="NullProjectService"/> if unset.
    /// </summary>
    public static IProjectService? ProjectService { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="NullShowDirectoryDiscoveryService"/> if unset.</summary>
    public static IShowDirectoryDiscoveryService? ShowDirectoryDiscoveryService { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to a real <see cref="XlightsAI.Studio.Xlights.ShowLayoutImporter"/> if unset (see <see cref="ShellViewModel"/>'s own constructor note on why this one collaborator does not use a throwing Null-object fallback).</summary>
    public static IShowLayoutImporter? ShowLayoutImporter { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="NullSequenceDocumentStore"/> if unset (PHASE-02 Step 012 — needs a real configured output directory, same reasoning as <see cref="ProjectService"/>/<see cref="ShowDirectoryDiscoveryService"/>).</summary>
    public static ISequenceDocumentStore? SequenceDocumentStore { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to a real <see cref="WavAudioDecoder"/> if unset (PHASE-04 Step 024 — pure, stateless domain logic, same reasoning as <see cref="ShowLayoutImporter"/>).</summary>
    public static IAudioDecoder? AudioDecoder { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="NullSongAssetStore"/> if unset (PHASE-04 Step 024 — needs a real configured output directory, same reasoning as <see cref="SequenceDocumentStore"/>).</summary>
    public static ISongAssetStore? SongAssetStore { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to a real <see cref="WaveOutAudioTransport"/> if unset (PHASE-04 Step 025 — pure, stateless-at-construction collaborator, same reasoning as <see cref="AudioDecoder"/>).</summary>
    public static IAudioTransport? AudioTransport { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="NullWaveformPyramidStore"/> if unset (PHASE-04 Step 026 — needs a real configured output directory, same reasoning as <see cref="SongAssetStore"/>).</summary>
    public static IWaveformPyramidStore? WaveformPyramidStore { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to a real <see cref="RuntimeCapabilityDetector"/> if unset (PHASE-15 Step 095 — pure, stateless-at-construction collaborator, same reasoning as <see cref="AudioDecoder"/>).</summary>
    public static IRuntimeCapabilityDetector? RuntimeCapabilityDetector { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="NullFfmpegInstaller"/> if unset (needs a real configured install directory, same reasoning as <see cref="SongAssetStore"/>). Backs the Audio screen's "Download ffmpeg" action; independent of whichever <see cref="AudioDecoder"/> is wired.</summary>
    public static IFfmpegInstaller? FfmpegInstaller { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="NullWholeSongGenerator"/> if unset (needs real analysis-cache directories/a real stem-separation model this layer does not yet own). Backs the Show Import screen's "Generate Whole-Song Plan" action.</summary>
    public static IWholeSongGenerator? WholeSongGenerator { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="NullModelAssetManager"/> if unset. Backs the Show Import screen's "Download separation model" action; independent of whichever <see cref="WholeSongGenerator"/> is wired.</summary>
    public static IModelAssetManager? StemModelAssetManager { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="XlightsAI.Studio.Melody.NullNoteTrackEditor"/> if unset (needs a real melody-revision-store directory this layer does not yet own). Backs the Piano Roll screen.</summary>
    public static XlightsAI.Studio.Melody.INoteTrackEditor? NoteTrackEditor { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="XlightsAI.Studio.Structure.NullMotifLinkOverrideStore"/> if unset. Backs the Structure screen.</summary>
    public static XlightsAI.Studio.Structure.IMotifLinkOverrideStore? MotifLinkOverrideStore { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; falls back to <see cref="XlightsAI.Studio.Display.NullDisplayLayoutEditor"/> if unset. Backs the Display Paths screen.</summary>
    public static XlightsAI.Studio.Display.IDisplayLayoutEditor? DisplayLayoutEditor { get; set; }

    /// <summary>
    /// Set by <see cref="Program.Main"/>; falls back to <see cref="XlightsAI.Studio.Xlights.XsqExporter"/>'s
    /// own parameterless constructor if unset -- which only knows <c>EffectRecipeCatalog.CreateDefault()</c>'s
    /// single hardcoded "On" recipe (id <c>effect.on.v1</c>), not the generated-vocabulary recipe ids
    /// (e.g. <c>effect.generated.on.v1</c>) <see cref="WholeSongGenerator"/> actually produces. Confirmed
    /// as a real bug against a real export: "Generate Whole-Song Plan" and "Export sequence" used to
    /// silently disagree on which recipe catalog is authoritative, so export rejected every effect the
    /// generator had just produced. Backs the Generate & Export screen's "Export sequence" action.
    /// </summary>
    public static XlightsAI.Studio.Xlights.IXsqExporter? XsqExporter { get; set; }

    /// <summary>
    /// Set by <see cref="Program.Main"/>; build-plan Phase 10 §3f ("Activate corpus mining") -- the
    /// directory the Generate &amp; Export screen's Developer Tools "Run corpus mining" action persists
    /// <see cref="XlightsAI.Studio.Pipeline.CraftRules"/>/<see cref="XlightsAI.Studio.Pipeline.ExemplarIndex"/>
    /// under, same one <see cref="WholeSongGenerator"/> (when it is a real <c>AiChoreographyWholeSongGenerator</c>)
    /// reads back on every generation. Falls back to <see langword="null"/> if unset -- the Developer
    /// Tools action then stays disabled, same "needs a real configured directory this layer does not
    /// yet own" reasoning as <see cref="SequenceDocumentStore"/>/<see cref="SongAssetStore"/>, rather
    /// than guessing a directory no composition root actually confirmed.
    /// </summary>
    public static string? CorpusKnowledgeDirectory { get; set; }

    /// <summary>Set by <see cref="Program.Main"/>; build-plan Phase 14 §2a.2 -- backs the "Creative direction" screen's persisted custom prompt. Falls back to <see cref="XlightsAI.Studio.Shell.NullCustomPromptStore"/> if unset.</summary>
    public static XlightsAI.Studio.Director.ICustomPromptStore? CustomPromptStore { get; set; }

    /// <summary>
    /// Set by <see cref="Program.Main"/>; build-plan Phase 16 §2b -- the same role-client resolver
    /// <c>AiChoreographyWholeSongGenerator</c> uses, reused here so "Run corpus mining" can also generate
    /// real exemplar descriptions via the Director role. Falls back to <see langword="null"/> if unset,
    /// in which case corpus mining still runs, just without description enrichment.
    /// </summary>
    public static XlightsAI.Studio.Director.AiRoleClientResolver? AiRoleClientResolver { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var diagnostics = Diagnostics ?? NullAppDiagnostics.Instance;
            var settingsStore = SettingsStore ?? NullSettingsStore.Instance;
            var projectService = ProjectService ?? NullProjectService.Instance;
            var showDirectoryDiscoveryService = ShowDirectoryDiscoveryService ?? NullShowDirectoryDiscoveryService.Instance;
            var showLayoutImporter = ShowLayoutImporter ?? new XlightsAI.Studio.Xlights.ShowLayoutImporter();
            var sequenceDocumentStore = SequenceDocumentStore ?? NullSequenceDocumentStore.Instance;
            var audioDecoder = AudioDecoder ?? new WavAudioDecoder();
            var songAssetStore = SongAssetStore ?? NullSongAssetStore.Instance;
            var audioTransport = AudioTransport ?? CreateDefaultAudioTransport(audioDecoder);
            var waveformPyramidStore = WaveformPyramidStore ?? NullWaveformPyramidStore.Instance;
            var waveformBuilder = new WaveformPyramidBuilder(audioDecoder, waveformPyramidStore);
            var runtimeCapabilityDetector = RuntimeCapabilityDetector ?? new RuntimeCapabilityDetector();
            var ffmpegInstaller = FfmpegInstaller ?? NullFfmpegInstaller.Instance;
            var wholeSongGenerator = WholeSongGenerator ?? NullWholeSongGenerator.Instance;
            var stemModelAssetManager = StemModelAssetManager ?? NullModelAssetManager.Instance;
            var noteTrackEditor = NoteTrackEditor ?? XlightsAI.Studio.Melody.NullNoteTrackEditor.Instance;
            var motifLinkOverrideStore = MotifLinkOverrideStore ?? XlightsAI.Studio.Structure.NullMotifLinkOverrideStore.Instance;
            var displayLayoutEditor = DisplayLayoutEditor ?? XlightsAI.Studio.Display.NullDisplayLayoutEditor.Instance;
            var displayGraphBuilder = new XlightsAI.Studio.Display.DisplayGraphBuilder();
            var xsqExporter = XsqExporter;
            var customPromptStore = CustomPromptStore ?? XlightsAI.Studio.Shell.NullCustomPromptStore.Instance;
            var aiRoleClientResolver = AiRoleClientResolver;
            var navigator = new ShellNavigator();

            // ProjectsViewModel.Load still takes a synchronous Func<IReadOnlyList<string>> (its
            // Step 004 contract, unchanged); RecentAsync is async, so this adapter blocks on it —
            // the same "this layer's persistence is synchronous" scoping decision Step 005 already
            // made for ISettingsStore/IAppDiagnostics, applied to the one place Step 006's async
            // IProjectService meets that existing synchronous screen contract.
            IReadOnlyList<string> ProjectNamesSource() =>
                projectService.RecentAsync(CancellationToken.None).GetAwaiter().GetResult()
                    .Select(summary => summary.Name)
                    .ToArray();

            var shellViewModel = new ShellViewModel(
                navigator,
                ProjectNamesSource,
                settingsStore,
                diagnostics,
                projectService,
                showDirectoryDiscoveryService,
                showLayoutImporter,
                sequenceDocumentStore: sequenceDocumentStore,
                audioDecoder: audioDecoder,
                songAssetStore: songAssetStore,
                audioTransport: audioTransport,
                waveformPyramidStore: waveformPyramidStore,
                waveformBuilder: waveformBuilder,
                runtimeCapabilityDetector: runtimeCapabilityDetector,
                ffmpegInstaller: ffmpegInstaller,
                wholeSongGenerator: wholeSongGenerator,
                stemModelAssetManager: stemModelAssetManager,
                noteTrackEditor: noteTrackEditor,
                motifLinkOverrideStore: motifLinkOverrideStore,
                displayLayoutEditor: displayLayoutEditor,
                displayGraphBuilder: displayGraphBuilder,
                xsqExporter: xsqExporter,
                corpusKnowledgeDirectory: CorpusKnowledgeDirectory,
                customPromptStore: customPromptStore,
                aiRoleClientResolver: aiRoleClientResolver);
            shellViewModel.RunStartup();

            desktop.MainWindow = new MainWindow(shellViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Windows uses winmm and macOS uses OpenAL; any other platform gets the typed "unsupported" stub (see <see cref="AudioTransportFactory"/>).</summary>
    private static IAudioTransport CreateDefaultAudioTransport(IAudioDecoder audioDecoder) =>
        AudioTransportFactory.Create(audioDecoder);
}
