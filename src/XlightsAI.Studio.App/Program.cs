using Avalonia;
using Velopack;
using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Projects;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Shell;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.App;

public static class Program
{
    /// <summary>
    /// Startup errors before Avalonia itself is up (e.g. a corrupt Avalonia/graphics backend
    /// install) must be reported, not swallowed by a silent process exit. Crash handlers are
    /// registered before <see cref="BuildAvaloniaApp"/> runs so even a composition-root crash is
    /// captured by diagnostics, not just UI-level failures.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        // Must run first: handles Velopack's install/update/uninstall hook invocations and exits.
        VelopackApp.Build().Run();

        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XlightsAI.Studio");

        var diagnostics = new AppDiagnostics(Path.Combine(appDataDirectory, "logs"));
        var settingsStore = new SettingsStore(appDataDirectory, diagnostics);
        var projectStore = new JsonProjectStore();
        var recentProjectsStore = new RecentProjectsStore(appDataDirectory);
        var projectService = new ProjectService(projectStore, recentProjectsStore);
        var showDirectoryDiscoveryService = new ShowDirectoryDiscoveryService();
        var showLayoutImporter = new ShowLayoutImporter();
        var sequenceDocumentStore = new JsonSequenceDocumentStore(Path.Combine(appDataDirectory, "exports"));

        // ffmpeg is an optional, explicitly consent-gated download (the Audio screen's "Download
        // ffmpeg" action, never invoked implicitly) that lets TranscodingAudioDecoder extend plain-WAV
        // probing to non-WAV containers (MP3, etc.) -- see FfmpegAsset's own doc comment for the
        // pinned version/license this build downloads.
        var downloadsDirectory = Path.Combine(appDataDirectory, "downloads");
        var releaseAssetDownloader = new HttpReleaseAssetDownloader();
        var assetInstaller = new AssetInstaller(downloadsDirectory, releaseAssetDownloader);
        // Windows downloads the pinned build; other platforms use an ffmpeg the user already installed (e.g. Homebrew).
        IFfmpegInstaller ffmpegInstaller = OperatingSystem.IsWindows()
            ? new FfmpegInstaller(Path.Combine(appDataDirectory, "tools", "ffmpeg"), assetInstaller)
            : new SystemFfmpegInstaller();
        var audioDecoder = new TranscodingAudioDecoder(
            new WavAudioDecoder(), ffmpegInstaller, Path.Combine(appDataDirectory, "audio-transcode-cache"));
        var songAssetStore = new JsonSongAssetStore(Path.Combine(appDataDirectory, "audio-assets"));
        var waveformPyramidStore = new JsonWaveformPyramidStore(Path.Combine(appDataDirectory, "waveform-cache"));

