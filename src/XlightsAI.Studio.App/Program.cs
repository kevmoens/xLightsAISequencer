using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
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
        var logDirectory = Path.Combine(appDataDirectory, "logs");

        var services = new ServiceCollection();
        RegisterLogging(services, logDirectory);
        RegisterServices(services, appDataDirectory);

        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("XlightsAI Studio starting up. Logs are written to {LogDirectory}.", logDirectory);

        var diagnostics = provider.GetRequiredService<AppDiagnostics>();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.LogCritical(ex, "Unhandled exception reached AppDomain.CurrentDomain.UnhandledException.");
                diagnostics.RecordFailure("unhandled-exception", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "An unobserved Task exception was raised.");
            diagnostics.RecordFailure("unobserved-task-exception", e.Exception);
            e.SetObserved();
        };

        App.Diagnostics = diagnostics;
        App.SettingsStore = provider.GetRequiredService<SettingsStore>();
        App.ProjectService = provider.GetRequiredService<ProjectService>();
        App.ShowDirectoryDiscoveryService = provider.GetRequiredService<ShowDirectoryDiscoveryService>();
        App.ShowLayoutImporter = provider.GetRequiredService<ShowLayoutImporter>();
        App.SequenceDocumentStore = provider.GetRequiredService<JsonSequenceDocumentStore>();
        App.AudioDecoder = provider.GetRequiredService<TranscodingAudioDecoder>();
        App.SongAssetStore = provider.GetRequiredService<JsonSongAssetStore>();
        App.AudioTransport = provider.GetRequiredService<IAudioTransport>();
        App.WaveformPyramidStore = provider.GetRequiredService<JsonWaveformPyramidStore>();
        App.FfmpegInstaller = provider.GetRequiredService<IFfmpegInstaller>();
        App.WholeSongGenerator = provider.GetRequiredService<AiChoreographyWholeSongGenerator>();
        App.CorpusKnowledgeDirectory = provider.GetRequiredService<CorpusKnowledgeDirectory>().Value;
        App.AiRoleClientResolver = provider.GetRequiredService<AiRoleClientResolver>();
        App.CustomPromptStore = provider.GetRequiredService<JsonCustomPromptStore>();
        App.XsqExporter = provider.GetRequiredService<XsqExporter>();
        App.StemModelAssetManager = provider.GetRequiredService<ModelAssetManagerRegistry>().StemSeparation;
        App.NoteTrackEditor = provider.GetRequiredService<NoteTrackEditor>();
        App.MotifLinkOverrideStore = provider.GetRequiredService<JsonMotifLinkOverrideStore>();
        App.DisplayLayoutEditor = provider.GetRequiredService<DisplayLayoutEditor>();
        App.Services = provider;

        try
        {
            _ = Task.Run(() => AppUpdater.CheckAndStageAsync(diagnostics));
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            logger.LogInformation("XlightsAI Studio shutting down normally.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal startup error -- the composition root or Avalonia lifetime threw before the app could run.");
            diagnostics.RecordFailure("startup-crash", ex);
            Console.Error.WriteLine($"Fatal startup error: {ex}");
            return 1;
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }

    /// <summary>
    /// Every class in this app that wants to log takes a plain <see cref="ILogger{T}"/> from
    /// Microsoft.Extensions.Logging -- the DI container supplies it. NLog is the one concrete
    /// provider plugged in here, writing EVERYTHING (down to Trace, which is where full LLM
    /// request/response bodies are logged) to one rolling file under
    /// <paramref name="logDirectory"/>, so a user who has never read the code can open that file
    /// and read, in order, what the app actually did.
    /// </summary>
    private static void RegisterLogging(IServiceCollection services, string logDirectory)
    {
        var nlogConfig = new NLog.Config.LoggingConfiguration();

        var fileTarget = new NLog.Targets.FileTarget("studio-log-file")
        {
            FileName = Path.Combine(logDirectory, "studio.log"),
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}",
            ArchiveFileName = Path.Combine(logDirectory, "studio.{#}.log"),
            ArchiveSuffixFormat = ".{0}",
            ArchiveAboveSize = 20_000_000,
            MaxArchiveFiles = 10,
            KeepFileOpen = true,
            AutoFlush = true,
        };

        // Wrapped async so a Trace-level flood of LLM request/response bodies never blocks the
        // calling thread (the pipeline/UI thread making the real call) on file I/O.
        var asyncTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(fileTarget)
        {
            OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Discard,
        };

        nlogConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, asyncTarget);
        NLog.LogManager.Configuration = nlogConfig;

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddNLog();
        });
    }

    private static void RegisterServices(IServiceCollection services, string appDataDirectory)
    {
        services.AddSingleton(sp => new AppDiagnostics(Path.Combine(appDataDirectory, "logs"), sp.GetRequiredService<ILogger<AppDiagnostics>>()));
        services.AddSingleton<IAppDiagnostics>(sp => sp.GetRequiredService<AppDiagnostics>());
        services.AddSingleton(sp => new SettingsStore(appDataDirectory, sp.GetRequiredService<IAppDiagnostics>()));
        services.AddSingleton<JsonProjectStore>();
        services.AddSingleton(_ => new RecentProjectsStore(appDataDirectory));
        services.AddSingleton<ProjectService>();
        services.AddSingleton<ShowDirectoryDiscoveryService>();
        services.AddSingleton<ShowLayoutImporter>();
        services.AddSingleton(_ => new JsonSequenceDocumentStore(Path.Combine(appDataDirectory, "exports")));

        // ffmpeg is an optional, explicitly consent-gated download (the Audio screen's "Download
        // ffmpeg" action, never invoked implicitly) that lets TranscodingAudioDecoder extend plain-WAV
        // probing to non-WAV containers (MP3, etc.) -- see FfmpegAsset's own doc comment for the
        // pinned version/license this build downloads.
        var downloadsDirectory = Path.Combine(appDataDirectory, "downloads");
        services.AddSingleton<HttpReleaseAssetDownloader>();
        services.AddSingleton(sp => new AssetInstaller(downloadsDirectory, sp.GetRequiredService<HttpReleaseAssetDownloader>()));
        // Windows downloads the pinned build; other platforms use an ffmpeg the user already installed (e.g. Homebrew).
        services.AddSingleton<IFfmpegInstaller>(sp => OperatingSystem.IsWindows()
            ? new FfmpegInstaller(Path.Combine(appDataDirectory, "tools", "ffmpeg"), sp.GetRequiredService<AssetInstaller>())
            : new SystemFfmpegInstaller());
        services.AddSingleton(sp => new TranscodingAudioDecoder(
            new WavAudioDecoder(), sp.GetRequiredService<IFfmpegInstaller>(), Path.Combine(appDataDirectory, "audio-transcode-cache")));
        services.AddSingleton(_ => new JsonSongAssetStore(Path.Combine(appDataDirectory, "audio-assets")));
        services.AddSingleton(_ => new JsonWaveformPyramidStore(Path.Combine(appDataDirectory, "waveform-cache")));

        // Real whole-song AI pipeline (M1-M4): the vocal/instrument separation model is a second,
        // separate optional consent-gated download (the Show Import screen's "Download separation
        // model" action) -- see SherpaOnnxSpleeterModelAsset's own doc comment for the pinned
        // version/license (and disclosed Spleeter-weights license caveat) this build downloads.
        services.AddSingleton(_ => new ModelAssetManagerRegistry(
            new ModelAssetManager(Path.Combine(appDataDirectory, "tools", "stem-separation-model"), new HttpModelAssetDownloader())));
        services.AddSingleton(sp => new SpectrogramStemSeparatorAdapter(
            new SpectrogramOnnxStemSeparator(
                sp.GetRequiredService<ModelAssetManagerRegistry>().StemSeparation, new TarBzip2ModelArchiveExpander(),
                new WavStereoAudioDecoder(), Path.Combine(appDataDirectory, "stem-separation-output")),
            SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe));
        services.AddSingleton(sp => new AudioAnalysisRunner(
            sp.GetRequiredService<TranscodingAudioDecoder>(), sp.GetRequiredService<SpectrogramStemSeparatorAdapter>(),
            SherpaOnnxSpleeterModelAsset.Archive,
            new JsonOnsetAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "onsets")),
            new JsonTempoAnalysisStore(Path.Combine(appDataDirectory, "analysis-cache", "tempo")),
            new JsonBeatGridStore(Path.Combine(appDataDirectory, "analysis-cache", "beatgrid")),
            new JsonEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "energy")),
            new JsonPitchTrackStore(Path.Combine(appDataDirectory, "analysis-cache", "pitch")),
            new JsonStemActivityResultStore(Path.Combine(appDataDirectory, "analysis-cache", "vocal-activity")),
            new JsonBandEnergyEnvelopeStore(Path.Combine(appDataDirectory, "analysis-cache", "band-energy")),
            sp.GetRequiredService<ILogger<AudioAnalysisRunner>>()));
        services.AddSingleton(sp => new StructureAnalysisRunner(
            new JsonBarFingerprintStore(Path.Combine(appDataDirectory, "analysis-cache", "fingerprints")),
            new JsonSectionBoundaryStore(Path.Combine(appDataDirectory, "analysis-cache", "boundaries")),
            new JsonSectionLabelStore(Path.Combine(appDataDirectory, "analysis-cache", "labels")),
            new JsonSectionLabelOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "label-overrides")),
            new JsonPhraseSegmentationStore(Path.Combine(appDataDirectory, "analysis-cache", "phrases")),
            new JsonPhraseBoundaryOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "phrase-overrides")),
            new JsonMotifClusteringStore(Path.Combine(appDataDirectory, "analysis-cache", "motifs")),
            sp.GetRequiredService<ILogger<StructureAnalysisRunner>>()));
        services.AddSingleton(sp => new WholeSongGenerator(sp.GetRequiredService<AudioAnalysisRunner>(), sp.GetRequiredService<StructureAnalysisRunner>()));

        // Build-plan "richer analysis tools" Phase 6 (2026-11-02): speech-to-text over the vocals stem, a
        // third, separate optional consent-gated download (same "never a hidden download" convention as
        // the stem-separation model above) -- see SherpaOnnxWhisperTinyEnModelAsset's own doc comment for
        // why this specific real model/vocabulary was chosen. Never blocks generation: a not-yet-installed
        // model is caught inside AiChoreographyWholeSongGenerator itself and simply yields no transcript.
        services.AddSingleton(sp => new WhisperOnnxTranscriber(
            new ModelAssetManager(Path.Combine(appDataDirectory, "tools", "whisper-transcription-model"), new HttpModelAssetDownloader()),
            new TarBzip2ModelArchiveExpander(), sp.GetRequiredService<TranscodingAudioDecoder>(),
            SherpaOnnxWhisperTinyEnModelAsset.Bundle, Path.Combine(appDataDirectory, "whisper-transcription-output")));

        // Build-plan Phases 5-8: the real LLM-driven choreography pipeline, replacing the old M1-M4
        // flat-"On" path as the production default (WholeSongGenerator above is kept, unused, so the old
        // path remains available rather than deleted). Reuses the exact %AppData%/ai-providers and
        // %AppData%/credentials paths AiProviderSettingsView's own code-behind already uses -- the same
        // Settings screen the user configures the Director/Choreographer roles on is what this reads
        // from, not a second, parallel config location.
        services.AddSingleton(_ => new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers")));
        services.AddSingleton(_ => CredentialStoreFactory.Create(Path.Combine(appDataDirectory, "credentials")));
        // HttpClient.Timeout defaults to 100s and governs the whole request regardless of any
        // CancellationToken passed to SendAsync, overriding RoleProviderConfig.RequestTimeoutSeconds
        // entirely -- confirmed against a real local Ollama model, whose chat-completion calls routinely
        // exceed 100s. Left unbounded here so RequestTimeoutSeconds/HttpJsonRequestExecutor's own
        // per-attempt timeout is the only real timeout in effect, as the config value is meant to be.
        services.AddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        services.AddSingleton(sp => new ChatCompletionClientFactory(sp.GetRequiredService<ICredentialStore>(), sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(sp => new AiRoleClientResolver(
            sp.GetRequiredService<JsonRoleProviderConfigStore>(), sp.GetRequiredService<ICredentialStore>(), sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<ILogger<AiRoleClientResolver>>()));

        // No existing Shell setting names a configured xLights install directory (confirmed by prior
        // research across AppSettings/XlightsRenderCapabilityProbe/ShowLayoutImporter) -- this falls back
        // to the same last-resort path EmissionStrategySelector's own doc comment already documents.
        const string effectMetadataDirectory = @"C:\Program Files\xLights\effectmetadata";
        // Build-plan Phase 10 §3f ("Activate corpus mining"): the same directory
        // ShellViewModel's RunCorpusMiningCommand (Generate & Export screen's Developer Tools) persists
        // CorpusMiningRunner's CraftRules/ExemplarIndex/PatternLibrary under, so a real corpus-mining
        // run and every subsequent real generation agree on where that knowledge actually lives.
        var corpusKnowledgeDirectory = Path.Combine(appDataDirectory, "corpus-knowledge");
        services.AddSingleton(_ => new CorpusKnowledgeDirectory(corpusKnowledgeDirectory));
        services.AddSingleton(sp => new AiChoreographyWholeSongGenerator(
            sp.GetRequiredService<AudioAnalysisRunner>(), sp.GetRequiredService<StructureAnalysisRunner>(), sp.GetRequiredService<AiRoleClientResolver>(),
            effectMetadataDirectory, Path.Combine(appDataDirectory, "ai-choreography"),
            knowledgeDirectory: corpusKnowledgeDirectory,
            style: XlightsAI.Studio.Director.StylePresetCatalog.HighEnergyEdmId,
            transcriber: sp.GetRequiredService<WhisperOnnxTranscriber>(),
            transcriptAudioDecoder: sp.GetRequiredService<TranscodingAudioDecoder>(),
            loggerFactory: sp.GetRequiredService<ILoggerFactory>()));

        // Build-plan Phase 14 §2a.2: same directory AiChoreographyWholeSongGenerator's own
        // projectDirectory above uses (bible/scripts/llm/exports) -- the persisted custom prompt
        // sits as a sibling of those, round-tripping across sessions.
        services.AddSingleton(_ => new XlightsAI.Studio.Director.JsonCustomPromptStore(Path.Combine(appDataDirectory, "ai-choreography")));

        // Confirmed as a real bug against a real export: ShellViewModel's own default XsqExporter()
        // (parameterless) only knows EffectRecipeCatalog.CreateDefault()'s single hardcoded "On" recipe
        // (id effect.on.v1) -- it rejected every generated-vocabulary recipe id (effect.generated.*)
        // AiChoreographyWholeSongGenerator actually produces, since those two components silently used
        // different recipe catalogs. Built from the exact same effectMetadataDirectory the generator
        // itself uses, so generation and export are never out of sync again.
        services.AddSingleton(_ =>
        {
            var exportCatalogs = EffectCatalogFactory.BuildFromMetadataDirectory(effectMetadataDirectory);
            return new XsqExporter(exportCatalogs.Recipes, exportCatalogs.Vocabulary);
        });

        // Backs the Piano Roll/Structure/Display Paths screens (docs/UI-CLEANUP-PLAN.md item 2,
        // Phase 2a) — same "needs a real directory this layer does not yet own" reasoning as every
        // other JSON-backed store above.
        services.AddSingleton(_ => new NoteTrackEditor(new JsonMelodyTrackRevisionStore(Path.Combine(appDataDirectory, "melody-revisions"))));
        services.AddSingleton(_ => new JsonMotifLinkOverrideStore(Path.Combine(appDataDirectory, "analysis-cache", "motif-link-overrides")));
        services.AddSingleton(_ => new DisplayLayoutEditor(new JsonDisplayLayoutCorrectionsStore(Path.Combine(appDataDirectory, "display-layout-corrections"))));
        services.AddSingleton<DisplayGraphBuilder>();

        // Windows uses winmm, macOS uses OpenAL; any other platform gets UnsupportedPlatformAudioTransport,
        // which throws one clear, typed message on first use.
        services.AddSingleton(sp => AudioTransportFactory.Create(sp.GetRequiredService<TranscodingAudioDecoder>()));
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

/// <summary>Trivial DI-registrable wrapper so the plain knowledge-directory string App.CorpusKnowledgeDirectory reads can be resolved from the container like everything else, without registering a bare <see cref="string"/> (which DI containers resolve ambiguously across unrelated string dependencies).</summary>
public sealed record CorpusKnowledgeDirectory(string Value);

/// <summary>Same reasoning as <see cref="CorpusKnowledgeDirectory"/>, for the one <see cref="ModelAssetManager"/> App.StemModelAssetManager reads back out -- named by role rather than registered as a bare, ambiguous <see cref="ModelAssetManager"/>.</summary>
public sealed record ModelAssetManagerRegistry(ModelAssetManager StemSeparation);
