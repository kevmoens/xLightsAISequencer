using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-02 Step 010: proves <see cref="ShellViewModel"/>'s existing <see cref="ShellViewModel.DiscoverShowDirectoryCommand"/>
/// (Step 009) now also runs a real <see cref="ShowLayoutImporter"/> end to end and exposes the
/// parsed <see cref="ShowImportViewModel.Layout"/> on the same screen, and that a layout that fails
/// to parse safely surfaces as an explicit screen error rather than an unhandled exception, with
/// Step 009's own existing tests (no explicit importer supplied) still passing unmodified.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step010ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step010-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Step010_Acceptance_Passes_DiscoverAlsoParsesLayoutOntoScreen()
    {
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(
            Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects),
            """
            <xrgb FormatVersion="2" BaseChannel="0">
              <models>
                <model name="Arch-01" DisplayAs="Arches" WorldPosX="1" WorldPosY="2" />
              </models>
              <modelGroups>
                <modelGroup name="Front-Yard" models="Arch-01" />
              </modelGroups>
            </xrgb>
            """);

        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();
        var layoutImporter = new ShowLayoutImporter();

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            showDirectoryDiscoveryService: discoveryService,
            showLayoutImporter: layoutImporter);
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        Assert.Null(shell.ShowImport.ErrorMessage);
        Assert.True(shell.ShowImport.IsLoaded);
        Assert.NotNull(shell.ShowImport.Layout);
        var model = Assert.Single(shell.ShowImport.Layout!.Models);
        Assert.Equal("Arch-01", model.XlightsName);
        Assert.Equal(["Front-Yard"], model.GroupNames);
        var point = Assert.Single(model.PreviewPoints);
        Assert.Equal(1, point.X);
        Assert.Equal(2, point.Y);
    }

    /// <summary>A show directory whose required file exists but is not safely parseable surfaces an explicit screen error, not an unhandled exception.</summary>
    [Fact]
    public async Task MalformedRgbEffects_SetsScreenError_NegativePath()
    {
        var showDirectory = Path.Combine(_root, "malformed-show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), "<xrgbEffects><models>");

        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();
        var layoutImporter = new ShowLayoutImporter();

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            showDirectoryDiscoveryService: discoveryService,
            showLayoutImporter: layoutImporter);
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        Assert.True(shell.ShowImport.IsError);
        Assert.False(string.IsNullOrEmpty(shell.ShowImport.ErrorMessage));
        Assert.Null(shell.ShowImport.Layout);
        // A failed parse must never update the persisted "last successful folder selection" (Step 009 contract, preserved).
        Assert.Null(settingsStore.Load().LastShowDirectory);
    }

    /// <summary>
    /// Backward compatibility: constructing without an explicit <see cref="IShowLayoutImporter"/>
    /// (Step 009's own existing call-site shape) still constructs and runs discovery — the default
    /// collaborator is a real, working importer, not a throwing stub (see <see cref="ShellViewModel"/>'s
    /// constructor note), so Step 009's already-accepted "discover succeeds" tests keep passing.
    /// </summary>
    [Fact]
    public async Task ShellViewModel_WithoutExplicitLayoutImporter_StillParsesLayout_BackwardCompatible()
    {
        var showDirectory = Path.Combine(_root, "default-importer-show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), "<xrgb />");

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            showDirectoryDiscoveryService: new ShowDirectoryDiscoveryService());
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        Assert.Null(shell.ShowImport.ErrorMessage);
        Assert.NotNull(shell.ShowImport.Layout);
        Assert.Empty(shell.ShowImport.Layout!.Models);
    }
}
