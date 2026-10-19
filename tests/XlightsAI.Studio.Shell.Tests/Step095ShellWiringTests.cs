using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-15 Step 095 ("Build installer/uninstaller, show-folder onboarding and safe defaults").
/// Proves the real vertical slice end to end through <see cref="ShellViewModel"/>: a fresh
/// installation (no persisted settings) lands on the first-run wizard automatically — this step's
/// own required acceptance, verbatim: "User completes first run without command line" — choosing a
/// non-admin workspace folder persists it to real on-disk settings and creates the real folder, a
/// restart of the same installation never shows the wizard again (the "saved-state verification"
/// this step's own user-visible outcome names), and every required failure/edge case is proven
/// against real production types rather than a mocking framework.
/// </summary>
[Trait("Category", "Integration")]
public sealed class Step095ShellWiringTests : IDisposable
{
    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-step095-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Step095_Acceptance_Passes()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var settingsStore = new SettingsStore(appDirectory, NullAppDiagnostics.Instance);
        var workspaceDirectory = Path.Combine(_rootDirectory, "Projects");

        // --- First launch of a fresh installation: no settings.json exists yet. ---
        var navigator1 = new ShellNavigator();
        var vm1 = new ShellViewModel(navigator1, () => Array.Empty<string>(), settingsStore: settingsStore);
        vm1.RunStartup();

        // Required acceptance, verbatim: "User completes first run without command line" — the
        // wizard is reached automatically, never by the user typing a command.
        Assert.Equal(ShellScreen.FirstRun, vm1.CurrentScreen);
        Assert.True(vm1.IsShowingFirstRun);

        vm1.FirstRun.WorkspaceDirectory = workspaceDirectory;
        Assert.True(vm1.CompleteFirstRunCommand.CanExecute(null));
        vm1.CompleteFirstRunCommand.Execute(null);
        await vm1.LastCompleteFirstRunCommandTask!;

        Assert.Null(vm1.FirstRun.ErrorMessage);
        Assert.True(vm1.FirstRun.IsCompleted);
        // Real I/O: the workspace folder actually exists on disk now, not just in memory.
        Assert.True(Directory.Exists(workspaceDirectory));
        // Real probe: never a hard-coded value — matches the real running machine.
        Assert.NotNull(vm1.FirstRun.ProbedCapability);
        Assert.Contains(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(), vm1.FirstRun.ProbedCapability!.Architecture);
        // Completing first run returns to Welcome, not stuck on the wizard.
        Assert.Equal(ShellScreen.Welcome, vm1.CurrentScreen);

        // Real on-disk settings file now reflects the saved state.
        var persisted = settingsStore.Load();
        Assert.True(persisted.HasCompletedFirstRun);
        Assert.Equal(workspaceDirectory, persisted.WorkspaceDirectory);

        // --- "Restart" the same installation: a brand-new ShellViewModel over the same real
        // settings file must never show the wizard again (saved-state verification). ---
        var navigator2 = new ShellNavigator();
        var vm2 = new ShellViewModel(navigator2, () => Array.Empty<string>(), settingsStore: settingsStore);
        vm2.RunStartup();

