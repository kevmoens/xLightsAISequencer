using System.Diagnostics;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-02 Step 014: proves <see cref="ShellViewModel.LaunchXlightsCommand"/> runs the real
/// <see cref="XlightsLauncher"/> end to end from the same <see cref="ShowImportViewModel.LastExport"/>
/// Step 013 already populates, and that every named failure/edge case surfaces as an explicit screen
/// error rather than an unhandled exception or a stuck busy state.
///
/// No installed xLights executable exists on this machine (carried forward from every prior PHASE-02
/// step's own disclosure). <c>where.exe</c> stands in for "an installed target executable" purely to
/// prove the real process-launch plumbing end to end; it is explicitly NOT xLights. The actual required
/// acceptance text — "Human-attested integration test with screenshot/render notes" — remains this
/// step's own separately-scoped, BLOCKED item (see implementation-evidence/PHASE-02/STEP-14.md).
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step014ShellWiringTests : IDisposable
{
    private static readonly string StandInExecutablePath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe")
        : "/usr/bin/true";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step014-shell-" + Guid.NewGuid().ToString("N"));
    private readonly List<int> _spawnedProcessIds = new();

    public void Dispose()
    {
        foreach (var pid in _spawnedProcessIds)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (ArgumentException)
            {
                // Already exited before we could look it up — expected for a `where.exe` stand-in.
            }
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static readonly string RgbEffectsXml =
        """
        <xrgb FormatVersion="2" BaseChannel="0">
          <models>
            <model name="Roofline" DisplayAs="Star" />
            <model name="Arch 1" DisplayAs="Arches" />
            <model name="Arch 2" DisplayAs="Arches" />
          </models>
          <modelGroups>
            <modelGroup name="Front-Arches" models="Roofline,Arch 1,Arch 2" />
          </modelGroups>
        </xrgb>
        """;

    private async Task<ShellViewModel> BuildShellWithExportedXsqAsync()
    {
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), RgbEffectsXml);

        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();
        var layoutImporter = new ShowLayoutImporter();
        var sequenceDocumentStore = new JsonSequenceDocumentStore(Path.Combine(appDirectory, "exports"));

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            showDirectoryDiscoveryService: discoveryService,
            showLayoutImporter: layoutImporter,
            sequenceDocumentStore: sequenceDocumentStore);
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        shell.ShowImport.EffectTargetNamesCsv = "Roofline, Arch 1, Arch 2";
        shell.GenerateEffectPlanCommand.Execute(null);
        await shell.LastGenerateEffectPlanCommandTask!;

        shell.ShowImport.XsqOutputPath = Path.Combine(_root, "exported.xsq");
        shell.ExportXsqCommand.Execute(null);
        await shell.LastExportXsqCommandTask!;

        return shell;
    }

    /// <summary>The phase gate's exact demo workflow continued: after Discover + Generate 3 Effects + Export XSQ, Open in xLights really starts a real configured executable against the real exported file.</summary>
    [Fact]
    public async Task Step014_Acceptance_Passes_LaunchXlightsEndToEnd()
    {
        Assert.True(File.Exists(StandInExecutablePath), $"Expected a real stand-in executable at '{StandInExecutablePath}'.");

        var shell = await BuildShellWithExportedXsqAsync();
        var exportedPath = shell.ShowImport.LastExport!.Path;
        shell.ShowImport.XlightsExecutablePath = StandInExecutablePath;

        shell.LaunchXlightsCommand.Execute(null);
        await shell.LastLaunchXlightsCommandTask!;

        Assert.Null(shell.ShowImport.LaunchErrorMessage);
        Assert.NotNull(shell.ShowImport.LastLaunch);
        Assert.True(shell.ShowImport.HasLaunch);
        Assert.Equal(exportedPath, shell.ShowImport.LastLaunch!.XsqPath);
        Assert.True(shell.ShowImport.LastLaunch!.ProcessId > 0);
        _spawnedProcessIds.Add(shell.ShowImport.LastLaunch!.ProcessId);
        Assert.False(shell.ShowImport.IsLaunchBusy);
    }

    /// <summary>Opening in xLights before Export XSQ has ever populated LastExport fails safely with an explicit screen error, never an unhandled exception.</summary>
    [Fact]
    public async Task LaunchWithoutAnExportYet_SetsScreenError_NegativePath()
    {
        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>());
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.XlightsExecutablePath = StandInExecutablePath;
        shell.LaunchXlightsCommand.Execute(null);
        await shell.LastLaunchXlightsCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.LaunchErrorMessage));
        Assert.Null(shell.ShowImport.LastLaunch);
        Assert.False(shell.ShowImport.IsLaunchBusy);
    }

    /// <summary>A missing/incompatible configured executable (the real state of this environment: no xLights install) fails safely with an explicit, actionable screen error and a recoverable (non-stuck) busy state.</summary>
    [Fact]
    public async Task LaunchWithMissingExecutable_SetsScreenError_NegativePath()
    {
        var shell = await BuildShellWithExportedXsqAsync();
        var missingExePath = Path.Combine(_root, "no-such-xlights.exe");
        shell.ShowImport.XlightsExecutablePath = missingExePath;

        shell.LaunchXlightsCommand.Execute(null);
        await shell.LastLaunchXlightsCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.LaunchErrorMessage));
        Assert.Contains(missingExePath, shell.ShowImport.LaunchErrorMessage, StringComparison.Ordinal);
        Assert.Null(shell.ShowImport.LastLaunch);
        Assert.False(shell.ShowImport.IsLaunchBusy); // recoverable: not stuck busy after the failure.

        // Recoverable state: a corrected path can be retried immediately without reconstructing the screen.
        shell.ShowImport.XlightsExecutablePath = StandInExecutablePath;
        shell.LaunchXlightsCommand.Execute(null);
        await shell.LastLaunchXlightsCommandTask!;

        Assert.Null(shell.ShowImport.LaunchErrorMessage);
        Assert.NotNull(shell.ShowImport.LastLaunch);
        _spawnedProcessIds.Add(shell.ShowImport.LastLaunch!.ProcessId);
    }

    /// <summary>Constructing without an explicit <see cref="IXlightsLauncher"/> (every earlier step's own existing call-site shape) keeps compiling and still launches for real via the real default instance.</summary>
    [Fact]
    public async Task ShellViewModel_WithoutExplicitXlightsLauncher_StillLaunchesForReal_BackwardCompatible()
    {
        var shell = await BuildShellWithExportedXsqAsync();
        shell.ShowImport.XlightsExecutablePath = StandInExecutablePath;

        shell.LaunchXlightsCommand.Execute(null);
        await shell.LastLaunchXlightsCommandTask!;

        Assert.Null(shell.ShowImport.LaunchErrorMessage);
        Assert.NotNull(shell.ShowImport.LastLaunch);
        _spawnedProcessIds.Add(shell.ShowImport.LastLaunch!.ProcessId);
    }
}