        // Real whole-song AI pipeline (M1-M4): the vocal/instrument separation model is a second,
        // separate optional consent-gated download (the Show Import screen's "Download separation
        // model" action) -- see SherpaOnnxSpleeterModelAsset's own doc comment for the pinned
        // version/license (and disclosed Spleeter-weights license caveat) this build downloads.
        var stemModelAssetManager = new ModelAssetManager(
            Path.Combine(appDataDirectory, "tools", "stem-separation-model"), new HttpModelAssetDownloader());
        var stemSeparator = new SpectrogramStemSeparatorAdapter(
            new SpectrogramOnnxStemSeparator(
                stemModelAssetManager, new TarBzip2ModelArchiveExpander(), new WavStereoAudioDecoder(),
                Path.Combine(appDataDirectory, "stem-separation-output")),
            SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe);
        var audioAnalysisRunner = new AudioAnalysisRunner(
            audioDecoder, stemSeparator, SherpaOnnxSpleeterModelAsset.Archive,
            new JsonOnsetAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "onsets")),
            new JsonTempoAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "tempo")),
            new JsonBeatGridStore(Path.Combine(appDataDirectory, "analysis-cache", "beatgrid")),
            new JsonEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "energy")),
            new JsonPitchTrackStore(Path.Combine(appDataDirectory, "analysis-cache", "pitch")),
            new JsonStemActivityResultStore(Path.Combine(appDataDirectory, "analysis-cache", "vocal-activity")),
            new JsonBandEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "band-energy")));
        var structureAnalysisRunner = new StructureAnalysisRunner(
            new JsonBarFingerprintStore(Path.Combine(appDataDirectory, "analysis-cache", "fingerprints")),
            new JsonSectionBoundaryStore(Path.Combine(appDataDirectory, "analysis-cache", "boundaries")),
            new JsonSectionLabelStore(Path.Combine(appDataDirectory, "analysis-cache", "labels")),
            new JsonSectionLabelOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "label-overrides")),
            new JsonPhraseSegmentationStore(Path.Combine(appDataDirectory, "analysis-cache", "phrases")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "phrase-overrides")),
            new JsonMotifClusteringStore(Path.Combine(appDataDirectory, "analysis-cache", "motifs")));
        var wholeSongGenerator = new WholeSongGenerator(audioAnalysisRunner, structureAnalysisRunner);

        // Build-plan Phases 5-8: the real LLM-driven choreography pipeline, replacing the old M1-M4
        // flat-"On" path as the production default (WholeSongGenerator above is kept, unused, so the old
        // path remains available rather than deleted). Reuses the exact %AppData%/ai-providers and
        // %AppData%/credentials paths AiProviderSettingsView's own code-behind already uses -- the same
        // Settings screen the user configures the Director/Choreographer roles on is what this reads
        // from, not a second, parallel config location.
        var roleProviderConfigStore = new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers"));
        var credentialStore = CredentialStoreFactory.Create(Path.Combine(appDataDirectory, "credentials"));
        // HttpClient.Timeout defaults to 100s and governs the whole request regardless of any
        // CancellationToken passed to SendAsync, overriding RoleProviderConfig.RequestTimeoutSeconds
        // entirely -- confirmed against a real local Ollama model, whose chat-completion calls routinely
        // exceed 100s. Left unbounded here so RequestTimeoutSeconds/HttpJsonRequestExecutor's own
        // per-attempt timeout is the only real timeout in effect, as the config value is meant to be.
        var aiHttpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var aiRoleClientResolver = new AiRoleClientResolver(roleProviderConfigStore, credentialStore, aiHttpClient);

        // No existing Shell setting names a configured xLights install directory (confirmed by prior
        // research across AppSettings/XlightsRenderCapabilityProbe/ShowLayoutImporter) -- this falls back
        // to the same last-resort path EmissionStrategySelector's own doc comment already documents.
        var effectMetadataDirectory = @"C:\Program Files\xLights\effectmetadata";
        // Confirmed as a real, significant bug: this composition root never passed a style, so every
        // real app generation silently used AiChoreographyWholeSongGenerator's own conservative default
        // (Traditional/Classic -- low density, a handful of calm effects) while every verification this
        // session ran through GenerateOnce explicitly, deliberately used High-Energy/EDM -- the real app
        // and every "it works now" test result were never actually the same configuration. No style-
        // selection UI exists yet (a disclosed scope boundary), so this is still a hardcoded default, but
        // now the SAME one everything in this session has actually been verified against.
        // Build-plan Phase 10 §3f ("Activate corpus mining"): the same directory
        // ShellViewModel's RunCorpusMiningCommand (Generate & Export screen's Developer Tools) persists
        // CorpusMiningRunner's CraftRules/ExemplarIndex/PatternLibrary under, so a real corpus-mining
        // run and every subsequent real generation agree on where that knowledge actually lives.
        var corpusKnowledgeDirectory = Path.Combine(appDataDirectory, "corpus-knowledge");
        var aiChoreographyWholeSongGenerator = new AiChoreographyWholeSongGenerator(
            audioAnalysisRunner, structureAnalysisRunner, aiRoleClientResolver,
            effectMetadataDirectory, Path.Combine(appDataDirectory, "ai-choreography"),
            knowledgeDirectory: corpusKnowledgeDirectory,
            style: XlightsAI.Studio.Director.StylePresetCatalog.HighEnergyEdmId);

        // Build-plan Phase 14 §2a.2: same directory AiChoreographyWholeSongGenerator's own
        // projectDirectory above uses (bible/scripts/llm/exports) -- the persisted custom prompt
        // sits as a sibling of those, round-tripping across sessions.
        var customPromptStore = new XlightsAI.Studio.Director.JsonCustomPromptStore(Path.Combine(appDataDirectory, "ai-choreography"));

        // Confirmed as a real bug against a real export: ShellViewModel's own default XsqExporter()
        // (parameterless) only knows EffectRecipeCatalog.CreateDefault()'s single hardcoded "On" recipe
        // (id effect.on.v1) -- it rejected every generated-vocabulary recipe id (effect.generated.*)
        // AiChoreographyWholeSongGenerator actually produces, since those two components silently used
        // different recipe catalogs. Built from the exact same effectMetadataDirectory the generator
        // itself uses, so generation and export are never out of sync again.
        var exportCatalogs = EffectCatalogFactory.BuildFromMetadataDirectory(effectMetadataDirectory);
        var xsqExporter = new XsqExporter(exportCatalogs.Recipes, exportCatalogs.Vocabulary);

        // Backs the Piano Roll/Structure/Display Paths screens (docs/UI-CLEANUP-PLAN.md item 2,
        // Phase 2a) — same "needs a real directory this layer does not yet own" reasoning as every
        // other JSON-backed store above.
        var noteTrackEditor = new NoteTrackEditor(new JsonMelodyTrackRevisionStore(Path.Combine(appDataDirectory, "melody-revisions")));
        var motifLinkOverrideStore = new JsonMotifLinkOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "motif-link-overrides"));
        var displayLayoutEditor = new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(Path.Combine(appDataDirectory, "display-layout-corrections")));
        var displayGraphBuilder = new DisplayGraphBuilder();

        // Windows uses winmm, macOS uses OpenAL; any other platform gets UnsupportedPlatformAudioTransport,
        // which throws one clear, typed message on first use.
        IAudioTransport audioTransport = AudioTransportFactory.Create(audioDecoder);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                diagnostics.RecordFailure("unhandled-exception", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            diagnostics.RecordFailure("unobserved-task-exception", e.Exception);
            e.SetObserved();
        };

        App.Diagnostics = diagnostics;
        App.SettingsStore = settingsStore;
        App.ProjectService = projectService;
        App.ShowDirectoryDiscoveryService = showDirectoryDiscoveryService;
        App.ShowLayoutImporter = showLayoutImporter;
        App.SequenceDocumentStore = sequenceDocumentStore;
        App.AudioDecoder = audioDecoder;
        App.SongAssetStore = songAssetStore;
        App.AudioTransport = audioTransport;
        App.WaveformPyramidStore = waveformPyramidStore;
        App.FfmpegInstaller = ffmpegInstaller;
        App.WholeSongGenerator = aiChoreographyWholeSongGenerator;
        App.CorpusKnowledgeDirectory = corpusKnowledgeDirectory;
        App.AiRoleClientResolver = aiRoleClientResolver;
        App.CustomPromptStore = customPromptStore;
        App.XsqExporter = xsqExporter;
        App.StemModelAssetManager = stemModelAssetManager;
        App.NoteTrackEditor = noteTrackEditor;
        App.MotifLinkOverrideStore = motifLinkOverrideStore;
        App.DisplayLayoutEditor = displayLayoutEditor;

        try
        {
            _ = Task.Run(() => AppUpdater.CheckAndStageAsync(diagnostics));
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            diagnostics.RecordFailure("startup-crash", ex);
            Console.Error.WriteLine($"Fatal startup error: {ex}");
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