        Assert.NotEqual(ShellScreen.FirstRun, vm2.CurrentScreen);
        Assert.False(vm2.IsShowingFirstRun);
    }

    [Fact]
    public async Task CompleteFirstRunCommand_WhenWorkspaceDirectoryEmpty_SetsErrorMessage_NeverCompletes_NegativePath()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var settingsStore = new SettingsStore(appDirectory, NullAppDiagnostics.Instance);
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore: settingsStore);
        vm.RunStartup();
        Assert.Equal(ShellScreen.FirstRun, vm.CurrentScreen);

        vm.FirstRun.WorkspaceDirectory = string.Empty;
        vm.CompleteFirstRunCommand.Execute(null);
        await vm.LastCompleteFirstRunCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.FirstRun.ErrorMessage));
        Assert.False(vm.FirstRun.IsCompleted);
        Assert.Equal(ShellScreen.FirstRun, vm.CurrentScreen);
        Assert.False(settingsStore.Load().HasCompletedFirstRun);
    }

    [Fact]
    public async Task CompleteFirstRunCommand_WhenWorkspaceUnderProgramFiles_RejectedAsAdminOnly_NegativePath()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var settingsStore = new SettingsStore(appDirectory, NullAppDiagnostics.Instance);
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>(), settingsStore: settingsStore);
        vm.RunStartup();

        vm.FirstRun.WorkspaceDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XlightsAI Studio Projects");
        vm.CompleteFirstRunCommand.Execute(null);
        await vm.LastCompleteFirstRunCommandTask!;

        Assert.False(string.IsNullOrEmpty(vm.FirstRun.ErrorMessage));
        Assert.Contains("administrator-only", vm.FirstRun.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.FirstRun.IsCompleted);
        Assert.False(settingsStore.Load().HasCompletedFirstRun);
    }

    /// <summary>
    /// Required failure/edge case, verbatim: "Clean machine and unsupported runtime must produce
    /// user-readable diagnostics." A named fixture type stands in for an unsupported machine (this
    /// test's own real host is always a supported combination) — same "Null*-style convention, not a
    /// mocking framework" precedent PHASE-15 Step 094's own evidence already established.
    /// </summary>
    [Fact]
    public async Task CompleteFirstRunCommand_WithUnsupportedRuntime_StillCompletes_WithReadableDiagnostics_NegativePath()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var settingsStore = new SettingsStore(appDirectory, NullAppDiagnostics.Instance);
        var workspaceDirectory = Path.Combine(_rootDirectory, "Projects");
        var unsupportedCapability = new RuntimeCapability("Linux 6.1.0", "Arm64", true, false, null);
        var fixtureDetector = new FixtureRuntimeCapabilityDetector(unsupportedCapability);
        var fixtureHealthChecks = new FixtureStartupHealthChecks(
            ["Unsupported platform: 'Linux 6.1.0' / 'Arm64' is not a supported combination for this release."]);

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore: settingsStore,
            runtimeCapabilityDetector: fixtureDetector,
            startupHealthChecks: fixtureHealthChecks);
        vm.RunStartup();

        vm.FirstRun.WorkspaceDirectory = workspaceDirectory;
        vm.CompleteFirstRunCommand.Execute(null);
        await vm.LastCompleteFirstRunCommandTask!;

        // A reportable diagnostic never blocks startup/first-run by itself (Section 1: the app must
        // still work, degraded, without ML) — only an actually unusable folder choice does.
        Assert.Null(vm.FirstRun.ErrorMessage);
        Assert.True(vm.FirstRun.IsCompleted);
        Assert.True(vm.FirstRun.HasHealthIssues);
        Assert.Contains("Unsupported platform", vm.FirstRun.HealthIssues[0]);
        Assert.Equal(unsupportedCapability, vm.FirstRun.ProbedCapability);
        Assert.True(settingsStore.Load().HasCompletedFirstRun);
    }

    [Fact]
    public async Task CompleteFirstRunCommand_InvokedTwiceBeforeFirstCompletes_OnlyRunsOnce_NegativePath()
    {
        var appDirectory = Path.Combine(_rootDirectory, "app");
        var settingsStore = new SettingsStore(appDirectory, NullAppDiagnostics.Instance);
        var gate = new TaskCompletionSource();
        var slowDetector = new SlowCountingRuntimeCapabilityDetector(gate.Task);

        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            settingsStore: settingsStore,
            runtimeCapabilityDetector: slowDetector);
        vm.RunStartup();

        vm.FirstRun.WorkspaceDirectory = Path.Combine(_rootDirectory, "Projects");
        vm.CompleteFirstRunCommand.Execute(null);
        var firstTask = vm.LastCompleteFirstRunCommandTask!;

        vm.CompleteFirstRunCommand.Execute(null);
        var secondTask = vm.LastCompleteFirstRunCommandTask!;

        gate.SetResult();
        await firstTask;
        await secondTask;

        Assert.Equal(1, slowDetector.ProbeCallCount);
    }

    private sealed class FixtureRuntimeCapabilityDetector(RuntimeCapability capability) : IRuntimeCapabilityDetector
    {
        public Task<RuntimeCapability> ProbeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(capability);
        }
    }

    private sealed class FixtureStartupHealthChecks(IReadOnlyList<string> issues) : IStartupHealthChecks
    {
        public Task<IReadOnlyList<string>> CheckAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(issues);
        }
    }

    private sealed class SlowCountingRuntimeCapabilityDetector(Task gate) : IRuntimeCapabilityDetector
    {
        public int ProbeCallCount { get; private set; }

        public async Task<RuntimeCapability> ProbeAsync(CancellationToken ct)
        {
            ProbeCallCount++;
            await gate;
            return new RuntimeCapability("Test OS", "X64", true, false, null);
        }
    }
}
