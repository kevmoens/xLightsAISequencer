using Avalonia;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Projects;
using XlightsAI.Studio.Shell;
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
        var audioDecoder = new WavAudioDecoder();
        var songAssetStore = new JsonSongAssetStore(Path.Combine(appDataDirectory, "audio-assets"));
        var waveformPyramidStore = new JsonWaveformPyramidStore(Path.Combine(appDataDirectory, "waveform-cache"));

        // Mac/Linux stub coverage (docs/ARCHITECTURE-AND-GUARDRAILS.md "Platform coverage"): only
        // Windows has a real playback backend (WaveOutAudioTransport); every other platform gets
        // UnsupportedPlatformAudioTransport, which throws one clear, typed message on first use.
        IAudioTransport audioTransport = OperatingSystem.IsWindows()
            ? new WaveOutAudioTransport(audioDecoder)
            : new UnsupportedPlatformAudioTransport();

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

        try
        {
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
