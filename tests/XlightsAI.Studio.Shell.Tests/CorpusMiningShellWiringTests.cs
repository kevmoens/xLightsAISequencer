namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// Build-plan Phase 10 §3f ("Activate corpus mining"): proves <see cref="ShellViewModel"/>'s
/// <see cref="ShellViewModel.RunCorpusMiningCommand"/>/<see cref="ShellViewModel.ShowImport"/>
/// corpus-mining state are wired end to end through a real <see cref="SettingsStore"/> (not a
/// stand-in): <see cref="AppSettings.CorpusRootDirectory"/> round-trips through persisted JSON, the
/// command is disabled until a real <c>corpusKnowledgeDirectory</c> is supplied by the composition
/// root (same reasoning as every other "needs a real configured directory" collaborator), and a
/// failed run (no real corpus at the typed directory) reports an explicit error rather than throwing
/// or silently persisting a bad directory.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CorpusMiningShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-corpus-mining-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_CorpusRootDirectory_RoundTripsThroughPersistedJson()
    {
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore1 = new SettingsStore(appDirectory, diagnostics);

        var existing = settingsStore1.Load();
        Assert.Null(existing.CorpusRootDirectory);

        settingsStore1.Save(existing with { SchemaVersion = AppSettings.CurrentSchemaVersion, CorpusRootDirectory = @"C:\xLights" });

        // A fresh store instance over the same directory must read back exactly what was written --
        // same "real persisted JSON, not an in-memory stand-in" proof as every other AppSettings field.
        var settingsStore2 = new SettingsStore(appDirectory, diagnostics);
        Assert.Equal(@"C:\xLights", settingsStore2.Load().CorpusRootDirectory);
    }

    [Fact]
    public void RunCorpusMiningCommand_WithoutKnowledgeDirectoryConfigured_IsDisabled()
    {
        // No corpusKnowledgeDirectory supplied -- same "needs a real configured directory this layer
        // does not yet own" reasoning as every other optional collaborator; the button must stay
        // disabled rather than attempt a write to a guessed location.
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());
        vm.RunStartup();
        vm.NavigateToGenerateExportCommand.Execute(null);

        Assert.False(vm.RunCorpusMiningCommand.CanExecute(null));
    }

    [Fact]
    public async Task RunCorpusMiningCommand_WithConfiguredDirectory_CorpusRootDoesNotExist_ReportsErrorAndDoesNotPersist_NegativePath()
    {
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        var knowledgeDirectory = Path.Combine(_root, "corpus-knowledge");

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore,
            diagnostics,
            corpusKnowledgeDirectory: knowledgeDirectory);
        vm.RunStartup();
        vm.NavigateToGenerateExportCommand.Execute(null);

        vm.ShowImport.CorpusRootDirectory = Path.Combine(_root, "does-not-exist");
        Assert.True(vm.RunCorpusMiningCommand.CanExecute(null));
        vm.RunCorpusMiningCommand.Execute(null);
        await vm.LastRunCorpusMiningCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.ShowImport.CorpusMiningErrorMessage));
        Assert.False(vm.ShowImport.IsCorpusMiningBusy);
        Assert.Null(settingsStore.Load().CorpusRootDirectory);
    }

    [Fact]
    public async Task RunCorpusMiningCommand_WithBlankCorpusRootDirectory_ReportsErrorWithoutThrowing_NegativePath()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            corpusKnowledgeDirectory: Path.Combine(_root, "corpus-knowledge"));
        vm.RunStartup();
        vm.NavigateToGenerateExportCommand.Execute(null);

        vm.RunCorpusMiningCommand.Execute(null);
        await vm.LastRunCorpusMiningCommandTask!;

        Assert.Equal("Enter a corpus root directory before running corpus mining.", vm.ShowImport.CorpusMiningErrorMessage);
    }

    [Fact]
    public void GenerateExportScreen_RestoresLastCorpusRootDirectory_FromSettings()
    {
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        settingsStore.Save(settingsStore.Load() with { SchemaVersion = AppSettings.CurrentSchemaVersion, CorpusRootDirectory = @"C:\xLights" });

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, diagnostics);
        vm.RunStartup();
        vm.NavigateToGenerateExportCommand.Execute(null);

        Assert.Equal(@"C:\xLights", vm.ShowImport.CorpusRootDirectory);
    }
}
