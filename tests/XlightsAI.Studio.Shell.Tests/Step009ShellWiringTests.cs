using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-02 Step 009: proves <see cref="ShellViewModel"/>'s <see cref="ShellViewModel.ShowImport"/>
/// screen and <see cref="ShellViewModel.DiscoverShowDirectoryCommand"/> are wired end to end through
/// a real <see cref="ShowDirectoryDiscoveryService"/> and a real <see cref="SettingsStore"/> (not
/// stand-ins): a successful discovery persists "last successful folder selection" to disk, a
/// restart restores it onto the screen, and earlier steps' constructor call sites (no
/// <see cref="IShowDirectoryDiscoveryService"/> supplied) keep compiling and behaving identically.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step009ShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step009-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Step009_Acceptance_Passes_DiscoverThenRestartRestoresDirectory()
    {
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), "<xrgb />");

        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();

        var navigator1 = new ShellNavigator();
        var shell1 = new ShellViewModel(
            navigator1,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            showDirectoryDiscoveryService: discoveryService);
        shell1.RunStartup();
        shell1.NavigateToShowImportCommand.Execute(null);

        shell1.ShowImport.ShowDirectory = showDirectory;
        Assert.True(shell1.DiscoverShowDirectoryCommand.CanExecute(null));
        shell1.DiscoverShowDirectoryCommand.Execute(null);
        await shell1.LastShowImportCommandTask!;

        Assert.Null(shell1.ShowImport.ErrorMessage);
        Assert.True(shell1.ShowImport.Discovery!.IsComplete);
        Assert.True(shell1.ShowImport.Discovery.HasRgbEffectsFile);

        // --- Restart: a fresh ShellViewModel must restore the persisted directory AND re-run discovery
        // automatically (user feedback 2026-11-01: "on startup preload Show layout if a folder is
        // there") since the folder still exists on disk.
        var navigator2 = new ShellNavigator();
        var shell2 = new ShellViewModel(
            navigator2,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            showDirectoryDiscoveryService: discoveryService);
        shell2.RunStartup();
        await shell2.LastStartupDiscoverShowDirectoryTask!;
        shell2.NavigateToShowImportCommand.Execute(null);

        Assert.Equal(showDirectory, shell2.ShowImport.ShowDirectory);
        Assert.NotNull(shell2.ShowImport.Discovery);
        Assert.True(shell2.ShowImport.Discovery.IsComplete);
    }

    [Fact]
    public async Task DiscoverAsync_PushesDirectoryToFrontOfRecentList_DedupedAndCapped()
    {
        // User feedback (2026-11-01): "when opening an existing show, can that be a drop down and select
        // existing and it sets the directory too."
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, diagnostics, showDirectoryDiscoveryService: discoveryService);
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        var showA = Path.Combine(_root, "show-a");
        var showB = Path.Combine(_root, "show-b");
        Directory.CreateDirectory(showA);
        Directory.CreateDirectory(showB);
        File.WriteAllText(Path.Combine(showA, ShowDirectoryFileNames.RgbEffects), "<xrgb />");
        File.WriteAllText(Path.Combine(showB, ShowDirectoryFileNames.RgbEffects), "<xrgb />");

        shell.ShowImport.ShowDirectory = showA;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        shell.ShowImport.ShowDirectory = showB;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        Assert.Equal([showB, showA], shell.ShowImport.RecentShowDirectories);

        // Re-opening showA must move it back to the front, not add a duplicate entry.
        shell.ShowImport.ShowDirectory = showA;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        Assert.Equal([showA, showB], shell.ShowImport.RecentShowDirectories);
    }

    [Fact]
    public async Task RestartWithLastShowDirectoryDeleted_SkipsAutoDiscovery_NoErrorSurfaced()
    {
        // User feedback (2026-11-01): a renamed/deleted remembered folder must not surface a startup
        // error -- the user still sees the ordinary empty Show Import screen.
        var showDirectory = Path.Combine(_root, "show");
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects), "<xrgb />");

        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();

        var navigator1 = new ShellNavigator();
        var shell1 = new ShellViewModel(navigator1, () => Array.Empty<string>(), settingsStore, diagnostics, showDirectoryDiscoveryService: discoveryService);
        shell1.RunStartup();
        shell1.NavigateToShowImportCommand.Execute(null);
        shell1.ShowImport.ShowDirectory = showDirectory;
        shell1.DiscoverShowDirectoryCommand.Execute(null);
        await shell1.LastShowImportCommandTask!;

        Directory.Delete(showDirectory, recursive: true);

        var navigator2 = new ShellNavigator();
        var shell2 = new ShellViewModel(navigator2, () => Array.Empty<string>(), settingsStore, diagnostics, showDirectoryDiscoveryService: discoveryService);
        shell2.RunStartup();

        Assert.Null(shell2.LastStartupDiscoverShowDirectoryTask);
        Assert.False(shell2.IsFailed);
    }

    [Fact]
    public async Task MissingRequiredFile_SetsErrorAndDoesNotPersistDirectory_NegativePath()
    {
        var showDirectory = Path.Combine(_root, "incomplete-show");
        Directory.CreateDirectory(showDirectory); // no xlights_rgbeffects.xml

        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var discoveryService = new ShowDirectoryDiscoveryService();

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            showDirectoryDiscoveryService: discoveryService);
        shell.RunStartup();
        shell.NavigateToShowImportCommand.Execute(null);

        shell.ShowImport.ShowDirectory = showDirectory;
        shell.DiscoverShowDirectoryCommand.Execute(null);
        await shell.LastShowImportCommandTask!;

        Assert.False(string.IsNullOrEmpty(shell.ShowImport.ErrorMessage));
        Assert.Null(settingsStore.Load().LastShowDirectory);
    }

    [Fact]
    public void ShellViewModel_WithoutShowDirectoryDiscoveryService_StillConstructsAndStarts_BackwardCompatible()
    {
        // Earlier steps' constructor call sites must keep compiling and behaving identically: no
        // IShowDirectoryDiscoveryService supplied defaults to NullShowDirectoryDiscoveryService.
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup();

        Assert.Equal(StartupState.Ready, vm.State);
        vm.NavigateToShowImportCommand.Execute(null);
        Assert.True(vm.DiscoverShowDirectoryCommand.CanExecute(null));
    }

    [Fact]
    public async Task DiscoverShowDirectoryCommand_WithNoServiceConfigured_ReportsErrorRatherThanThrowing_NegativePath()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());
        vm.RunStartup();
        vm.NavigateToShowImportCommand.Execute(null);

        vm.ShowImport.ShowDirectory = Path.Combine(_root, "unused");
        vm.DiscoverShowDirectoryCommand.Execute(null);
        await vm.LastShowImportCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.ShowImport.ErrorMessage));
    }
}
