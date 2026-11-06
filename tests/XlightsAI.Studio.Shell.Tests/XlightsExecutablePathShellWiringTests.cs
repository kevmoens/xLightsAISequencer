namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// User feedback (2026-11-03): "Can't that be loaded on start up and don't allow generate until that is
/// filled in." Proves <see cref="ShowImportViewModel.XlightsExecutablePath"/> persists through a real
/// <see cref="SettingsStore"/> (not an in-memory stand-in), restores on startup, and gates
/// <see cref="ShellViewModel.GenerateWholeSongPlanCommand"/> only while the quality-pass toggle is on.
/// </summary>
[Trait("Category", "Integration")]
public sealed class XlightsExecutablePathShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-xlights-exe-path-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_XlightsExecutablePath_RoundTripsThroughPersistedJson()
    {
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore1 = new SettingsStore(appDirectory, diagnostics);

        var existing = settingsStore1.Load();
        Assert.Null(existing.XlightsExecutablePath);

        settingsStore1.Save(existing with { SchemaVersion = AppSettings.CurrentSchemaVersion, XlightsExecutablePath = @"C:\Program Files\xLights\xLights.exe" });

        var settingsStore2 = new SettingsStore(appDirectory, diagnostics);
        Assert.Equal(@"C:\Program Files\xLights\xLights.exe", settingsStore2.Load().XlightsExecutablePath);
    }

    [Fact]
    public void Startup_RestoresPersistedXlightsExecutablePath()
    {
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        settingsStore.Save(settingsStore.Load() with { SchemaVersion = AppSettings.CurrentSchemaVersion, XlightsExecutablePath = @"C:\Program Files\xLights\xLights.exe" });

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, diagnostics);
        vm.RunStartup();

        Assert.Equal(@"C:\Program Files\xLights\xLights.exe", vm.ShowImport.XlightsExecutablePath);
    }

    [Fact]
    public void SettingXlightsExecutablePath_PersistsImmediately_WithoutAnySeparateSaveAction()
    {
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, diagnostics);
        vm.RunStartup();

        vm.ShowImport.XlightsExecutablePath = @"C:\Program Files\xLights\xLights.exe";

        Assert.Equal(@"C:\Program Files\xLights\xLights.exe", settingsStore.Load().XlightsExecutablePath);
    }

    [Fact]
    public void GenerateWholeSongPlanCommand_QualityPassOff_NeverGatedOnExecutablePath()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());
        vm.RunStartup();

        vm.CustomPrompt.EnableQualityPass = false;
        vm.ShowImport.XlightsExecutablePath = "";

        Assert.True(vm.GenerateWholeSongPlanCommand.CanExecute(null));
    }

    [Fact]
    public void GenerateWholeSongPlanCommand_QualityPassOnWithNoExecutablePath_IsDisabled()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());
        vm.RunStartup();

        vm.CustomPrompt.EnableQualityPass = true;
        vm.ShowImport.XlightsExecutablePath = "";

        Assert.False(vm.GenerateWholeSongPlanCommand.CanExecute(null));
        Assert.True(vm.ShowQualityPassPathWarning);
    }

    [Fact]
    public void GenerateWholeSongPlanCommand_QualityPassOnWithExecutablePathFilledIn_IsEnabled()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());
        vm.RunStartup();

        vm.CustomPrompt.EnableQualityPass = true;
        vm.ShowImport.XlightsExecutablePath = @"C:\Program Files\xLights\xLights.exe";

        Assert.True(vm.GenerateWholeSongPlanCommand.CanExecute(null));
        Assert.False(vm.ShowQualityPassPathWarning);
    }

    [Fact]
    public void GenerateExportScreen_RestoresXlightsExecutablePath_FromSettings()
    {
        var appDirectory = Path.Combine(_root, "app");
        var diagnostics = new AppDiagnostics(Path.Combine(appDirectory, "logs"));
        var settingsStore = new SettingsStore(appDirectory, diagnostics);
        settingsStore.Save(settingsStore.Load() with { SchemaVersion = AppSettings.CurrentSchemaVersion, XlightsExecutablePath = @"C:\Program Files\xLights\xLights.exe" });

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore, diagnostics);
        vm.RunStartup();
        vm.NavigateToGenerateExportCommand.Execute(null);

        Assert.Equal(@"C:\Program Files\xLights\xLights.exe", vm.ShowImport.XlightsExecutablePath);
    }
}
